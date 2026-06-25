using NLog.Targets;
using stocker.Client;
using stocker.Enums;
using stocker.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace stocker.Services;

/// <summary>
/// サーバーへの状態通知を行うサービス
/// オンライン通知およびJOB状態通知を担当する
/// </summary>

public class NotificationService
{
    // API通信クライアント
    private readonly ApiClient _apiclient;

    public NotificationService(ApiClient apiClient)
    {
        _apiclient = apiClient;
    }

    // オンライン通知送信処理
    public async Task SendOnlineAsync(string stockerId)
    {
        try
        {
            // オンライン通知リクエスト作成
            OnlineRequest? request = new()
            {
                StockerId = stockerId,
                ConnectionStatus = "ONLINE"
            };

            // サーバーへオンライン通知送信
            await _apiclient.PostAsync<object>(
                "/api/stub/equipment/online",
                request);
            //Console.WriteLine($"ONLINE通知 : {stockerId}");

            Console.WriteLine("オンライン通知送信");
        }
        catch (Exception ex)
        {
            // 通信失敗
            Console.WriteLine($"E-45 オンライン通知失敗:{ex.Message}");
            throw;
        }
    }


    // JOB開始通知送信
    public async Task NotifyRunningAsync(string stockerId,JobInfo job)
    {
        // オフライン中は通知しない
        if(AppState.ConnectionStatus != ConnectionStatus.ONLINE)
        {
            return;
        }

        try
        {
            // RUNNING通知リクエスト作成
            JobStatusRequest? request = new()
            {
                StockerId = stockerId,
                JobStatus = "RUNNING",
                CurrentOperationState = "TRAVELING",
                Job = job
            };

            // サーバーへ開始通知送信
            await _apiclient.PostAsync(
                "/api/stub/equipment/started",
                request);

            //Console.WriteLine($"\nRUNNING通知 : {job.JobId}");

            Console.WriteLine($"RUNNING通知送信 JobId={job.JobId}");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"E-45 RUNNING通知失敗:{ex.Message}");
        }
    }

    // JOB完了通知送信
    public async Task NotifyCompletedAsync(string stockerId,JobInfo job)
    {
        // オフライン中は通知しない
        if(AppState.ConnectionStatus != ConnectionStatus.ONLINE)
        {
            return;
        }

        try
        {
            // COMPLETED通知リクエスト作成
            JobStatusRequest? request = new()
            {
                StockerId = stockerId,
                JobStatus = "COMPLETED",
                CurrentOperationState = "IDLE",
                Job = job
            };

            // サーバーへ完了通知送信
            await _apiclient.PostAsync<object>(
                "/api/stub/equipment/completed",
                request);

            //Console.WriteLine($"COMPLETED通知 : {job.JobId}");

            Console.WriteLine($"COMPLETED通知送信 JobId={job.JobId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"E-45 COMPLETED通知失敗:{ex.Message}");
        }
    }
}
