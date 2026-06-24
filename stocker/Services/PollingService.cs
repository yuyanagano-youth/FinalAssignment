using System;
using System.Collections.Generic;
using System.Text;
using stocker.Client;
using stocker.Models;
using stocker.Enums;

namespace stocker.Services;


public class PollingService
{
    // API通信を行うクライアント
    private readonly ApiClient _apiClient;

    // サーバーから受信したコマンドを振り分けるクラス
    private readonly CommandDispatcher _dispatcher;

    // ポーリング処理の停止制御用
    private CancellationTokenSource? _pollingTokenSource;
    public PollingService(
    ApiClient apiClient,
    CommandDispatcher dispatcher)
    {
        _apiClient = apiClient;
        _dispatcher = dispatcher;
    }


    // ポーリング処理の開始
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
                await Task.Delay(TimeSpan.FromSeconds(10),_pollingTokenSource.Token);
            }

        });

        return Task.CompletedTask;
    }

    // ポーリング処理停止
    public void StopPolling()
    {
        // 実行中のポーリングループへ停止通知
        _pollingTokenSource?.Cancel();
    }


    // サーバーへポーリング要求を送信する
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

            Console.WriteLine($"StockerId={request.StockerId}");
            Console.WriteLine($"CurrentOperationState={request.CurrentOperationState}");


            //API送信用リクエスト生成
            PollingResponse? response =
               await _apiClient.PostAsync<PollingRequest, PollingResponse>("http://172.16.7.6:5028/api/stub/equipment/polling", request);

            // レスポンスなし
            if (response == null) { Console.WriteLine( "PollingResponseがnull"); return null; }
            // JOBなし
            if (!response.HasPendingJob) { Console.WriteLine( "実行待ちJOBなし"); return response; } 
            // JOB情報なし
            if (response.Job == null) { Console.WriteLine( "JOB情報が取得できません"); return response; } 
            Console.WriteLine( $"JOB受信 : {response.Job.JobId}");
            
            Console.WriteLine( $"Command : {response.Job.Command}");
            // CommandDispatcherへ渡す
            await _dispatcher.Dispatch( response.Job); return response;
  
        }
        catch (Exception ex)
        {
            // 通信エラーや予期しない例外をログ出力
            Console.WriteLine(ex.Message);
            return null;
        }
    }
}

