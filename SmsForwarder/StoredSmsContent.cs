using System;

namespace SmsForwarder
{
    public class StoredSmsContent
    {
        public long Id { get; set; }
        public long ThreadId { get; set; }
        public string Address { get; set; }
        public string Person { get; set; }
        public DateTime Date { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }

        public override string ToString()
        {
            return $"{nameof(Id)}={Id}\r\n" +
                   $"{nameof(ThreadId)}={ThreadId}\r\n" +
                   $"{nameof(Address)}={Address}\r\n" +
                   $"{nameof(Person)}={Person}\r\n" +
                   $"{nameof(Date)}={Date}\r\n" +
                   $"{nameof(Text)}={Text}\r\n" +
                   $"{nameof(Type)}={Type}\r\n" +
                   "====";
        }
    }
}