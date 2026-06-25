using stocker.Models;
using stocker.Enums;
using stocker.Client;
using stocker.Services;


namespace stocker.Services;

public class JobService
{
    private readonly NotificationService _notificationService;

    public JobService(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task ExecuteJobAsync(JobInfo job)
    {
        try
        {
            Console.WriteLine($"JOB開始 : {job.JobId}");
            AppState.OperationState = OperationState.TRAVELING;
            AppState.CurrentJobId = job.JobId;

            AppState.CancellationTokenSource = new CancellationTokenSource();

            await _notificationService.NotifyRunningAsync("STK001", job);

            await ExecuteTransferAsync(job);

            await _notificationService.NotifyCompletedAsync("STK001", job);

            AppState.CurrentJobId = null;
            AppState.AcceptedJobId = null;
            AppState.OperationState = OperationState.IDLE;

            Console.WriteLine($"JOB完了:{job.JobId}\n");
        }
        catch(OperationCanceledException)
        {
            AppState.CurrentJobId = null;
            AppState.AcceptedJobId = null;
            AppState.OperationState = OperationState.IDLE;


            Console.WriteLine($"JOB中断 : {job.JobId}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);

            AppState.CurrentJobId= null;
            AppState.AcceptedJobId = null;
            AppState.OperationState = OperationState.IDLE;
        }


    }

    public async Task ExecuteTransferAsync(JobInfo job)
    {
        Console.WriteLine("搬送開始\n");

        await Task.Delay(TimeSpan.FromSeconds(20),AppState.CancellationTokenSource!.Token);

        Console.WriteLine("搬送完了");
    }

    public async Task StopJob(string jobId)
    {
        if(AppState.OperationState != OperationState.TRAVELING)
        {
            Console.WriteLine("実行中JOBなし");
            return;
        }

        if(AppState.CurrentJobId != jobId)
        {
            Console.WriteLine("JOB不一致");
            return;
        }

        CancelCurrentJob();

        Console.WriteLine($"JOB停止 : {jobId}");
    }


    public void CancelCurrentJob()
    {
        AppState.CancellationTokenSource?.Cancel();
        Console.WriteLine("Cancel要求");
    }

    public bool IsRunning()
    {
        return AppState.OperationState == OperationState.TRAVELING;
    }
}
