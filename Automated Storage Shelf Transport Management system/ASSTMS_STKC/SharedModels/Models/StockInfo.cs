namespace ASSTMS_STKC.Models
{
    public class StockInfo
    {
        public string StockerName { get; set; }
        public string Status { get; set; }
        public string ConnectionStatus { get; set; }
        public string OperationState { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }
}
