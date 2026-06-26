using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Microsoft.AspNetCore.Mvc;

namespace ASSTMS_STKC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]


    public class StubController : ControllerBase
    {
        private readonly StockersRepository _stockersRepository;
        private readonly ShelfRepository _shelfRepository;
        private readonly LogRepository _logRepository;
        private readonly JobRepository _jobRepository;
        private readonly ILogger _logger;


        public StubController(
            StockersRepository stockersRepository,
            ShelfRepository shelfRepository,
            LogRepository logRepository,
            JobRepository jobRepository,
            ILogger<StubController> logger)
        {
            _stockersRepository = stockersRepository;
            _shelfRepository = shelfRepository;
            _logRepository = logRepository;
            _jobRepository = jobRepository;
            _logger = logger;
        }


        //スタブオンライン報告
        [HttpPost("equipment/online")]
        public async Task<IActionResult> HandleOnlineNotifyAsync([FromBody] OnlineReportReq req)
        {
            await _stockersRepository.updateLastSeenTime(req.StockerId);

            try
            {
                if (req == null || string.IsNullOrEmpty(req.StockerId) || string.IsNullOrEmpty(req.ConnectionStatus))
                {
                    return BadRequest(new { Message = "JSONデータの変換に失敗しました。" });
                }

                int rows = await _stockersRepository.UpdateOnlineStatus(req.StockerId, req.ConnectionStatus);
                _logger.LogInformation($"[STUB] オンライン移行");
                await _logRepository.InsertLog(req.StockerId, "INFO", "オンライン移行");

                return Ok();
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[STUB] 不明なエラー {ex.Message}");

                return StatusCode(500, new
                {
                    message = "サーバー内部でエラーが発生しました"
                });
            }
        }

        //動作開始報告
        [HttpPost("equipment/started")]
        public async Task<IActionResult> HandleActionStartAsync([FromBody] JobStartReportReq req)
        {
            await _stockersRepository.updateLastSeenTime(req.StockerId);

            try
            {
                if (req == null ||
                    string.IsNullOrEmpty(req.StockerId) ||
                    string.IsNullOrEmpty(req.Job.JobId) ||
                    string.IsNullOrEmpty(req.JobStatus) ||
                    string.IsNullOrEmpty(req.CurrentOperationState)
                    )
                {
                    return BadRequest(new { Message = "JSONデータの変換に失敗しました。" });
                }

                int stockerRows = await _stockersRepository.UpdateOperationState(req.StockerId, req.CurrentOperationState);
                int jobRows = await _jobRepository.UpdateJobStatus(req.Job.JobId, req.JobStatus);

                _logger.LogInformation($"[STUB] 搬送開始　搬送元: {req.Job.Source} -> 搬送先: {req.Job.Destination}");
                await _logRepository.InsertLog(req.StockerId, "INFO", $"搬送開始　搬送元: {req.Job.Source} -> 搬送先: {req.Job.Destination}");

                return Ok();
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[STUB] 不明なエラー {ex.Message}");

                return StatusCode(500, new
                {
                    message = "サーバー内部でエラーが発生しました"
                });
            }
        }

        //動作完了報告
        [HttpPost("equipment/completed")]
        public async Task<IActionResult> HandleStatusReportAsync([FromBody] JobStartReportReq req)
        {
            await _stockersRepository.updateLastSeenTime(req.StockerId);

            try
            {
                if (req == null || 
                    string.IsNullOrEmpty(req.StockerId) || 
                    string.IsNullOrEmpty(req.Job.JobId) || 
                    string.IsNullOrEmpty(req.JobStatus) || 
                    string.IsNullOrEmpty(req.CurrentOperationState)||
                    string.IsNullOrEmpty(req.Job.Source) ||      
                    string.IsNullOrEmpty(req.Job.Destination) || 
                    string.IsNullOrEmpty(req.Job.CarrierId)
                    )
                {
                    return BadRequest(new { Message = "JSONデータの変換に失敗しました。" });
                }

                int stockerRows = await _stockersRepository.UpdateOperationState(req.StockerId, req.CurrentOperationState);
                int jobRows = await _jobRepository.UpdateJobStatus(req.Job.JobId, req.JobStatus);

                if (req.Job.Source == "IN_PORT")
                {
                    int insertRow = await _shelfRepository.InsertStock(req.Job.Destination, req.Job.CarrierId);
                }
                else
                {
                    int deleteRow = await _shelfRepository.DeleteStockByShelfId(req.Job.Source);
                }

                _logger.LogInformation($"[STUB] 搬送完了　搬送元: {req.Job.Source} -> 搬送先: {req.Job.Destination}");
                await _logRepository.InsertLog(req.StockerId, "INFO", $"搬送完了　搬送元: {req.Job.Source} -> 搬送先: {req.Job.Destination}");

                return Ok();
            }

            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ex,message = $"サーバー内部でエラーが発生しました{ex.Message}"
                });
            }
        }

        //ポーリング
        [HttpPost("equipment/polling")]
        public async Task<IActionResult> HandleJobCheckAsync([FromBody] PollingReq req)
        {
            await _stockersRepository.updateLastSeenTime(req.StockerId);

            try
            {
                if (string.IsNullOrEmpty(req.StockerId))
                {
                    return BadRequest(new
                    {
                        message = "StockerIdが指定されていません"
                    });
                }

                JobInfo? job = await _jobRepository.GetOldestUnprocessedJob(req.StockerId);

                if (job == null)
                {
                    Console.WriteLine("未処理のJOBはありませんでした。");

                    var emptyPayload = new PollingRes(
                        HasPendingJob: false,
                        Job: null
                    );
                    return Ok(emptyPayload);
                }

                var jobRecord = new Job(
                    JobId: job.JobId,
                    Command: "TRANSFER",
                    CarrierId: job.CarrierId,
                    Source: job.SourceLocation,
                    Destination: job.DestLocation
                    );

                var requestPayload = new PollingRes(
                    HasPendingJob: true,
                    Job: jobRecord
                    );

                return Ok(requestPayload);
            }

            catch (Exception ex)
            {
                //_logger.Error(ex);

                return StatusCode(500, new
                {
                    ex,message = $"サーバー内部でエラーが発生しました{ex.Message}"
                });
            }
        }

    }
}
