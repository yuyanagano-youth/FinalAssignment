using stocker.Client;
using stocker.Enums;
using stocker.Models;
using stocker.Services;

namespace stocker;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("アプリ開始");

    try
        {

            // 依存オブジェクト生成
            var apiClient = new ApiClient();

            var notificationService =
                new NotificationService(apiClient);

            var jobService =
                new JobService(notificationService);

            var commandDispatcher =
                new CommandDispatcher(jobService);

            var pollingService =
                new PollingService(
                    apiClient,
                    commandDispatcher);

            var onlineManager = new OnlineManager(
                notificationService,
                pollingService,
                jobService);

            // 初期設定
            onlineManager.Initialize();

            Console.WriteLine();
            Console.WriteLine("1.オンライン / 2.オフライン");
            Console.Write("選択 : ");

            while (true)
            {
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        await onlineManager.GoOnlineAsync("STK001");
                        break;

                    case "2":
                        await onlineManager.GoOfflineAsync();
                        break;

                    case "0":
                        await onlineManager.GoOfflineAsync();
                        Console.WriteLine("アプリ終了");
                        return;

                    default:
                        Console.WriteLine("入力エラー");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー : {ex.Message}");
        }

        Console.WriteLine("アプリ終了");
    }

}
