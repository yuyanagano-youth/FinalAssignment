namespace stocker.Models;

/// <summary>
/// JOB受付結果を返却するレスポンス
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// 設備ID
    /// </summary>
    public string StockerId { get; set; }= string.Empty;

    public string? JobId { get; set; }

    public string? JobStatus { get; set; }

    public string? CurrentOperationState { get; set; }
}
