using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.Http.HttpResults;
using stocker.Enums;
using stocker.Models;
using stocker.Services;
using System.Net;
using System.Text.Json;

namespace stocker.Services;

/// <summary>
/// HTTPリクエストを受信し、JOB実行指示を受け付ける
/// </summary>
public class CommandListener
{
    // HTTP受信用Listener
    private HttpListener? _listener;

    // JOB実行を振り分けるDispatcher
    private readonly CommandDispatcher _dispatcher;

    public CommandListener(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }



    /// <summary>
    /// Listener開始
    /// </summary>
    public async Task StartListener()
    {
        try
        {
            _listener = new HttpListener();
            // 受信町アドレス登録

            //string prefix = "http//172.16.7.6:5028/api/stub/equipment/instruction";

            //Console.WriteLine(prefix);


            _listener.Prefixes.Add("http://*:5028/");

            _listener.Start();

            Console.WriteLine("Listener開始");

            // バックグラウンドで受信処理開始
            _ = Task.Run(async () =>
            {
                await ReceiveRequestAsync();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"E-18 Listener起動失敗 : {ex.Message}");
            throw;
        }
    }


    /// <summary>
    /// Listener停止
    /// </summary>
    public void StopListener()
    {
        _listener?.Stop();

        _listener?.Close();

        _listener = null;

        Console.WriteLine("Listener停止");
    }



    /// <summary>
    /// 受信JSONを CommandRequestへ変換
    /// </summary>
    /// <param name="requestBody">受信JSON文字列</param>
    /// <returns>CommandRequest</returns>
    public CommandRequest? ParseRequest(string requestBody)
    {
        return JsonSerializer.Deserialize<CommandRequest>(requestBody);

    }


    /// <summary>
    /// レスポンスオブジェクトをJSONへ変換
    /// </summary>
    /// <param name="response">レスポンス情報</param>
    /// <returns>JSON文字列</returns>
    public string CreateResponse(CommandResponse response)
    {
        return JsonSerializer.Serialize(response);
    }


    /// <summary>
    /// リクエスト受信ループ
    /// Listener停止まで繰り返し受信を行う。
    /// </summary>
    public async Task ReceiveRequestAsync()
    {
        while(_listener != null && _listener.IsListening)
        {
            try
            {
                // HTTPリクエスト受信待ち
                var context = await _listener.GetContextAsync();


                // リクエストボディ読込
                using var reader = new StreamReader(context.Request.InputStream);

                string requestBody = await reader.ReadToEndAsync();

                // JSON → CommandRequest変換
                CommandRequest? request = ParseRequest(requestBody);


                // リクエスト解析失敗
                if (request == null)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    return;
                }

                // JOB情報未設定
                if (request.Job == null)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    return;
                }


                // JOB実行中の場合
                // 新規JOBは受け付けずPENDING返却
                if (AppState.OperationState == OperationState.RUNNING)
                {
                    CommandResponse response = new()
                    {
                        StockerId = "STK001",
                        JobId = request.Job.JobId,
                        JobStatus = "PENDING"
                    };

                    string responseJson = CreateResponse(response);

                    context.Response.StatusCode = 200;

                    using var writer = new StreamWriter(context.Response.OutputStream);

                    await writer.WriteAsync(responseJson);

                    context.Response.Close();

                    // 次の受信待ちへ
                    continue;
                }

                // IDLEならJOB実行依頼
                await _dispatcher.Dispatch(request.Job);


                // 実行受付成功レスポンス生成
                CommandResponse successResponse = new()
                {
                    StockerId = "STK001",
                    JobId = request.Job.JobId
                };

                string successJson =
                    CreateResponse(successResponse);

                context.Response.StatusCode = 200;

                using var successWriter =
                    new StreamWriter(context.Response.OutputStream);

                await successWriter.WriteAsync(successJson);

                context.Response.Close();
            }
            catch (Exception ex)
            {
                // 想定外エラー
                Console.WriteLine($"E-16 予期しない例外 : {ex.Message}");
            }
        }
    }
}
