using ASSTMS_STKC.Data.Repositories;

namespace ASSTMS_STKC.Services
{
    public class StockerTimeoutService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StockerTimeoutService> _logger;
        private Timer? _timer;

        public StockerTimeoutService(IServiceProvider serviceProvider, ILogger<StockerTimeoutService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // アプリ起動時に5秒ごとに CheckTimeoutFromDatabase を実行するタイマーをセット
            _timer = new Timer(CheckTimeoutFromDatabase, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private async void CheckTimeoutFromDatabase(object? state)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var stockerRepository = scope.ServiceProvider.GetRequiredService<StockersRepository>();

                    // 30秒通信がない保管棚をOFFLINEに更新する
                    List<string> stockerIds = await stockerRepository.TimeoutOfflineStockers(30);

                    foreach (var id in stockerIds)
                    {
                        _logger.LogWarning("[STUB] ストッカーオフライン StockerId={StockerId}",id);
                        var logRepo = scope.ServiceProvider.GetRequiredService<LogRepository>();
                        await logRepo.InsertLog(id, "WARN", $"オフライン移行");
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
