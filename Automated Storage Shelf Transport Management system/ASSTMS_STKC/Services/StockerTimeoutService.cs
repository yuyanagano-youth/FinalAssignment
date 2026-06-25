using ASSTMS_STKC.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ASSTMS_STKC.Services
{
    public class StockerTimeoutService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StockerTimeoutService> _logger;
        private Timer? _timer;

        // コンストラクタでサービスプロバイダーだけを受け取る
        public StockerTimeoutService(IServiceProvider serviceProvider, ILogger<StockerTimeoutService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        // サーバー起動時に自動で呼び出される
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // 5秒ごとに CheckTimeoutFromDatabase を実行するタイマーをセット
            _timer = new Timer(CheckTimeoutFromDatabase, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        // 5秒ごとに裏で動く処理
        private async void CheckTimeoutFromDatabase(object? state)
        {
            try
            {
                // バックグラウンド処理(Singleton)からリポジトリ(Scoped)を安全に呼び出すためのスコープ作成
                using (var scope = _serviceProvider.CreateScope())
                {
                    // ★TimeoutOfflineStockers が定義されているリポジトリクラスを指定してください
                    // 例として IStockerRepository としています。環境に合わせて変更してください。
                    var stockerRepo = scope.ServiceProvider.GetRequiredService<StockersRepository>();

                    // 30秒通信がない保管棚を一括で 'OFFLINE' に更新する
                    List<string> stockerIds = await stockerRepo.TimeoutOfflineStockers(30);

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

        // サーバー停止時に呼び出される
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
