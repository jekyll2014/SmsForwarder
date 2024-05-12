using Android.App;
using Android.Content;
using Android.Provider;

namespace SmsForwarder
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" })]
    public class SmsBroadcastReceiver : BroadcastReceiver
    {
        private const string IntentAction = "android.provider.Telephony.SMS_RECEIVED";

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != IntentAction)
                return;

            var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);

            for (var i = 0; i < messages?.Length; i++)
            {
                var smsText = messages[i].MessageBody ?? "";
                var smsSender = messages[i].OriginatingAddress ?? "";
                BroadcastSms(new SmsContent(smsText, smsSender));
            }
        }

        private static void BroadcastSms(SmsContent sms)
        {
            MainActivity.EnqueueSms(sms);
        }
    }
}