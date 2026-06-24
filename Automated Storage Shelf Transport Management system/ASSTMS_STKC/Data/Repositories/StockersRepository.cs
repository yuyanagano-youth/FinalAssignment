using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Dapper;
using System.Data;
using System.Text.Json;

namespace ASSTMS_STKC.Data.Repositories
{
    public class StockersRepository
    {
        private readonly SqlDatabaseContext _context;

        // コンストラクタで本物の Context を受け取る
        public StockersRepository(SqlDatabaseContext context)
        {
            _context = context;
        }

        // 1. 保管棚状態の取得 (SELECT)
        public async Task<List<StockInfo>> GetStockerStatusesForFront()
        {
            string sql = @"
                SELECT * 
                FROM Stockers;";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Query<StockInfo>(sql).ToList();
            }

        }

        // 2. オンライン報告時の状態変更 (UPDATE)
        public async Task<int> UpdateOnlineStatus(string stockerId, string connectionStatus)
        {
            string sql = @"
                UPDATE Stockers 
                SET ConnectionStatus = @ConnectionStatus, OperationState = 'IDLE'
                WHERE StockerID = @StockerId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    StockerId = stockerId,
                    ConnectionStatus = connectionStatus,

                });
            }
        }

        // 3 動作開始、完了時の状態変更 (UPDATE)
        public async Task<int> UpdateOperationState(string stockerId, string operationState)
        {
            string sql = @"
                UPDATE Stockers 
                SET OperationState = @OperationState
                WHERE StockerID = @StockerId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    StockerId = stockerId,
                    OperationState = operationState,
                });
            }
        }

        // 4　一定時間通信がない保管棚をオフラインに変更 (UPDATE)
        public async Task<int> TimeoutOfflineStockers(int timeoutSeconds)
        {
            //最終通信時刻が(現在時刻-設定時間)より前の時間のレコードを一括変更
            string sql = @"
                UPDATE Stockers
                SET ConnectionStatus = 'OFFLINE'
                WHERE LastHeartbeat <= DATEADD(SECOND, -@Timeout, GETDATE());";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    Timeout = timeoutSeconds
                });
            }
        }

        // 5　通信の度に最終通信時間を更新 (UPDATE)
        public async Task<int> updateLastSeenTime(string stockerid)
        {
            //最終通信時刻が(現在時刻-設定時間)より前の時間のレコードを一括変更
            string sql = @"
                UPDATE Stockers
                SET LastHeartbeat = GETDATE()
                WHERE StockerID = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    StockerID = stockerid
                });
            }
        }

        //6 送信用のJOBを取得
        public async Task<IEnumerable<JobInfo>> GetPendingJobsAsync()
        {
            string sql = @"
        SELECT j.* FROM Jobs j
        INNER JOIN Stockers s ON j.StockerId = s.StockerId
        WHERE j.JobStatus = 'PENDING'          -- 条件1: 実行待ちのJOB
          AND s.ConnectionStatus = 'ONLINE'; -- 条件2: 保管棚がオンライン状態のもの";

            using (IDbConnection db = _context.CreateConnection())
            {
                return await db.QueryAsync<JobInfo>(sql);
            }
        }
    }
}
