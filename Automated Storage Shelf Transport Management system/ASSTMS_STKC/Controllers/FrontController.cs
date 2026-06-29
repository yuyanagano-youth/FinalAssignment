using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.Services;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ASSTMS_STKC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FrontController : ControllerBase
    {
        private readonly StockersRepository _stockersRepository;
        private readonly ShelfRepository _shelfRepository;
        private readonly LogRepository _logRepository;
        private readonly JobRepository _jobRepository;
        private readonly JobValidator _jobValidator;
        private readonly StubCommandService _stubCommandService;
        private readonly ILogger<FrontController> _logger;


        public FrontController(
            StockersRepository stockersRepository,
            ShelfRepository shelfRepository,
            LogRepository logRepository,
            JobRepository jobRepository,
            JobValidator jobValidator,
            StubCommandService stubCommandService,
            ILogger<FrontController> logger
            )
        {
            _stockersRepository = stockersRepository;
            _shelfRepository = shelfRepository;
            _logRepository = logRepository;
            _jobRepository = jobRepository;
            _jobValidator = jobValidator;
            _stubCommandService = stubCommandService;
            _logger = logger;
        }

        //保管棚状態一覧取得
        [HttpGet("stockers")]
        public async Task<IActionResult> GetStockerListAsync()
        {
            try
            {
                //_logger.LogInformation("[FRONT] ストッカー一覧取得要求を受信");

                List<StockInfo> stockerList = await _stockersRepository.GetStockerStatusesForFront();

                // リストを、通信用の record（StockerIndexRes）のリストに詰め替え
                List<StockerIndexRes> responseList = stockerList.Select(dto => new StockerIndexRes(
                    dto.StockerId,
                    dto.StockerName,
                    dto.Status,
                    dto.ConnectionStatus,
                    dto.OperationState,
                    JsonSerializer.Deserialize<List<Alarms>>(dto.alarms)
                )).ToList();

                //_logger.LogInformation("[FRONT] ストッカー一覧取得完了");
                return Ok(responseList);
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }
        }

        //JOB一覧取得
        [HttpGet("jobs/active")]
        public async Task<IActionResult> GetJobListAsync([FromQuery] string? stockerId)
        {
            try
            {
                //_logger.LogInformation("[FRONT] JOB一覧取得要求を受信");

                List<JobInfo> JobList = await _jobRepository.GetAllJobs(stockerId);

                List<JobIndexRes> responseList = JobList.Select(dto => new JobIndexRes(
                    dto.JobId ?? string.Empty,
                    dto.StockerId ?? string.Empty,
                    dto.CarrierId ?? string.Empty,
                    dto.SourceLocation ?? string.Empty,
                    dto.DestLocation ?? string.Empty,
                    dto.JobStatus ?? string.Empty
                )).ToList();

                //_logger.LogInformation("[FRONT] JOB一覧取得完了");

                return Ok(responseList);
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }

        }

        //JOB削除
        [HttpDelete("jobs/{jobId}")]
        public async Task<IActionResult> CancelJobAsync([FromRoute] string jobId)
        {
            try
            {
                _logger.LogInformation($"[FRONT] JOB削除要求: {jobId}");

                if (string.IsNullOrEmpty(jobId))
                {
                    _logger.LogWarning($"[FRONT] JOBIDが指定されていません");
                    return BadRequest();
                }

                bool success = await _jobRepository.DeleteOrCancelJob(jobId);

                if (success == true)
                {
                    _logger.LogInformation($"[FRONT] JOB削除完了: {jobId}");
                    return Ok(new
                    {
                        Message = $"JOB削除完了: {jobId}"
                    });
                }

                else
                {
                    _logger.LogWarning($"[FRONT] JOB削除に失敗しました: {jobId}");
                    return Ok(new
                    {
                        Message = $"JOB削除に失敗しました: {jobId}"
                    });
                }
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }

        }

        //在庫一覧取得
        [HttpGet("inventory/shelves")]
        public async Task<IActionResult> GetShelfListAsync([FromQuery] string? stockerId)
        {
            try
            {
                //_logger.LogInformation($"[FRONT] 在庫一覧取得要求を受信: {stockerId}");

                List<ShelfInfo> ShelfList;

                if (string.IsNullOrEmpty(stockerId))
                {
                    _logger.LogWarning($"[FRONT] ストッカーIDが指定されていません");
                    return BadRequest();
                }
                else
                {
                    ShelfList = await _shelfRepository.GetAllShelves(stockerId);
                }

                List<ShelfStockIndexRes> responseList = ShelfList.Select(dto => new ShelfStockIndexRes(
                    dto.ShelfName ?? string.Empty,
                    dto.CarrierId,
                    dto.InTime
                )).ToList();

                //_logger.LogInformation($"[FRONT] 在庫一覧取得完了: {stockerId}");

                return Ok(responseList);
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }
        }

        //ログ一覧取得
        [HttpGet("logs/recent")]
        public async Task<IActionResult> GetLogListAsync([FromQuery] string? stockerId)
        {
            try
            {
                //_logger.LogInformation($"[FRONT] ログ一覧取得要求を受信: {stockerId}");
                //stockerId が null/空の場合は「全件取得」として扱う（GetAllLogsのSQLが対応済み）
                List<DeviceLog> LogList = await _logRepository.GetAllLogs(stockerId);

                List<LogIndexRes> responseList = LogList.Select(dto => new LogIndexRes(
                    dto.Timestamp,
                    dto.Level,
                    dto.Message ?? string.Empty
                )).ToList();

                //_logger.LogInformation($"[FRONT] ログ一覧取得完了: {stockerId}");
                return Ok(responseList);
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }
        }

        //JOB生成、停止指令
        [HttpPost("equipment/command")]
        public async Task<IActionResult> CreateJobAsync([FromBody] JobCreateReq req)
        {
            try
            {
                if (req.Command == "TRANSFER")
                {
                    _logger.LogInformation($"[FRONT] 搬送ジョブ生成要求を受信 CarrierID: {req.CarrierId}, 搬送元: {req.Source} -> 搬送先: {req.Destination}");

                    var (isValid, errorMessage) = await _jobValidator.IsValidAsync(req);

                    if (!isValid)
                    {
                        _logger.LogWarning($"[FRONT] 搬送ジョブ生成失敗:{errorMessage}");
                        await _logRepository.InsertLog(req.StockerId, "WARN", $"搬送ジョブ生成に失敗しました");

                        return BadRequest(new
                        {
                            Success = false,
                            JobId = (string?)null,
                            ErrorCode = "ER-001",
                            Message = errorMessage
                        });
                    }

                    string newJobId = await _jobRepository.InsertJob(req);

                    //Console.WriteLine($"JOBを生成しました。ID: {newJobId}, CarrierID: {req.CarrierId}, 搬送元: {req.Source} -> 搬送先: {req.Destination}");

                    _logger.LogInformation($"[FRONT] 搬送ジョブ生成 搬送元: {req.Source} -> 搬送先: {req.Destination}");
                    await _logRepository.InsertLog(req.StockerId, "INFO", $"搬送ジョブ生成 搬送元: {req.Source} -> 搬送先: {req.Destination}");

                    return Ok(new
                    {
                        Success = true,
                        JobId = newJobId,
                        Message = "搬送ジョブを受付しました."
                    });
                }

                else if (req.Command == "STOP")
                {

                    _logger.LogInformation($"[FRONT] 搬送ジョブ停止要求を受信");

                    JobInfo travelingJob = await _stockersRepository.GetTravelingJobsAsync();

                    if (travelingJob == null)
                    {
                        _logger.LogWarning($"[FRONT] 搬送中のジョブが存在しません");
                        return BadRequest("搬送中ジョブが存在しません。");
                    }

                    var jobRecord = new Job(
                        JobId: travelingJob.JobId,
                        Command: req.Command,
                        CarrierId: travelingJob.CarrierId,
                        Source: travelingJob.SourceLocation,
                        Destination: travelingJob.DestLocation
                        );

                    var requestPayload = new OperationInstructionsReq(
                        HasPendingJob: true,
                        Job: jobRecord
                    );

                    if (travelingJob != null)
                    {
                        HttpResponseMessage response = await _stubCommandService.SendStopCommandAsync(requestPayload);

                        if (response.IsSuccessStatusCode)
                        {
                            StopCompletedRes result = await response.Content.ReadFromJsonAsync<StopCompletedRes>();

                            if (result == null)
                            {
                                _logger.LogError("[STUB] JSON変換に失敗しました");
                                return StatusCode(500);
                            }

                            if (string.IsNullOrEmpty(result.JobId) ||
                                string.IsNullOrEmpty(result.JobStatus) ||
                                string.IsNullOrEmpty(result.StockerId) ||
                                string.IsNullOrEmpty(result.CurrentOperationState))
                            {
                                _logger.LogError("[STUB] レスポンス項目不足がたりません");
                                return StatusCode(500);
                            }

                            _logger.LogInformation("[STUB] 搬送中断");
                            await _logRepository.InsertLog(result.StockerId, "INFO", "搬送中断");

                            await _jobRepository.UpdateJobStatus(result.JobId, result.JobStatus);
                            //_logger.LogInformation($"[DB] Job: {result.JobId}のステータスを{result.JobStatus}に更新");

                            await _stockersRepository.UpdateOperationState(result.StockerId, result.CurrentOperationState);
                            //_logger.LogInformation($"[DB] ストッカー: {result.StockerId}のステータスを{result.CurrentOperationState}に更新");

                            return Ok();
                        }
                        else
                        {
                            string error = await response.Content.ReadAsStringAsync();

                            _logger.LogError($"搬送の停止に失敗しました: {error}");

                            return BadRequest();

                        }
                    }

                    else
                    {
                        _logger.LogError($"搬送の停止に失敗しました");
                        return BadRequest();
                    }
                }

                else
                {
                    _logger.LogError($"不正なコマンドを受信: {req.Command}");
                    return BadRequest();
                }
            }

            catch (OperationCanceledException)
            {
                return StatusCode(408, "タイムアウトが発生");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FRONT] 不明なエラー {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}
