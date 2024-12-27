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

using System;
using System.Collections.Concurrent;
using System.Linq;
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
[assembly: UsesPermission(Android.Manifest.Permission.ReceiveBootCompleted)]
[assembly: UsesPermission(Android.Manifest.Permission.ReadSms)]
[assembly: UsesPermission(Android.Manifest.Permission.ReceiveSms)]
[assembly: UsesPermission(Android.Manifest.Permission.RequestIgnoreBatteryOptimizations)]
[assembly: UsesPermission(Android.Manifest.Permission.SendSms)]

[assembly: UsesPermission(Android.Manifest.Permission.AccessNotificationPolicy)]
[assembly: UsesPermission(Android.Manifest.Permission.BindNotificationListenerService)]
namespace SmsForwarder
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private const int SmsExpiryHours = 24;
        private static MainActivity? _instance;
        private Intent? _intent;
        //private Intent? _intent2;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private static TelegramService? _telegram;
        private static readonly ConcurrentQueue<QueuedSms> SmsQueue = new ConcurrentQueue<QueuedSms>();
        private static BootBroadcastReceiver _bootReceiver = new BootBroadcastReceiver();

        private static TextView? _tvSmsContent;
        private static CheckBox? _smsPermissionCheckBox;
        private static CheckBox? _phonePermissionCheckBox;
        private static CheckBox? _callLogPermissionCheckBox;
        private static CheckBox? _restartOnBootCheckBox;
        private static CheckBox? _lastSmsCheckBox;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            _instance = this;

            _bootReceiver = new BootBroadcastReceiver();
            if (AppSettings.RestartOnBoot)
            {
                try
                {
                    var result = RegisterReceiver(_bootReceiver, new IntentFilter(Intent.ActionBootCompleted));
                }
                catch (Exception ex)
                {
                    ShowToast("Can't register restart on boot");
                    AppSettings.RestartOnBoot = false;
                }
            }
            else
            {
                try
                {
                    UnregisterReceiver(_bootReceiver);
                }
                catch (Exception ex)
                {
                    ShowToast("Can't unregister restart on boot");
                }

                AppSettings.RestartOnBoot = false;
            }

            SmsManager smsManager = null;
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.S)
                {
                    smsManager = (SmsManager)GetSystemService(Java.Lang.Class.FromType(typeof(SmsManager)));
                }
                else
                {
                    smsManager = SmsManager.Default;
                }
            }
            catch (Exception ex)
            {
                MainActivity.ShowToast($"Can't get SmsManager: {ex}");
            }

            _telegram = new TelegramService(AppSettings.TelegramToken, AppSettings.AuthorisedUsers, smsManager);

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

            var phonesText = FindViewById<EditText>(Resource.Id.editTextPhones);
            if (phonesText != null)
            {
                phonesText.Text = AppSettings.IgnoredPhonesString;
                phonesText.FocusChange += (sender, args) =>
                {
                    if (!args.HasFocus && AppSettings.IgnoredPhonesString != phonesText.Text)
                    {
                        AppSettings.IgnoredPhonesString = phonesText.Text;
                        phonesText.Text = AppSettings.IgnoredPhonesString;
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
                Android.Manifest.Permission.ReceiveBootCompleted,
                Android.Manifest.Permission.ReadCallLog,
                Android.Manifest.Permission.RequestIgnoreBatteryOptimizations,
                Android.Manifest.Permission.SendSms,
            }, 0);

            _smsPermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxSmsPermission);
            if (_smsPermissionCheckBox != null)
            {
                var status = CheckSmsPermission();
                _smsPermissionCheckBox.Checked = status == Permission.Granted;
            }

            _phonePermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxPhonePermission);
            if (_phonePermissionCheckBox != null)
            {
                var status = CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState);
                _phonePermissionCheckBox.Checked = status == Permission.Granted;
            }

            _callLogPermissionCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxCallLogPermission);
            if (_callLogPermissionCheckBox != null)
            {
                var status = CheckSelfPermission(Android.Manifest.Permission.ReadCallLog);
                _callLogPermissionCheckBox.Checked = status == Permission.Granted;
            }

            _lastSmsCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxLastSms);

            _restartOnBootCheckBox = FindViewById<CheckBox>(Resource.Id.checkBoxRestartOnBoot);
            if (_restartOnBootCheckBox != null)
            {
                _restartOnBootCheckBox.Checked = AppSettings.RestartOnBoot;
                _restartOnBootCheckBox.CheckedChange += RestartOnBootCheckBox_CheckedChange;
            }

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                var token = _cts.Token;
                await QueueWatcherTask(token);
            });

            _intent = new Intent(this, typeof(SmsForwardingService));
            this.StartForegroundServiceCompat<SmsForwardingService>();

            /*Intent intent = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
            StartActivity(intent);*/
            //_intent2 = new Intent(this, typeof(NotificationForwardService));
            //this.StartForegroundServiceCompat<NotificationForwardService>();

            /*var pushIntent = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
            StartActivity(pushIntent);*/
        }

        private void RestartOnBootCheckBox_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked)
            {
                try
                {
                    var result = RegisterReceiver(_bootReceiver, new IntentFilter(Intent.ActionBootCompleted));
                    AppSettings.RestartOnBoot = true;
                }
                catch (Exception ex)
                {
                    ShowToast("Can't register restart on boot");
                    AppSettings.RestartOnBoot = false;
                }
            }
            else
            {
                try
                {
                    UnregisterReceiver(_bootReceiver);
                    AppSettings.RestartOnBoot = false;
                }
                catch (Exception ex)
                {
                    ShowToast("Can't unregister restart on boot");
                }
            }

            if (_restartOnBootCheckBox != null)
                _restartOnBootCheckBox.Checked = AppSettings.RestartOnBoot;
        }

        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            var id = item.ItemId;
            if (id == Resource.Id.action_exit)
            {
                SmsQueue.Clear();
                StopService(_intent);
                //StopService(_intent2);
                _cts?.Cancel();
                _cts?.Dispose();
                _telegram?.Dispose();

                this.FinishAndRemoveTask();
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());

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

        public Permission CheckSmsPermission()
        {
            var s1 = CheckSelfPermission(Android.Manifest.Permission.ReadSms);
            var s2 = CheckSelfPermission(Android.Manifest.Permission.ReceiveSms);
            var s3 = CheckSelfPermission(Android.Manifest.Permission.SendSms);

            return (s1 == Permission.Granted
                    && s2 == Permission.Granted
                    && s3 == Permission.Granted) ? Permission.Granted : Permission.Denied;
        }

        public Permission CheckPhonePermission()
        {
            return CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState);
        }

        public static void EnqueueSms(SmsContent smsContent)
        {
            if (AppSettings.IgnoredPhones.Contains(smsContent.Sender))
                return;

            foreach (var user in AppSettings.AuthorisedUsers)
            {
                SmsQueue.Enqueue(new QueuedSms(smsContent, user));
            }

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
                        var messageContent = $"SMS from [{sms.Content.Sender}]: {sms.Content.Message}";
                        var sendTask = _telegram?.SendText(sms.UserId, messageContent,
                            _cts?.Token ?? CancellationToken.None);
                        if (sendTask?.Result == null)
                        {
                            if (DateTime.Now.Subtract(sms.CreatedOn).TotalHours < SmsExpiryHours)
                                SmsQueue.Enqueue(sms);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Telegram exception: {ex}\r\n\r\n");

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
                    Toast.MakeText(Android.App.Application.Context, text, ToastLength.Short)?.Show();
                });

            }
            catch (Exception ex)
            {
            }
        }
    }
}
