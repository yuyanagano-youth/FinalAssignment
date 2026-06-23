namespace ASSTMS_STKC.SharedModels.Models
{
    public class JobInfo
    {
        public string JobId { get; set; }
        public string StockerId { get; set; }
        public string CarrierId { get; set; }
        public string SourceLocation { get; set; }
        public string DestLocation { get; set; }
        public string JobStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ClosedAt { get; set; }
    }
}
