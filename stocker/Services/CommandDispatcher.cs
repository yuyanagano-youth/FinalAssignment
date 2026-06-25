using stocker.Models;
using stocker.Enums;
using stocker.Client;
using stocker.Services;

namespace stocker.Services;

public class CommandDispatcher
{
    private readonly JobService _jobService;

    public CommandDispatcher(JobService jobService)
    {
        _jobService = jobService;
    }

    public async Task Dispatch(JobInfo job)
    {



        if (job.Command == "TRANSFER")
        {
            await HandleTransfer(job);
            return;
        }

        if (job.Command == "STOP")
        {
            await HandleStop(job);
            return;
        }



    }

    public async Task HandleTransfer(JobInfo job)
    {
       

            if (job.JobId == null)
            {
                Console.WriteLine("jobIdなし");
                return;
            }

            if (AppState.AcceptedJobId == job.JobId)
            {
                Console.WriteLine($"重複JOBがあります");
                return;
            }

            AppState.AcceptedJobId = job.JobId;

            await _jobService.ExecuteJobAsync(job);

            return;
        
    }

    public async Task HandleStop(JobInfo job)
    {
        if(job.JobId == null)
        {
            Console.WriteLine("JobIdなし");
            return;
        }

        await _jobService.StopJob(job.JobId);

        return;
    }
}
