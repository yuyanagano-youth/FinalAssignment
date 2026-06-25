using stocker.Enums;
using stocker.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace stocker.Services;

public class OnlineManager
{
    NotificationService _notificationService;
    PollingService _pollingService;
    JobService _jobService;

    public OnlineManager(NotificationService notificationService,
        PollingService pollingService,
        JobService jobService)
    {
        _notificationService = notificationService;
        _pollingService = pollingService;
        _jobService = jobService;
    }

    public void Initialize()
    {
        AppState.ConnectionStatus = ConnectionStatus.OFFLINE;
        AppState.OperationState = OperationState.IDLE;
        AppState.CurrentJobId = null;
        AppState.AcceptedJobId = null;
        return;
    }
    
    public async Task GoOnlineAsync(string stockerId)
    {
        if(AppState.ConnectionStatus == ConnectionStatus.ONLINE)
        {
            return;
        }

        AppState.ConnectionStatus = ConnectionStatus.ONLINE;

        Console.WriteLine("オンライン化開始");

        AppState.OperationState = OperationState.IDLE;

        await _notificationService.SendOnlineAsync(stockerId);

        
        await _pollingService.StartPolling();

        Console.WriteLine("オンライン化完了\n");

    }

    public async Task GoOfflineAsync()
    {
        if(AppState.ConnectionStatus == ConnectionStatus.OFFLINE)
        {
            return;
        }

        _pollingService.StopPolling();

        if(AppState.OperationState == OperationState.TRAVELING)
        {
            _jobService.CancelCurrentJob();
        }

        AppState.CurrentJobId = null;
        AppState.AcceptedJobId = null;
        AppState.CancellationTokenSource = null;


        AppState.OperationState = OperationState.IDLE;
        AppState.ConnectionStatus = ConnectionStatus.OFFLINE;

        Console.WriteLine("オフライン化完了");
    }
}
