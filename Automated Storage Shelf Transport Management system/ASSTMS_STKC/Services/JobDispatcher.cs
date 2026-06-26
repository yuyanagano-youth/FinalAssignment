using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;

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
            // アプリ起動時に3秒ごとに DB を監視するタイマを設定
            _timer = new Timer(PollAndDispatchJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
            return Task.CompletedTask;
        }

        private async void PollAndDispatchJobs(object? state)
        {
            try
            {
                //1回の定期処理ごとに、新しくスコープを作って使い終わったら捨てる
                using (var scope = _serviceProvider.CreateScope())
                {
                    var stockerRepository = scope.ServiceProvider.GetRequiredService<StockersRepository>();

                    JobInfo? job = await stockerRepository.GetPendingJobsAsync();

                    if (job == null)
                    {
                        return;
                    }

                    var jobRecord = new Job(
                                JobId: job.JobId,
                                Command: "TRANSFER", 
                                CarrierId: job.CarrierId,
                                Source: job.SourceLocation,      
                                Destination: job.DestLocation   
                            );

                        var requestPayload = new OperationInstructionsReq(
                            HasPendingJob: true,
                            Job: jobRecord
                        );

                        string stubUrl = "http://172.16.7.19:5029/";
                        var response = await _httpClient.PostAsJsonAsync(stubUrl, requestPayload);

                        //if (response.IsSuccessStatusCode)
                        //{
                        //    Console.WriteLine($"[JOB送信] スタブへの送信成功。 (JobID: {job.JobId})");
                        //}
                        //else
                        //{
                        //    Console.WriteLine($"[JOB送信エラー] スタブが拒否しました。 StatusCode: {response.StatusCode}");
                        //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //アプリ停止時にタイマをストップする
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            //タイマーの破棄
            _timer?.Dispose();
        }
    }
}
