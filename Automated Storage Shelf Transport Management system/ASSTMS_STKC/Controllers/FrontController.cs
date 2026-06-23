using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Microsoft.AspNetCore.Mvc;
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
        public IActionResult GetStockers()
        {
            var list = new List<StockerIndexRes>
            {
                new StockerIndexRes("STK001", "ストッカー1", "Active", "ONLINE", "IDLE"),
                new StockerIndexRes("STK002", "ストッカー2", "Active", "OFFLINE", "TRAVELING")
            };

            return Ok(list);

            //List<StockInfo> stockerList = _stockersRepository.GetStockerStatusesForFront();

            //// リストを、通信用の record（StockerIndexRes）のリストに詰め替え
            //List<StockerIndexRes> responseList = stockerList.Select(dto => new StockerIndexRes(
            //    dto.StockerId,
            //    dto.StockerName,
            //    dto.Status,
            //    dto.ConnectionStatus,
            //    dto.OperationState
            //)).ToList();

            //// 200 OK と一緒に詰め替えたリストをJSONとして返却
            //return Ok(responseList);
        }

        [HttpGet("jobs/active")]
        public IActionResult GetActiveJobs([FromQuery] string? stockerId)
        {
            var list = new List<JobIndexRes>
            {
                new JobIndexRes("JOB001", "STK001", "C001", "P01", "P02", "IDLE"),
                new JobIndexRes("JOB002", "STK002", "C002", "P03", "P04", "TRAVELING")
            };

            return Ok(list);

            //List<JobInfo> JobList;

            //if (string.IsNullOrEmpty(stockerId))
            //{
            //    // 省略された場合（全件取得）
            //    JobList = _jobRepository.GetAllActiveJobs();
            //}
            //else
            //{
            //    // stockerId が指定された場合（フィルター検索）
            //    JobList = _jobRepository.GetActiveJobsByStockerId(stockerId);
            //}

            //// 通信用の record（JobIndexRes）のリストに詰め替える
            //List<JobIndexRes> responseList = JobList.Select(dto => new JobIndexRes(
            //    dto.JobId,
            //    dto.StockerId,
            //    dto.CarrierId,
            //    dto.SourceLocation,
            //    dto.DestLocation,
            //    dto.JobStatus
            //)).ToList();

            //// 200 OK で返却
            //return Ok(responseList);
        }

        [HttpDelete("jobs/{jobId}")]
        public IActionResult DeleteJob([FromRoute] string jobId)
        {
            // 💡 URLの「{jobId}」の部分が、自動的に引数の jobId に入ってきます。
            Console.WriteLine($"[フロント通信受信] JOB削除要求: {jobId}");

            //bool success = _jobRepository.DeleteOrCancelJob(jobId);

            return Ok();
        }

        [HttpGet(" /api/front/inventory/shelves")]
        public IActionResult GetShelves([FromQuery] string? stockerId)
        {
            var list = new List<ShelfStockIndexRes>
            {
                new ShelfStockIndexRes("R001", "C001", DateTime.Now),
                new ShelfStockIndexRes("R002", "C002", DateTime.Now)
            };

            return Ok(list);

            //List<ShelfInfo> ShelfList;

            //if (string.IsNullOrEmpty(stockerId))
            //{
            //    return BadRequest();
            //}
            //else
            //{
            //    // stockerId が指定された場合（フィルター検索）
            //    ShelfList = _shelfRepository.GetAllShelves(stockerId);
            //}

            //// 通信用の record（JobIndexRes）のリストに詰め替える
            //List<ShelfStockIndexRes> responseList = ShelfList.Select(dto => new ShelfStockIndexRes(
            //    dto.ShelfName,
            //    dto.CarrierId,
            //    dto.InTime
            //)).ToList();

            //// 200 OK で返却
            //return Ok(responseList);
        }

    }
}
