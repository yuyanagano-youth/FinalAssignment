namespace ASSTMS_STKC.SharedModels.Models
{
    public class DeviceLog
    {
        public long LogId { get; set; }
        public DateTime Timestamp { get; set; }
        public string StockerId { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
    }

}
