using System;

namespace SmsForwarder
{
    public class StoredSmsRecord
    {
        public long Id { get; set; } = 0;
        public long ThreadId { get; set; } = 0;
        public string Address { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.MinValue;
        public string Subject { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{nameof(Id)}={Id}\r\n" +
                   $"{nameof(ThreadId)}={ThreadId}\r\n" +
                   $"{nameof(Address)}={Address}\r\n" +
                   $"{nameof(Name)}={Name}\r\n" +
                   $"{nameof(Phone)}={Phone}\r\n" +
                   $"{nameof(Date)}={Date}\r\n" +
                   $"{nameof(Subject)}={Subject}\r\n" +
                   $"{nameof(Text)}={Text}\r\n" +
                   $"{nameof(Type)}={Type}\r\n" +
                   "====";
        }
    }
}