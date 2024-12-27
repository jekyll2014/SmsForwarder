using Android.App;
using Android.Content;

namespace SmsForwarder
{
    [BroadcastReceiver(Label = "BootBroadcastReceiver", DirectBootAware = true, Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted }, Priority = (int)IntentFilterPriority.HighPriority)]
    public class BootBroadcastReceiver : BroadcastReceiver
    {
        private const string IntentAction = Intent.ActionBootCompleted;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != IntentAction || !AppSettings.RestartOnBoot)
                return;

            var newIntent = new Intent(context, typeof(MainActivity));
            newIntent.AddFlags(ActivityFlags.NewTask);
            //newIntent.SetFlags(ActivityFlags.NewTask);            
            context.StartActivity(newIntent);
            //context.StartForegroundService(newIntent);
        }
    }
}