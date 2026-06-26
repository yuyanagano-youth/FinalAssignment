using stocker.Enums;
using stocker.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using NLog;

namespace stocker.Services;

/// <summary>
/// オンライン・オフライン状態を管理する。
/// オンライン通知、ポーリング開始・停止、JOB中断を制御する。
/// </summary>
public class OnlineManager
{
    // ログ出力用
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // オンライン通知サービス
    private readonly NotificationService _notificationService;

    // ポーリングサービス
    private readonly PollingService _pollingService;

    // JOB実行サービス
    private readonly JobService _jobService;

    public OnlineManager(NotificationService notificationService,
        PollingService pollingService,
        JobService jobService)
    {
        _notificationService = notificationService;
        _pollingService = pollingService;
        _jobService = jobService;
    }

    /// <summary>
    /// アプリケーション状態を初期化する。
    /// 起動時はオフライン・待機状態とする。
    /// </summary>
    public void Initialize()
    {
        // 接続状態をオフラインへ初期化
        AppState.ConnectionStatus = ConnectionStatus.OFFLINE;

        // 設備状態を待機中へ初期化
        AppState.OperationState = OperationState.IDLE;

        // 実行中JOB情報をクリア
        AppState.CurrentJobId = null;
        AppState.AcceptedJobId = null;

        logger.Info("初期化完了");

        return;
    }


    /// <summary>
    /// オンライン状態へ切り替える。
    /// オンライン通知送信後、ポーリングを開始する。
    /// </summary>
    /// <param name="stockerId">設備ID</param>
    public async Task GoOnlineAsync(string stockerId)
    {
        // すでにオンラインなら処理しない
        if(AppState.ConnectionStatus == ConnectionStatus.ONLINE)
        {
            return;
        }

        // 接続状態をオンラインに変更
        AppState.ConnectionStatus = ConnectionStatus.ONLINE;

        Console.WriteLine("オンライン化開始");
        logger.Info("オンライン化開始");

        // 設備状態を待機中に設定
        AppState.OperationState = OperationState.IDLE;

        // サーバーへオンライン通知を送信
        await _notificationService.SendOnlineAsync(stockerId);

        // ポーリング開始
        await _pollingService.StartPolling();

        Console.WriteLine("オンライン化完了\n");
        logger.Info("オンライン化完了");

    }

    /// <summary>
    /// オフライン状態へ切り替える。
    /// ポーリング停止および実行中JOBを中断する。
    /// </summary>
    public async Task GoOfflineAsync()
    {
        // 既にオフラインなら処理しない
        if (AppState.ConnectionStatus == ConnectionStatus.OFFLINE)
        {
            return;
        }

        // ポーリング停止
        _pollingService.StopPolling();

        // 搬送中のJOBがある場合は、オフライン化に伴いキャンセルする
        if(AppState.OperationState == OperationState.TRAVELING)
        {
            _jobService.CancelCurrentJob();
        }


        // 実行中JOB情報をクリア
        AppState.CurrentJobId = null;
        AppState.AcceptedJobId = null;
        AppState.CancellationTokenSource = null;

        // 状態をオフライン・待機中へ戻す
        AppState.OperationState = OperationState.IDLE;
        AppState.ConnectionStatus = ConnectionStatus.OFFLINE;

        Console.WriteLine("オフライン化完了");
        logger.Info("オフライン化完了");
    }
}
