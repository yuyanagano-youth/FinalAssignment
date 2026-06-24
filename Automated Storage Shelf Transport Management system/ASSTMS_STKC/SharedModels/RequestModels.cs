using System.Collections;

namespace ASSTMS_STKC.SharedModels
{

    //フロントエンド用

    //ストッカー一覧取得(レスポンス)
    public record Alarms(string errorCode, string message);
    public record StockerIndexRes(string StockerId, string StockerName, string Status, string ConnectionStatus, string OperationState, List<Alarms>? Alarms);
    //JOB生成 or STOP(リクエスト)
    public record JobCreateReq(string Command, string? StockerId, string? CarrierId, string? Source, string? Destination);
    //JOB生成成功(レスポンス)
    public record JobCreateSuccessRes(bool Success, string JobId, string Message);
    //JOB生成エラー(レスポンス)
    public record JobCreateFailureRes(bool Success, string JobId, string ErrorCode, string Message);
    //JOB一覧取得
    public record JobIndexRes(string JobId, string StockerId, string CarrierId, string Source, string Destination, string Status);
    //棚在庫一覧取得(レスポンス)
    public record ShelfStockIndexRes(string ShelfName, string? CarrierId, DateTime? InTime);
    //ログ取得(レスポンス)
    public record LogIndexRes(DateTime TimeStamp, string? Level, string Message);

    //コンソールアプリ用

    //オンライン報告(リクエスト)
    public record OnlineReportReq(string StockerId, string ConnectionStatus);

    //定期ポーリング(リクエスト)
    public record PollingReq(string StockerId, string CurrentOperationState);
    //定期ポーリング(レスポンス)
    public record Job(string? JobId, string? Command, string? CarrierId, string? Source, string? Destination);
    public record PollingRes(bool HasPendingJob, Job Job);

    //動作開始報告(リクエスト)
    public record JobStartReportReq(string? StockerId, string? JobStatus, string? CurrentOperationState, Job Job);

    //動作完了報告(リクエスト)
    public record JobCompleteReportReq(string StockerId, string JobStatus, string CurrentOperationState, Job Job);

    //動作指示(リクエスト)
    public record OperationInstructionsReq(bool HasPendingJob, Job Job);
    //実行可能時動作指示(レスポンス)
    public record OperationInstructionSuccessRes(string StockerId, string JobId);
    //実行失敗時動作指示(レスポンス)
    public record OperationInstructionsFailureRes(string StockerId, string JobId, string JobStatus);

    //停止完了(レスポンス)
    public record StopCompletedRes(string StockerId, string JobId, string JobStatus, string CurrentOperationState);
    //既に停止中(レスポンス)
    public record AlreadyStoppedRes(string StockerId);

    //エラーメッセージ
    public record ErrorRes(string Message);

}
