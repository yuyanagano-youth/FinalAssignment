using NLog;
using stocker.Client;
using stocker.Enums;
using stocker.Models;

namespace stocker.Services;

/// <summary>
/// サーバーへ定期的にポーリング要求を送信する。
/// 実行待ちJOBを取得し、受信したコマンドをDispatcherへ渡す。
/// </summary>
public class PollingService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // API通信を行うクライアント
    private readonly ApiClient _apiClient;

    // サーバーから受信したコマンドを振り分けるクラス
    private readonly CommandDispatcher _dispatcher;

    private readonly Func<Task>? _goOfflineAsync;

    // ポーリング処理の停止制御用
    private CancellationTokenSource? _pollingTokenSource;
    public PollingService(
    ApiClient apiClient,
    CommandDispatcher dispatcher,
    Func<Task>? goOfflineAsync)
    {
        _apiClient = apiClient;
        _dispatcher = dispatcher;
        _goOfflineAsync = goOfflineAsync;
    }


    /// <summary>
    /// ポーリング処理を開始する。
    /// オンライン中は一定間隔でサーバーへ問い合わせを行う。
    /// </summary>
    public Task StartPolling()
    {
        // キャンセル制御用オブジェクト生成
        _pollingTokenSource = new CancellationTokenSource();

        // バックグラウンドでポーリング開始
        _ = Task.Run(async () =>
        {
            // 停止指示を受けるまでループ
            while (!_pollingTokenSource.Token.IsCancellationRequested)
            {
                // ポーリング実行
                await PollingAsync("STK001", AppState.OperationState.ToString());

                // 次回ポーリングまで5秒待機
                await Task.Delay(TimeSpan.FromSeconds(5),_pollingTokenSource.Token);
            }

        });

        Console.WriteLine("Polling開始");
        logger.Info("Polling開始");

        return Task.CompletedTask;
    }

    /// <summary>
    /// ポーリング処理を停止する。
    /// </summary>
    public void StopPolling()
    {
        // 実行中のポーリングループへ停止通知
        _pollingTokenSource?.Cancel();
    }


    /// <summary>
    /// サーバーへポーリング要求を送信する。
    /// 実行待ちJOBが存在する場合はDispatcherへ処理を依頼する。
    /// </summary>
    /// <param name="stockerId">設備ID</param>
    /// <param name="operationState">現在の設備状態</param>
    /// <returns>ポーリング応答</returns>
    public async Task<PollingResponse?> PollingAsync(string stockerId, string operationState)
    {
        try
        {
            // オフライン状態ではポーリングしない
            if (AppState.ConnectionStatus != ConnectionStatus.ONLINE)
            {
                return null;
            }

            // JOB実行中は新規JOBを受け付けないためポーリングを行わない
            if (AppState.OperationState != OperationState.IDLE)
            {
                return null;
            }

            PollingRequest request = new()
            {
                StockerId = stockerId,
                CurrentOperationState = operationState
            };


            // サーバーへポーリング要求を送信
            PollingResponse? response =
               await _apiClient.PostAsync<PollingRequest, PollingResponse>("api/stub/equipment/polling", request);

            // レスポンスなし
            if (response == null) 
            {
                Console.WriteLine("PollingResponseがnull");
                logger.Warn("PollingResponseがnull");
                return null; 
            }
            // JOBなし
            if (!response.HasPendingJob)
            {
                Console.WriteLine("実行待ちJOBなし");
                logger.Info("実行待ちJOBなし");
                return response; 
            } 
            // JOB情報なし
            if (response.Job == null) 
            {
                Console.WriteLine("JOB情報が取得できません");
                logger.Warn("JOB情報が取得できません");
                return response;
            }

            Console.WriteLine("JOB情報を取得しました");
            // 取得したJOBをCommandDispatcherへ渡して実行を依頼
            await _dispatcher.Dispatch( response.Job);
            
            return response;
  
        }
        catch (HttpRequestException ex)
        {
            logger.Error(ex, "サーバーとの通信に失敗");

            if (AppState.ConnectionStatus == ConnectionStatus.ONLINE && _goOfflineAsync != null)
            {
                await _goOfflineAsync();
            }

            return null;
        }
        catch (TaskCanceledException ex)
        {
            logger.Error(ex, "通信タイムアウト");

            if (AppState.ConnectionStatus == ConnectionStatus.ONLINE && _goOfflineAsync != null)
            {
                await _goOfflineAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Polling処理で予期しない例外");
            return null;
        }
    }
}

