using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.Services;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
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

        public FrontController(StockersRepository stockersRepository,ShelfRepository shelfRepository,LogRepository logRepository,JobRepository jobRepository)
        {
            _stockersRepository = stockersRepository;
            _shelfRepository = shelfRepository;
            _logRepository = logRepository;
            _jobRepository = jobRepository;
        }

        //保管棚状態一覧取得
        [HttpGet("stockers")]
        public async Task<IActionResult> GetStockerListAsync()
        {
            //var list = new List<StockerIndexRes>
            //{
            //    new StockerIndexRes("STK001", "ストッカー1", "Active", "ONLINE", "IDLE", new Alarms("ER-001","異常が発生しました")),
            //    new StockerIndexRes("STK002", "ストッカー2", "Active", "OFFLINE", "TRAVELING", new Alarms("ER-002","異常"))
            //};

            //return Ok(list);

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

            // 200 OK と一緒に詰め替えたリストをJSONとして返却
            return Ok(responseList);
        }

        //JOB一覧取得
        [HttpGet("jobs/active")]
        public async Task<IActionResult> GetJobListAsync([FromQuery] string? stockerId)
        {
            //var list = new List<JobIndexRes>
            //{
            //    new JobIndexRes("JOB001", "STK001", "C001", "P01", "P02", "IDLE"),
            //    new JobIndexRes("JOB002", "STK002", "C002", "P03", "P04", "TRAVELING")
            //};

            //return Ok(list);

            List<JobInfo> JobList = await _jobRepository.GetAllJobs(stockerId);

            // 通信用の record（JobIndexRes）のリストに詰め替える
            List<JobIndexRes> responseList = JobList.Select(dto => new JobIndexRes(
                dto.JobId,
                dto.StockerId,
                dto.CarrierId,
                dto.SourceLocation,
                dto.DestLocation,
                dto.JobStatus
            )).ToList();

            // 200 OK で返却
            return Ok(responseList);
        }

        //JOB削除
        [HttpDelete("jobs/{jobId}")]
        public async Task<IActionResult> CancelJobAsync([FromRoute] string jobId)
        {

            Console.WriteLine($"[フロント通信受信] JOB削除要求: {jobId}");

            //bool success = await _jobRepository.DeleteOrCancelJob(jobId);

            return Ok();
        }

        [HttpGet("inventory/shelves")]
        public async Task<IActionResult> GetShelfListAsync([FromQuery] string? stockerId)
        {
            //var list = new List<ShelfStockIndexRes>
            //{
            //    new ShelfStockIndexRes("R001", "C001", DateTime.Now),
            //    new ShelfStockIndexRes("R002", "C002", DateTime.Now)
            //};

            //return Ok(list);

            List<ShelfInfo> ShelfList;

            if (string.IsNullOrEmpty(stockerId))
            {
                return BadRequest();
            }
            else
            {
                // stockerId が指定された場合（フィルター検索）
                ShelfList = await _shelfRepository.GetAllShelves(stockerId);
            }

            // 通信用の record（JobIndexRes）のリストに詰め替える
            List<ShelfStockIndexRes> responseList = ShelfList.Select(dto => new ShelfStockIndexRes(
                dto.ShelfName,
                dto.CarrierId,
                dto.InTime
            )).ToList();

            // 200 OK で返却
            return Ok(responseList);
        }

        //ログ取得
        [HttpGet("logs/recent")]
        public async Task<IActionResult> GetLogListAsync([FromQuery] string? stockerId)
        {
            //var list = new List<ShelfStockIndexRes>
            //{
            //    new ShelfStockIndexRes("R001", "C001", DateTime.Now),
            //    new ShelfStockIndexRes("R002", "C002", DateTime.Now)
            //};

            //return Ok(list);

            List<DeviceLog> LogList;

            if (string.IsNullOrEmpty(stockerId))
            {
                return BadRequest();
            }
            else
            {
                // stockerId が指定された場合（フィルター検索）
                LogList = await _logRepository.GetAllLogs(stockerId);
            }

            // 通信用の record（LogIndexRes）のリストに詰め替える
            List<LogIndexRes> responseList = LogList.Select(dto => new LogIndexRes(
                dto.Timestamp,
                dto.Level,
                dto.Message
            )).ToList();

            // 200 OK で返却
            return Ok(responseList);
        }

        //JOB生成、停止指令
        [HttpPost("equipment/command")]
        public async Task<IActionResult> CreateJobAsync([FromBody] JobCreateReq req)
        {
            Console.WriteLine($"[フロント通信受信] 搬送ジョブ送信要求を受け取りました");

            if (req == null || string.IsNullOrEmpty(req.Command) || string.IsNullOrEmpty(req.StockerId))
            {
                return BadRequest(new { Message = "必要なデータが不足しています。" });
            }


            if (req.Command == "TRANSFER")
            {
                //実際は別クラスでバリテーションチェック
                //JobService.ReceiveJobFromFront(req);
                //await JobService.ReceiveJobFromFront(req);

                string newJobId = await _jobRepository.InsertJob(req);

                Console.WriteLine($"JOBを生成しました。ID: {newJobId}, 搬送元: {req.Source} -> 搬送先: {req.Destination}");

                if (!string.IsNullOrEmpty(newJobId))
                {
                    return Ok(new
                    {
                        Success = true,
                        JobId = newJobId,
                        Message = "搬送ジョブを受付しました。"
                    });
                }

                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        JobId = newJobId,
                        ErroCode = "ER-001",
                        Message = "搬送ジョブを受付しました。"
                    });
                }
            }

            else if (req.Command == "STOP")
            {
                //実際は別クラスでバリテーションチェック
                //bool success = await StubCommandService.SendStopCommandAsync(req.StockerId);
                bool success = true;

                if (success == true)
                {
                    return Ok();
                }

                else
                {
                    return BadRequest();
                }
            }

            else
            {
                return BadRequest();
            }
        }
    }
}
