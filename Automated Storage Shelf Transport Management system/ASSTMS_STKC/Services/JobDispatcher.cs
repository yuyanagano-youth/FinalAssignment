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

            // 3秒ごとに DB の JOB テーブルを監視する
            _timer = new Timer(PollAndDispatchJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            return Task.CompletedTask;
        }

        private async void PollAndDispatchJobs(object? state)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // 各種リポジトリの取得
                    var jobRepo = scope.ServiceProvider.GetRequiredService<StockersRepository>();

                    // 1. 条件の揃ったJOBをDBから取得
                    var pendingJobs = await jobRepo.GetPendingJobsAsync();

                    foreach (var job in pendingJobs)
                    {

                        var jobRecord = new Job(
                                JobId: job.JobId,
                                Command: "TRANSFER", 
                                CarrierId: job.CarrierId,
                                Source: job.SourceLocation,      
                                Destination: job.DestLocation   
                            );

                        // 2. 最上位の送信リクエスト record に包む
                        var requestPayload = new OperationInstructionsReq(
                            HasPendingJob: true,
                            Job: jobRecord
                        );

                        // 3. スタブへPOST送信
                        string stubUrl = "http://172.16.7.19:5029/";
                        var response = await _httpClient.PostAsJsonAsync(stubUrl, requestPayload);

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
                Console.WriteLine($"{ex.Message}");
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
