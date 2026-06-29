using NLog;
using stocker.Enums;
using stocker.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace stocker.Services;

/// <summary>
/// HTTPリクエストを受信し、JOB実行指示を受け付ける
/// </summary>
public class CommandListener
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // HTTPリクエスト受信用Listener
    private HttpListener? _listener;

    // 受信したコマンドを処理クラスへ振り分ける
    private readonly CommandDispatcher _dispatcher;

    public CommandListener(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// HTTP Listenerを開始する
    /// リクエスト受信処理はバックグラウンドで実行する
    /// </summary>
    public async Task StartListener()
    {
        try
        {
            _listener = new HttpListener();

            // 受信町アドレスを登録
            _listener.Prefixes.Add("http://*:5029/");

            // HTTP受信開始
            _listener.Start();


            logger.Info("Listener開始");
            Console.WriteLine("Listener開始");

            // リクエスト受信ループをバックグラウンドで開始
            _ = Task.Run(async () =>
            {
                await ReceiveRequestAsync();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"E-18 Listener起動失敗 : {ex.Message}");
            logger.Error(ex, "E-18 Listener起動失敗");
            throw;
        }
    }


    /// <summary>
    /// Listener停止し、リソースを解放する
    /// </summary>
    public void StopListener()
    {
        if(_listener == null)
        {
            return;
        }
        // Lostener停止
        _listener?.Stop();

        // 使用したリソースを開放
        _listener?.Close();

        // インスタンスを破棄
        _listener = null;

        logger.Info("Listener停止");
    }



    /// <summary>
    /// 受信JSONを CommandRequestへ変換
    /// </summary>
    /// <param name="requestBody">受信JSON文字列</param>
    /// <returns>CommandRequest</returns>
    public static CommandRequest? ParseRequest(string requestBody)
    {
        // JSONのプロパティ名の大文字・小文字を区別しない
        JsonSerializerOptions? option = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // JSON文字列をCommandRequestへ変換
        return JsonSerializer.Deserialize<CommandRequest>(requestBody, option);

    }


    /// <summary>
    /// レスポンスオブジェクトをJSONへ変換
    /// </summary>
    /// <param name="response">レスポンス情報</param>
    /// <returns>JSON文字列</returns>
    public static string CreateResponse(CommandResponse response)
    {
        // レスポンスオブジェクトをJSONへ変換
        return JsonSerializer.Serialize(response);
    }


    /// <summary>
    /// リクエスト受信ループ
    /// Listener停止まで繰り返し受信を行う。
    /// </summary>
    public async Task ReceiveRequestAsync()
    {
        // Listenerが停止されるまでリクエスト受信を繰り返す
        while(_listener != null && _listener.IsListening)
        {
            try
            {
                // クライアントからのHTTPリクエストを待機
                HttpListenerContext? context = await _listener.GetContextAsync();

                // リクエストボディ読込
                using StreamReader reader = new(context.Request.InputStream);

                // リクエストボディ(JSON)を読み込む
                string requestBody = await reader.ReadToEndAsync();

                // JSONをCommandRequestへ変換
                CommandRequest? request = ParseRequest(requestBody);

                // JSON解析失敗の場合は400(Bad Request)
                if (request == null)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                // JOB情報が存在しない場合は400(Bad Request)
                if (request.Job == null)
                {
                    logger.Info("Job == null");
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                // JOB受付ログ出力
                Console.WriteLine("JOB受信");
                logger.Info($"JOB受信 : {request.Job.JobId}({request.Job.Command})");


                // JOB実行中(TRAVELING)の場合は
                // STOP以外の新規JOBは受け付けずPENDINGを返す
                if (request.Job.Command != null &&
                    request.Job.Command != "STOP" &&
                    AppState.OperationState == OperationState.TRAVELING)
                {
                    // PENDINGレスポンス生成
                    CommandResponse response = new()
                    {
                        StockerId = "STK001",
                        JobId = request.Job.JobId,
                        JobStatus = "PENDING"
                    };

                    string responseJson = CreateResponse(response);

                    context.Response.StatusCode = 200;

                    using StreamWriter? writer = new(context.Response.OutputStream);

                    await writer.WriteAsync(responseJson);

                    context.Response.Close();

                    // 次の受信待ちへ
                    continue;
                }

                // 受信したコマンドをDispatcherへ処理依頼
                await _dispatcher.Dispatch(request.Job);


                // 実行受付成功レスポンス生成
                CommandResponse successResponse;

                if (request.Job.Command == "STOP")
                {
                    // STOPはABORTEDを返却
                    successResponse = new()
                    {
                        StockerId = "STK001",
                        JobId = request.Job.JobId,
                        JobStatus = "ABORTED",
                        CurrentOperationState = OperationState.IDLE.ToString(),
                    };

                }
                else
                {
                    // TRANSFERは受付成功レスポンスを返却
                    successResponse = new()
                    {
                        StockerId = "STK001",
                        JobId = request.Job.JobId,
                    };
                }


                string successJson = CreateResponse(successResponse);

                // JSON文字列をHTTP送信用のバイト配列へ変換
                byte[] buffer = Encoding.UTF8.GetBytes(successJson);

                // HTTP 200(OK)を返却
                context.Response.StatusCode = 200;

                // レスポンス形式をJSONとして通知
                context.Response.ContentType = "application/json";

                // レスポンスサイズを設定
                context.Response.ContentLength64 = buffer.Length;

                // JSONデータをレスポンスへ書き込む
                await context.Response.OutputStream.WriteAsync(
                    buffer,
                    0,
                    buffer.Length);

                // バッファ内のデータをクライアントへ送信
                await context.Response.OutputStream.FlushAsync();

                // レスポンスを終了し接続を閉じる
                context.Response.Close();
            }
            catch (HttpListenerException)
            {
                // Listener停止に伴う正常終了
                break;
            }

            catch (ObjectDisposedException)
            {
                // Listener破棄に伴う正常終了
                break;
            }

            catch (OperationCanceledException)
            {
                // キャンセル要求による正常終了
                break;
            }
            catch (Exception ex)
            {
                // 想定外エラー
                Console.WriteLine($"E-16 予期しない例外 : {ex.Message}");
                logger.Error(ex, "E-16 予期しない例外");
                break;
            }
        }
    }
}
