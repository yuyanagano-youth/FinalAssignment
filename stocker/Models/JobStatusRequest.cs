namespace stocker.Models;

public class JobStatusRequest
{
    public string StockerId { get; set; } = string.Empty;

    public string JobStatus {  get; set; } = string.Empty;

    public string CurrentOperationState {  get; set; } = string.Empty;

    public JobInfo? Job {  get; set; }
}
