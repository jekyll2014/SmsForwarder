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
using System.Threading;
using System.Threading.Tasks;

using Toolbar = AndroidX.AppCompat.Widget.Toolbar;

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

        private TextView? _tvSmsContent;
        private CheckBox? _smsPermissionCheckBox;
        private CheckBox? _phonePermissionCheckBox;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            var fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            if (fab != null)
                fab.Click += FabOnClick;

            var tokenText = FindViewById<EditText>(Resource.Id.editTextToken);
            if (tokenText != null)
            {
                tokenText.Text = AppSettings.TelegramToken;
                tokenText.AfterTextChanged += (sender, args) =>
                {
                    AppSettings.TelegramToken = args.Editable?.ToString() ?? string.Empty;
                };
            }

            var usersText = FindViewById<EditText>(Resource.Id.editTextUsers);
            if (usersText != null)
            {
                usersText.Text = AppSettings.AuthorisedUsersString;
                usersText.AfterTextChanged += (sender, args) =>
                {
                    AppSettings.AuthorisedUsersString = args.Editable?.ToString() ?? string.Empty;
                };
            }

            _tvSmsContent = FindViewById<TextView>(Resource.Id.tvSMSContent);

            _smsPermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxSmsPermission);
            var status = RequestSmsPermission().Result;
            if (_smsPermissionCheckBox != null)
                _smsPermissionCheckBox.Checked = status == PermissionStatus.Granted;

            // ToDo: fix 2nd permission request hangs the program
            _phonePermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxPhonePermission);
            status = RequestPhonePermission().Result;
            if (_phonePermissionCheckBox != null)
                _phonePermissionCheckBox.Checked = status == PermissionStatus.Granted;

            _instance = this;
            _telegram = new TelegramService(AppSettings.TelegramToken, AppSettings.AuthorisedUsers);

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                var token = _cts.Token;
                await QueueWatcherTask(token);
            });

            _intent = new Intent(this, typeof(SmsForwardingService));
            StartService(_intent);

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
            Snackbar.Make(view, "Replace with your own action", BaseTransientBottomBar.LengthLong)
                .SetAction("Action", (View.IOnClickListener)null).Show();
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

        private static async Task<PermissionStatus> RequestSmsPermission()
        {
            try
            {
                var status = await CheckSmsPermission();
                if (status != PermissionStatus.Granted)
                {
                    if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Sms))
                    {
                        //Gonna need that location
                    }

                    status = await CrossPermissions.Current.RequestPermissionAsync<SmsPermission>();
                }

                return status;
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

        private static async Task<PermissionStatus> RequestPhonePermission()
        {
            try
            {
                var status = await CheckPhonePermission();
                if (status != PermissionStatus.Granted)
                {
                    if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Phone))
                    {
                        //Gunna need that location
                    }

                    status = await CrossPermissions.Current.RequestPermissionAsync<PhonePermission>();
                }

                return status;
            }
            catch (Exception ex)
            {
                return PermissionStatus.Unknown;
            }
        }

        public static void EnqueueSms(SmsContent smsContent)
        {
            SmsQueue.Enqueue(smsContent);
            if (_instance?._tvSmsContent != null)
            {
                _instance._tvSmsContent.Text +=
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
                        _telegram?.SendText(AppSettings.AuthorisedUsers, messageContent);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                }

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }

        public static bool SendSms(string address, string text)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(text))
                return false;

            try
            {
                SmsManager.Default?.SendTextMessage(address, null,
                    text, null, null);
            }
            catch (Exception e)
            {
                return false;
            }

            if (_instance?._tvSmsContent != null)
                _instance._tvSmsContent.Text += $"SMS sent to [{address}]:\r\n{text}\r\n\r\n";

            return true;
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
