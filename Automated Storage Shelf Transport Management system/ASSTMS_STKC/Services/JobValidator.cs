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

        public bool IsValid(JobCreateReq request, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage = "リクエストがnullです";
                return false;
            }

            if (request.Source == "IN_PORT" && request.Destination == "OUT_PORT")
            {
                errorMessage = "ポート同士の搬送はできません";
                return false;
            }

            // =================================================
            // 入庫判定
            // =================================================
            if (request.Source == "IN_PORT")
            {
                var existsInShelf = _shelfRepository.ExistsCarrier(request.CarrierId);

                if (existsInShelf)
                {
                    errorMessage = "同じキャリアIDが既に棚に存在します";
                    return false;
                }

                var hasEmptyShelf = _shelfRepository.HasEmptyShelf();

                if (!hasEmptyShelf)
                {
                    errorMessage = "空き棚がありません";
                    return false;
                }

                var duplicateJob = _jobRepository.ExistsInboundJob(request.CarrierId);

                if (duplicateJob)
                {
                    errorMessage = "同じキャリアの入庫指示が既に存在します";
                    return false;
                }
            }

            // =================================================
            // 出庫判定
            // =================================================
            if (request.Destination == "OUT_PORT")
            {
                var existsInShelf =
                    _shelfRepository.ExistsCarrierInSourceShelf(
                        request.Source,
                        request.CarrierId);

                if (!existsInShelf)
                {
                    errorMessage = "指定棚にキャリアが存在しません（在庫なし）";
                    return false;
                }

                var duplicateJob = _jobRepository.ExistsOutboundJob(request.CarrierId);

                if (duplicateJob)
                {
                    errorMessage = "同じキャリアの出庫指示が既に存在します";
                    return false;
                }
            }

            return true;
        }
    }
}
