namespace SmsForwarder
{
    public class SmsContent
    {
        public string Message { get; set; }
        public string Sender { get; set; }

        public SmsContent(string message, string sender)
        {
            Message = message;
            Sender = sender;
        }
    }
}