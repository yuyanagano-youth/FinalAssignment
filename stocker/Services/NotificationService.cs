using NLog;
using stocker.Client;
using stocker.Enums;
using stocker.Models;

namespace stocker.Services;

/// <summary>
/// サーバーへの状態通知を行うサービス
/// オンライン通知およびJOB状態通知を担当する
/// </summary>

public class NotificationService
{
    // ログ出力用
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // API通信を行うクライアント
    private readonly ApiClient _apiclient;

    public NotificationService(ApiClient apiClient)
    {
        _apiclient = apiClient;
    }

    /// <summary>
    /// オンライン通知をサーバーへ送信する
    /// </summary>
    /// <param name="stockerId">設備ID</param>
    public async Task SendOnlineAsync(string stockerId)
    {
        try
        {
            // オンライン通知リクエスト作成
            OnlineRequest? request = new()
            {
                StockerId = stockerId,
                ConnectionStatus = "ONLINE"
            };

            // サーバーへオンライン通知送信
            await _apiclient.PostAsync<object>(
                "/api/stub/equipment/online",
                request);

            Console.WriteLine("オンライン通知送信");
            logger.Info($"オンライン通知送信 StockerId = {stockerId}");
        }
        catch (Exception ex)
        {
            // 通信エラー
            Console.WriteLine($"E-45 オンライン通知失敗:{ex.Message}");
            logger.Error(ex, "E-45 オンライン通知失敗");
            throw;
        }
    }


    /// <summary>
    /// JOB開始(RUNNING)をサーバーへ通知する
    /// </summary>
    /// <param name="stockerId">設備ID</param>
    /// <param name="job">実行中JOB</param>
    public async Task NotifyRunningAsync(string stockerId,JobInfo job)
    {
        // オフライン中は通知を送信しない
        if(AppState.ConnectionStatus != ConnectionStatus.ONLINE)
        {
            return;
        }

        try
        {
            // RUNNING通知リクエスト作成
            JobStatusRequest? request = new()
            {
                StockerId = stockerId,
                JobStatus = "RUNNING",
                CurrentOperationState = "TRAVELING",
                Job = job
            };

            // サーバーへRUNNING通知送信
            await _apiclient.PostAsync(
                "/api/stub/equipment/started",
                request);


            Console.WriteLine($"RUNNING通知送信 JobId={job.JobId}");
            logger.Info($"RUNNING通知送信 JobId = {job.JobId}");

        }
        catch (Exception ex)
        {
            // 通信失敗
            Console.WriteLine($"E-45 RUNNING通知失敗:{ex.Message}");
            logger.Error(ex, $"E-45 RUNNING通知失敗 JobId = {job.JobId}");
        }
    }

    /// <summary>
    /// サーバーへJOB完了通知(COMPLETED)を送信する
    /// </summary>
    public async Task NotifyCompletedAsync(string stockerId,JobInfo job)
    {
        // オフライン中はサーバーと通信できないため通知を送信しない
        if (AppState.ConnectionStatus != ConnectionStatus.ONLINE)
        {
            return;
        }

        try
        {
            // COMPLETED通知リクエスト作成
            JobStatusRequest? request = new()
            {
                StockerId = stockerId,
                JobStatus = "COMPLETED",
                CurrentOperationState = "IDLE",
                Job = job
            };

            // サーバーへ完了通知送信
            await _apiclient.PostAsync<object>(
                "/api/stub/equipment/completed",
                request);

            //Console.WriteLine($"COMPLETED通知 : {job.JobId}");

            Console.WriteLine($"COMPLETED通知送信 JobId={job.JobId}");
            logger.Info($"COMPLETED通知送信 JobId={job.JobId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"E-45 COMPLETED通知失敗:{ex.Message}");
            logger.Error(ex, $"E-45 COMPLETED通知失敗 JobId={job.JobId}");
        }
    }
}
