namespace ASSTMS_STKC.SharedModels.Models
{
    public class StockInfo
    {
        public string StockerId { get; set; }
        public string StockerName { get; set; }
        public string Status { get; set; }
        public string ConnectionStatus { get; set; }
        public string OperationState { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public Alarms alarms { get; set; }
    }
}
