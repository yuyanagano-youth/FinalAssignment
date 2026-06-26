using NLog;
using stocker.Client;
using stocker.Enums;
using stocker.Models;
using stocker.Services;

namespace stocker.Services;

/// <summary>
/// サーバーから受信したコマンドを判別し、対応する処理へ振り分ける
/// </summary>
public class CommandDispatcher
{
    // ログ出力用
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // JOB実行サービス
    private readonly JobService _jobService;

    public CommandDispatcher(JobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// コマンド種別に応じて処理を振り分ける
    /// </summary>
    /// <param name="job">受信したJOB情報</param>
    public Task Dispatch(JobInfo job)
    {
        Console.WriteLine($"コマンド受信 : JobId={job.JobId}, Command={job.Command}");
        logger.Info($"コマンド受信 JobId={job.JobId}, Command={job.Command}");

        // TRANSFERコマンド
        if (job.Command == "TRANSFER")
        {
            // JOB実行はバックグラウンドで開始
            _ = Task.Run(() => HandleTransfer(job));
        }
        // STOPコマンド
        else if (job.Command == "STOP")
        {
            _ = HandleStop(job);
        }
        // 未対応コマンド
        else
        {
            logger.Warn($"未対応コマンド : {job.Command}");
            Console.WriteLine($"未対応コマンド : {job.Command}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// TRANSFERコマンドを処理する
    /// </summary>
    /// <param name="job">実行対象JOB</param>
    public async Task HandleTransfer(JobInfo job)
    {
        // JobIdが存在しない場合は処理しない
        if (job.JobId == null)
        {
            Console.WriteLine("jobIdなし");
            logger.Warn("jobIdなし");
            return;
        }

        // 同じJOBを重複実行しないようチェック
        if (AppState.AcceptedJobId == job.JobId)
        {
            Console.WriteLine("重複JOBがあります");
            logger.Warn($"重複JOB受信 : {job.JobId}");
            return;
        }

        logger.Info($"TRANSFER受付 : {job.JobId}");

        // 現在受け付けたJOBとして保存
        AppState.AcceptedJobId = job.JobId;

        // JOB実行開始
        await _jobService.ExecuteJobAsync(job);
    }

    /// <summary>
    /// STOPコマンドを処理する
    /// </summary>
    /// <param name="job">停止対象JOB</param>
    public async Task HandleStop(JobInfo job)
    {
        // JobIdが存在しない場合は処理しない
        if (job.JobId == null)
        {
            Console.WriteLine("JobIdなし");
            logger.Warn("JobIdなし");
            return;
        }

        Console.WriteLine($"STOP受付 : {job.JobId}");
        logger.Info($"STOP受付 : {job.JobId}");

        // 実行中JOBの停止を依頼
        await _jobService.StopJob(job.JobId);
    }
}