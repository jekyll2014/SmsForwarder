using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Phone;
using Android.OS;

namespace SmsForwarder
{
    [Service(Exported = true)]
    public class SmsForwardingService : Service
    {
        private SmsRetrieverClient? _client;

        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            // From shared code or in your PCL
            // Get an instance of SmsRetrieverClient, used to start listening for a matching SMS message.
            _client = SmsRetriever.GetClient(this.ApplicationContext);

            // Starts SmsRetriever, which waits for ONE matching SMS message until timeout
            // (5 minutes). The matching SMS message will be sent via a Broadcast Intent with
            // action SmsRetriever#SMS_RETRIEVED_ACTION.
            var task = _client.StartSmsRetriever();

            // You could also Listen for success/failure of StartSmsRetriever
            task.AddOnSuccessListener(new SuccessListener());
            task.AddOnFailureListener(new FailureListener());

            return StartCommandResult.NotSticky;
        }
    }
}