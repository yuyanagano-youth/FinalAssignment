using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASSTMS_STKC.Services
{
    public class JobDispatcher : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        private Timer? _timer;

        public JobDispatcher(IServiceProvider serviceProvider, HttpClient httpClient)
        {
            _serviceProvider = serviceProvider;
            _httpClient = httpClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("JobDispatchService (JOB自動送信サービス) が起動しました。");

            // 3秒ごとに DB の JOB テーブルを監視する
            _timer = new Timer(PollAndDispatchJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
            return Task.CompletedTask;
        }

        private async void PollAndDispatchJobs(object? state)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // 各種リポジトリの取得（実際のクラス名に書き換えてください）
                    var jobRepo = scope.ServiceProvider.GetRequiredService<StockersRepository>();

                    // 1. 条件の揃ったJOBをDBから取得
                    var pendingJobs = await jobRepo.GetPendingJobsAsync();

                    foreach (var job in pendingJobs)
                    {
                        Console.WriteLine($"[JOB送信] 条件一致のJOBを発見しました。 (JobID: {job.JobId})");

                        var jobRecord = new Job(
                                JobId: job.JobId,
                                Command: "TRANSFER", // 必要に応じて jobInfo.Something からマッピングしてください
                                CarrierId: job.CarrierId,
                                Source: job.SourceLocation,      // SourceLocation ➔ Source
                                Destination: job.DestLocation   // DestLocation ➔ Destination
                            );

                        //3. 最上位の送信リクエスト record に包む
                        var requestPayload = new OperationInstructionsReq(
                            HasPendingJob: true,
                            Job: jobRecord
                           // job
                        );

                        // 2. スタブへPOST送信
                        // ※ポート番号や送信データの形式（request）はスタブの仕様に合わせてください
                        string stubUrl = "http://localhost:5029/";
                        var response = await _httpClient.PostAsJsonAsync(stubUrl, job);

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[JOB送信] スタブへの送信成功。 (JobID: {job.JobId})");
                        }
                        else
                        {
                            Console.WriteLine($"[JOB送信エラー] スタブが拒否しました。 StatusCode: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JOB監視エラー] {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
