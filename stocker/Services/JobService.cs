using NLog;
using stocker.Client;
using stocker.Enums;
using stocker.Models;
using stocker.Services;


namespace stocker.Services;


/// <summary>
/// JOBの実行・停止を管理する。
/// 実行状態の更新、通知送信、キャンセル制御を行う。
/// </summary>
public class JobService
{
    // ログ出力用
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // ステータス通知サービス
    private readonly NotificationService _notificationService;

    public JobService(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }


    /// <summary>
    /// JOBを実行する。
    /// 実行開始から完了・中断までの状態管理を行う。
    /// </summary>
    /// <param name="job">実行するJOB情報</param>
    public async Task ExecuteJobAsync(JobInfo job)
    {
        try
        {

            // JOB開始
            Console.WriteLine($"JOB開始 : {job.JobId}");
            logger.Info($"JOB開始 : {job.JobId}");

            // 設備状態を搬送中へ変更
            AppState.OperationState = OperationState.TRAVELING;

            // 実行中JOBを保持
            AppState.CurrentJobId = job.JobId;

            // STOP要求を受け付けるためのCancellationToken生成
            AppState.CancellationTokenSource = new CancellationTokenSource();

            // RUNNING通知送信
            await _notificationService.NotifyRunningAsync("STK001", job);

            // 輸送処理実行
            await ExecuteTransferAsync(job);

            // COMPLETED通知送信
            await _notificationService.NotifyCompletedAsync("STK001", job);

            // 実行中JOB情報をクリア
            AppState.CurrentJobId = null;
            AppState.AcceptedJobId = null;

            // 設備状態を待機中へ戻す
            AppState.OperationState = OperationState.IDLE;

            Console.WriteLine($"JOB完了:{job.JobId}\n");
            logger.Info($"JOB完了 : {job.JobId}");
        }
        catch(OperationCanceledException)
        {
            // STOP要求によりJOBが中断された

            // 状態を初期化
            AppState.CurrentJobId = null;
            AppState.AcceptedJobId = null;
            AppState.OperationState = OperationState.IDLE;


            Console.WriteLine($"JOB中断 : {job.JobId}\n");
            logger.Info($"JOB中断 : {job.JobId}");
        }
        catch (Exception ex)
        {
            // 想定外エラーでも状態を初期化

            Console.WriteLine(ex.Message);

            AppState.CurrentJobId= null;
            AppState.AcceptedJobId = null;
            AppState.OperationState = OperationState.IDLE;

            Console.WriteLine($"JOB異常終了 : {job.JobId}");
            logger.Error(ex, $"JOB異常終了 : {job.JobId}");
        }


    }


    /// <summary>
    /// 搬送処理を実行する。
    /// 学習用のため20秒待機で搬送を表現する。
    /// </summary>
    /// <param name="job">搬送対象JOB</param>
    public async Task ExecuteTransferAsync(JobInfo job)
    {
        // 輸送開始
        Console.WriteLine("搬送開始\n");
        logger.Info($"搬送開始 : {job.JobId}");

        // 輸送時間を疑似的に再現
        // STOP要求があればキャンセルされる
        await Task.Delay(TimeSpan.FromSeconds(20),AppState.CancellationTokenSource!.Token);

        // 輸送完了
        Console.WriteLine("搬送完了");
        logger.Info($"搬送完了 : {job.JobId}");
    }


    /// <summary>
    /// 実行中JOBを停止する。
    /// </summary>
    /// <param name="jobId">停止対象JOBID</param>
    public async Task StopJob(string jobId)
    {
        // STOP対象JOBが現在実行中か確認
        if (AppState.OperationState != OperationState.TRAVELING)
        {
            Console.WriteLine("実行中JOBなし");
            logger.Warn("実行中JOBなし");
            return;
        }

        // 実行中JOBが存在するか確認
        if (AppState.CurrentJobId != jobId)
        {
            Console.WriteLine("JOB不一致");
            logger.Warn($"JOB不一致 : {jobId}");
            return;
        }

        // 実行中JOBへキャンセル要求
        CancelCurrentJob();

        Console.WriteLine($"JOB停止 : {jobId}");
        logger.Info($"JOB停止 : {jobId}");
    }


    /// <summary>
    /// 実行中JOBへキャンセル要求を送信する。
    /// </summary>
    public void CancelCurrentJob()
    {
        // CancellationTokenへキャンセル通知
        AppState.CancellationTokenSource?.Cancel();
        Console.WriteLine("Cancel要求");
        logger.Info("Cancel要求");
    }

    /// <summary>
    /// 設備が搬送中か判定する。
    /// </summary>
    /// <returns>
    /// true : 搬送中
    /// false : 待機中
    /// </returns>
    public bool IsRunning()
    {
        return AppState.OperationState == OperationState.TRAVELING;
    }
}
