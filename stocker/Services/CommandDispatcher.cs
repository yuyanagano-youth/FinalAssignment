using NLog;
using stocker.Client;
using stocker.Enums;
using stocker.Models;
using stocker.Services;

namespace stocker.Services;

public class CommandDispatcher
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly JobService _jobService;

    public CommandDispatcher(JobService jobService)
    {
        _jobService = jobService;
    }

    public Task Dispatch(JobInfo job)
    {
        logger.Info($"コマンド受信 JobId={job.JobId}, Command={job.Command}");


        if (job.Command == "TRANSFER")
        {
            _ = Task.Run(() => HandleTransfer(job));
        }
        else if (job.Command == "STOP")
        {
            _ = HandleStop(job);
        }
        else
        {
            logger.Warn($"未対応コマンド : {job.Command}");
            Console.WriteLine($"未対応コマンド : {job.Command}");
        }

        return Task.CompletedTask;
    }

    public async Task HandleTransfer(JobInfo job)
    {
        if (job.JobId == null)
        {
            logger.Warn("jobIdなし");
            Console.WriteLine("jobIdなし");
            return;
        }
        
        if (AppState.AcceptedJobId == job.JobId)
        {
            logger.Warn($"重複JOB受信 : {job.JobId}");
            Console.WriteLine($"重複JOBがあります");
            return;
        }

        logger.Info($"TRANSFER受付 : {job.JobId}");

        AppState.AcceptedJobId = job.JobId;
        
        await _jobService.ExecuteJobAsync(job);
        
        return;
        
    }

    public async Task HandleStop(JobInfo job)
    {
        if(job.JobId == null)
        {
            logger.Warn("JobIdなし");
            Console.WriteLine("JobIdなし");
            return;
        }

        logger.Info($"STOP受付 : {job.JobId}");

        await _jobService.StopJob(job.JobId);

        return;
    }
}
