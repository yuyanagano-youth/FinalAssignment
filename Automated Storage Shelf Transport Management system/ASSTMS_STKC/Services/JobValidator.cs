using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;

namespace ASSTMS_STKC.Services
{
    public class JobValidator
    {
        private readonly ShelfRepository _shelfRepository;
        private readonly JobRepository _jobRepository;

        public JobValidator(
            ShelfRepository shelfRepository,
            JobRepository jobRepository)
        {
            _shelfRepository = shelfRepository;
            _jobRepository = jobRepository;
        }

        public async Task<(bool isValid, string errorMessage)> IsValidAsync(JobCreateReq req)
        {
            string Message = string.Empty;

            if (req == null)
            {
                Message = "リクエストがnullです";
                return (false, Message);
            }

            if (req.Source == "IN_PORT" && req.Destination == "OUT_PORT")
            {
                Message = "ポート同士の搬送はできません";
                return (false, Message);
            }

            // =================================================
            // 入庫判定
            // =================================================
            if (req.Source == "IN_PORT")
            {
                var existsInShelf = await _shelfRepository.ExistsCarrier(req.StockerId,req.CarrierId);

                if (existsInShelf)
                {
                    Message = "同じキャリアIDが既に棚に存在します";
                    return (false, Message);
                }

                var hasEmptyShelf = await _shelfRepository.HasEmptyShelf(req.StockerId);

                if (!hasEmptyShelf)
                {
                    Message = "空き棚がありません";
                    return (false, Message);
                }

                var duplicateJob = await _jobRepository.ExistsInboundJob(req.CarrierId);

                if (duplicateJob)
                {
                    Message = "同じキャリアの入庫指示が既に存在します";
                    return (false, Message);
                }

                var isShelfEmpty = await _shelfRepository.IsShelfEmpty(req.CarrierId);

                if (isShelfEmpty)
                {
                    Message = "指定された棚には既に在庫が存在します";
                    return (false, Message);
                }


                return (true, string.Empty);
            }

            // =================================================
            // 出庫判定
            // =================================================
            else if (req.Destination == "OUT_PORT")
            {
                var existsInShelf = await _shelfRepository.ExistsCarrierInSourceShelf(req.Source,req.CarrierId);

                if (!existsInShelf)
                {
                    Message = "指定棚にキャリアが存在しません（在庫なし）";
                    return (false, Message);
                }

                var duplicateJob = await _jobRepository.ExistsOutboundJob(req.CarrierId);

                if (duplicateJob)
                {
                    Message = "同じキャリアの出庫指示が既に存在します";
                    return (false, Message);
                }

                return (true, string.Empty);
            }

            else
            {
                Message = "棚から棚への搬送はできません";
                return (false, Message);
            }
            

        }
    }
}
