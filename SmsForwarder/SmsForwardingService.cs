using Android.App;
using Android.Content;
using Android.OS;

using AndroidX.Core.App;

namespace SmsForwarder
{
    [Service(Exported = true)]
    public class SmsForwardingService : Service
    {
        private const string ForegroundChannelId = "1003";
        private const int ServiceId = 100;
        private static readonly Context Context = Application.Context;

        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var notification = BuildNotification(Resources?.GetString(Resource.String.app_name) ?? "", "SMS forward started");
            StartForeground(ServiceId, notification);

            return StartCommandResult.NotSticky;
        }

        public override void OnDestroy()
        {
            StopTheService();
            base.OnDestroy();
        }

        public override bool StopService(Intent? name)
        {
            StopTheService();
            return base.StopService(name);
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