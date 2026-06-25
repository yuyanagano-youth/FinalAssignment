using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.Services;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Microsoft.AspNetCore.Mvc;
using System.Buffers;
using System.Data;

namespace ASSTMS_STKC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]


    public class StubController : ControllerBase
    {
        //必要ないものは後で消す
        private readonly StockersRepository _stockersRepository;
        private readonly ShelfRepository _shelfRepository;
        private readonly LogRepository _logRepository;
        private readonly JobRepository _jobRepository;


        public StubController(
            StockersRepository stockersRepository,
            ShelfRepository shelfRepository,
            LogRepository logRepository,
            JobRepository jobRepository)
        {
            _stockersRepository = stockersRepository;
            _shelfRepository = shelfRepository;
            _logRepository = logRepository;
            _jobRepository = jobRepository;
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

                return Ok();
            }

            catch (Exception ex)
            {
                //_logger.Error(ex);

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
                //JSONデータのnull判別の方法は要確認
                if (req == null || string.IsNullOrEmpty(req.StockerId) || string.IsNullOrEmpty(req.Job.JobId) || string.IsNullOrEmpty(req.JobStatus) || string.IsNullOrEmpty(req.CurrentOperationState))
                {
                    return BadRequest(new { Message = "JSONデータの変換に失敗しました。" });
                }

                //string? Source, string? Destination
                //更新対象が存在しない場合の処理を追加
                int stockerRows = await _stockersRepository.UpdateOperationState(req.StockerId, req.CurrentOperationState);
                int jobRows = await _jobRepository.UpdateJobStatus(req.Job.JobId, req.JobStatus);


                return Ok();
            }

            catch (Exception ex)
            {
                //_logger.Error(ex);

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
                if (req == null || string.IsNullOrEmpty(req.StockerId) || string.IsNullOrEmpty(req.Job.JobId) || string.IsNullOrEmpty(req.JobStatus) || string.IsNullOrEmpty(req.CurrentOperationState))
                {
                    return BadRequest(new { Message = "JSONデータの変換に失敗しました。" });
                }

                //更新対象が存在しない場合の処理を追加
                int stockerRows = await _stockersRepository.UpdateOperationState(req.StockerId, req.CurrentOperationState);
                int jobRows = await _jobRepository.UpdateJobStatus(req.Job.JobId, req.JobStatus);
                int insertRow = await _shelfRepository.InsertStock(req.Job.Destination, req.Job.CarrierId);
                int deleteRow = await _shelfRepository.DeleteStockByShelfId(req.Job.Source);

                return Ok();
            }

            catch (Exception ex)
            {
                //_logger.Error(ex);

                return StatusCode(500, new
                {
                    message = "サーバー内部でエラーが発生しました"
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

                JobInfo? job = await _jobRepository.GetOldestUnprocessedJob();

                if (job == null)
                {
                    Console.WriteLine("未処理のJOBはありませんでした。");

                    // 例：JOBがない状態のレスポンスを作って返す
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
                    message = "サーバー内部でエラーが発生しました"
                });
            }
        }

    }
}
