using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Telephony;
using Android.Views;
using Android.Widget;

using AndroidX.AppCompat.App;

using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;

using Plugin.Permissions;
using Plugin.Permissions.Abstractions;

using System;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;

using Permission = Android.Content.PM.Permission;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.PersistentActivity)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]
[assembly: UsesPermission(Android.Manifest.Permission.PostNotifications)]
[assembly: UsesPermission(Android.Manifest.Permission.ReadPhoneState)]
[assembly: UsesPermission(Android.Manifest.Permission.ReadCallLog)]
[assembly: UsesPermission(Android.Manifest.Permission.ReadSms)]
[assembly: UsesPermission(Android.Manifest.Permission.ReceiveSms)]
[assembly: UsesPermission(Android.Manifest.Permission.RequestIgnoreBatteryOptimizations)]
[assembly: UsesPermission(Android.Manifest.Permission.SendSms)]
namespace SmsForwarder
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private static MainActivity? _instance;
        private Intent? _intent;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private TelegramService? _telegram;
        private static readonly ConcurrentQueue<SmsContent> SmsQueue = new ConcurrentQueue<SmsContent>();

        private static TextView? _tvSmsContent;
        private static CheckBox? _smsPermissionCheckBox;
        private static CheckBox? _phonePermissionCheckBox;
        private static CheckBox? _callLogPermissionCheckBox;
        private static CheckBox? _lastSmsCheckBox;


        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            _instance = this;

            _telegram = new TelegramService(AppSettings.TelegramToken, AppSettings.AuthorisedUsers);

            var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            var fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            if (fab != null)
                fab.Click += FabOnClick;

            var tokenText = FindViewById<EditText>(Resource.Id.editTextToken);
            if (tokenText != null)
            {
                tokenText.Text = AppSettings.TelegramToken;
                tokenText.FocusChange += (sender, args) =>
                {
                    if (!args.HasFocus && AppSettings.TelegramToken != tokenText.Text)
                    {
                        AppSettings.TelegramToken = tokenText.Text;
                        _telegram?.SetToken(AppSettings.TelegramToken);
                    }
                };
            }

            var usersText = FindViewById<EditText>(Resource.Id.editTextUsers);
            if (usersText != null)
            {
                usersText.Text = AppSettings.AuthorisedUsersString;
                usersText.FocusChange += (sender, args) =>
                {
                    if (!args.HasFocus && AppSettings.AuthorisedUsersString != usersText.Text)
                    {
                        AppSettings.AuthorisedUsersString = usersText.Text;
                        usersText.Text = AppSettings.AuthorisedUsersString;

                        if (_telegram != null)
                            _telegram.AuthorisedUsers = AppSettings.AuthorisedUsers;
                    }
                };
            }

            _tvSmsContent = FindViewById<TextView>(Resource.Id.tvSMSContent);

            RequestPermissions(new string[]
            {
                Android.Manifest.Permission.Internet,
                Android.Manifest.Permission.PersistentActivity,
                Android.Manifest.Permission.ForegroundService,
                Android.Manifest.Permission.PostNotifications,
                Android.Manifest.Permission.ReadPhoneState,
                Android.Manifest.Permission.ReadSms,
                Android.Manifest.Permission.ReceiveSms,
                Android.Manifest.Permission.ReadCallLog,
                Android.Manifest.Permission.RequestIgnoreBatteryOptimizations,
                Android.Manifest.Permission.SendSms,
            }, 0);

            _smsPermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxSmsPermission);
            var status = CheckSmsPermission().Result;
            if (_smsPermissionCheckBox != null)
                _smsPermissionCheckBox.Checked = status == PermissionStatus.Granted;

            _phonePermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxPhonePermission);
            status = CheckPhonePermission().Result;
            if (_phonePermissionCheckBox != null)
                _phonePermissionCheckBox.Checked = status == PermissionStatus.Granted;

            _callLogPermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxCallLogPermission);
            var s = CheckSelfPermission(Android.Manifest.Permission.ReadCallLog);
            if (_callLogPermissionCheckBox != null)
                _callLogPermissionCheckBox.Checked = s == Permission.Granted;

            _lastSmsCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxLastSms);

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                var token = _cts.Token;
                await QueueWatcherTask(token);
            });

            _intent = new Intent(this, typeof(SmsForwardingService));
            this.StartForegroundServiceCompat<SmsForwardingService>();

            /*var pushIntent = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
            StartActivity(pushIntent);*/
        }

        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            var id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private static void FabOnClick(object sender, EventArgs eventArgs)
        {
            var view = (View)sender;
            Snackbar.Make(view, "Contact author at jekyll2006@gmail.com", BaseTransientBottomBar.LengthLong)
                //.SetAction("Action", (View.IOnClickListener)null)
                .Show();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public static async Task<PermissionStatus> CheckSmsPermission()
        {
            try
            {
                return await CrossPermissions.Current.CheckPermissionStatusAsync<SmsPermission>();
            }
            catch (Exception ex)
            {
                return PermissionStatus.Unknown;
            }
        }

        public static async Task<PermissionStatus> CheckPhonePermission()
        {
            try
            {
                return await CrossPermissions.Current.CheckPermissionStatusAsync<PhonePermission>();
            }
            catch (Exception ex)
            {
                return PermissionStatus.Unknown;
            }
        }

        public static void EnqueueSms(SmsContent smsContent)
        {
            SmsQueue.Enqueue(smsContent);

            if (_lastSmsCheckBox?.Checked == true && _tvSmsContent != null)
            {
                _tvSmsContent.Text =
                    $"SMS received from [{smsContent.Sender}]:\r\n{smsContent.Message}\r\n\r\n";
            }
        }

        private async Task QueueWatcherTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (SmsQueue.TryDequeue(out var sms))
                {
                    try
                    {
                        var messageContent = $"SMS from [{sms.Sender}]: {sms.Message}";
                        _telegram?.SendText(AppSettings.AuthorisedUsers, messageContent, _cts?.Token ?? CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                }

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }

        public static void ShowToast(string text)
        {
            try
            {
                _instance?.RunOnUiThread(() =>
                {
                    Toast.MakeText(Android.App.Application.Context, text, ToastLength.Long)?.Show();
                });

            }
            catch (Exception ex)
            {
            }
        }

        protected override void OnDestroy()
        {
            SmsQueue.Clear();
            StopService(_intent);
            _cts?.Cancel();
            _cts?.Dispose();
            _telegram?.Dispose();
            base.OnDestroy();
        }
    }
}
