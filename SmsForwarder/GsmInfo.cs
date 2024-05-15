namespace SmsForwarder
{
    internal class GsmInfo
    {
        public string CarrierName { get; set; } = string.Empty;
        public int SignalStrength { get; set; } = -1;
        public string NetworkType { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{nameof(CarrierName)}: {CarrierName}\r\n" +
                   $"{nameof(SignalStrength)}: {SignalStrength}\r\n" +
                   $"{nameof(NetworkType)}: {NetworkType}";
        }
    }
}