using stocker.Client;
using stocker.Enums;
using stocker.Models;
using stocker.Services;
using System;
using NLog;


namespace stocker;

public class Program
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// アプリケーションのエントリーポイント
    /// </summary>
    public static async Task Main(string[] args)
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

            var commandListener = new CommandListener(commandDispatcher);

            // アプリケーション状態を初期化
            onlineManager.Initialize();

            // 操作メニュー表示
            Console.WriteLine();
            Console.WriteLine("1.オンライン / 2.オフライン");
            Console.Write("選択 : ");

            // ユーザー入力を受け付ける
            while (true)
            {
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":

                        // オンライン化
                        await onlineManager.GoOnlineAsync("STK001");

                        // HTTP Listener開始
                        await commandListener.StartListener();

                        break;

                    case "2":

                        // HTTP Listener停止
                        commandListener.StopListener();

                        // オフライン化
                        await onlineManager.GoOfflineAsync();

                        break;

                    case "0":

                        // Listener停止
                        commandListener.StopListener();

                        // オフライン化
                        await onlineManager.GoOfflineAsync();
                        // アプリ終了
                        Console.WriteLine("アプリ終了");
                        logger.Info("アプリ終了");
                        return;

                        // メニュー以外が入力された
                    default:
                        Console.WriteLine("入力エラー");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            // 想定外エラー
            logger.Error(ex,"予期しないエラー");
            Console.WriteLine($"エラー : {ex.Message}");
        }

    }

}
