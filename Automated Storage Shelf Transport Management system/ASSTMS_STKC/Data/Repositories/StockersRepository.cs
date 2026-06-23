using ASSTMS_STKC.SharedModels.Models;
using Dapper;
using System.Data;

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
        public List<StockInfo> GetStockerStatusesForFront()
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
        public int UpdateOnlineStatus(string stockerId, string connectionStatus, string operationState)
        {
            string sql = @"
                UPDATE Stockers 
                SET ConnectionStatus = @ConnectionStatus, OperationState = @OperationState
                WHERE StockerID = @StockerId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    StockerId = stockerId,
                    ConnectionStatus = connectionStatus,
                    OperationState = operationState,
                });
            }
        }

        // 3 動作開始、完了時の状態変更 (UPDATE)
        public int UpdateOperationState(string stockerId, string operationState)
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
        public int TimeoutOfflineStockers(int timeoutSeconds)
        {
            //最終通信時刻が(現在時刻-設定時間)より前の時間のレコードを一括変更
            string sql = @"
                UPDATE Stockers
                SET OperationState = 'OFFLINE'
                WHERE LastHeartbeat <= DATEADD(SECOND, -@Timeout, GETDATE());";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    Timeout = timeoutSeconds
                });
            }
        }
    }
}
