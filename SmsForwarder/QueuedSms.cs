using System;

namespace SmsForwarder
{
    class QueuedSms
    {
        public readonly SmsContent Content;
        public readonly DateTime CreatedOn = DateTime.Now;
        public readonly long UserId;

        public QueuedSms(SmsContent content, long userId)
        {
            Content = content;
            UserId = userId;
        }
    }
}