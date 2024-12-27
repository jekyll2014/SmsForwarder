using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Util;

namespace SmsForwarder
{
    [Service(Exported = true, Label = "NotificationForwardService", Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
    [IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
    public class NotificationForwardService : NotificationListenerService
    {
        public override void OnCreate()
        {
            base.OnCreate();
            Log.Info("start running", "Servico Criado");
        }
        public override void OnDestroy()
        {
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
    }
}