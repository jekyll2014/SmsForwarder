using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;

using AndroidX.Core.App;

namespace SmsForwarder
{
    [Service(Exported = true, Label = "NotificationForwardService", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
    [IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
    public class NotificationForwardService : NotificationListenerService
    {
        private const string ForegroundChannelId = "1004";
        private const int ServiceId = 200;
        private static readonly Context Context = Application.Context;

        public override void OnCreate()
        {
            base.OnCreate();
            var notification = BuildNotification(Resources?.GetString(Resource.String.app_name) ?? "", "Notification forward started");
            StartForeground(ServiceId, notification);
        }
        public override void OnDestroy()
        {
            StopTheService();
            base.OnDestroy();
        }
        public override IBinder OnBind(Intent intent)
        {
            return base.OnBind(intent);
        }
        public override bool OnUnbind(Intent intent)
        {
            return base.OnUnbind(intent);
        }

        public override void OnNotificationPosted(StatusBarNotification? sbn)
        {
            var notification = sbn?.Notification;
            Bundle extras = notification?.Extras;
            if (extras != null)
            {
                // Get the title from notification
                string title = extras.GetString(Notification.ExtraTitle, "") ?? "";
                MainActivity.ShowToast("============" + title + "======================");

                // Get the content from notification
                string content = extras.GetString(Notification.ExtraText, "") ?? "";
                MainActivity.ShowToast("============" + content + "======================");
            }

            base.OnNotificationPosted(sbn);
        }

        public override void OnNotificationRemoved(StatusBarNotification sbn)
        {
            base.OnNotificationRemoved(sbn);
        }

        private void StopTheService()
        {
            StopForeground(StopForegroundFlags.Detach);
            StopSelf();
        }

        private static Notification BuildNotification(string appName, string notificationText)
        {
            // Building intent
            var intent = new Intent(Context, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.SingleTop);
            intent.PutExtra(appName, notificationText);

            var pendingIntent = PendingIntent.GetActivity(Context, 0, intent, PendingIntentFlags.UpdateCurrent);

            var notifBuilder = new NotificationCompat.Builder(Context, ForegroundChannelId)
                .SetContentTitle(appName)
                .SetContentText(notificationText)
                .SetSmallIcon(Resource.Drawable.ic_mtrl_chip_checked_circle)
                .SetOngoing(true)
                .SetContentIntent(pendingIntent);

            // Building channel if API verion is 26 or above
            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var notificationChannel = new NotificationChannel(ForegroundChannelId, appName, NotificationImportance.High)
                {
                    Importance = NotificationImportance.Default
                };

                notificationChannel.EnableLights(true);
                notificationChannel.EnableVibration(true);
                notificationChannel.SetShowBadge(true);
                //notificationChannel.SetVibrationPattern(new long[] { 100, 200, 300, 400, 500, 400, 300, 200, 400 });

                if (Context.GetSystemService(Context.NotificationService) is NotificationManager notifManager)
                {
                    notifBuilder.SetChannelId(ForegroundChannelId);
                    notifManager.CreateNotificationChannel(notificationChannel);
                }
            }

            return notifBuilder.Build();
        }
    }
}