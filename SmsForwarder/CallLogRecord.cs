using System;

namespace SmsForwarder
{
    public class CallLogRecord
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.MinValue;
        public string Type { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{nameof(Name)}={Name}\r\n" +
                   $"{nameof(Phone)}={Phone}\r\n" +
                   $"{nameof(Date)}={Date}\r\n" +
                   $"{nameof(Duration)}={Duration} sec.\r\n" +
                   $"{nameof(Type)}={Type}\r\n" +
                   "====";
        }
    }
}