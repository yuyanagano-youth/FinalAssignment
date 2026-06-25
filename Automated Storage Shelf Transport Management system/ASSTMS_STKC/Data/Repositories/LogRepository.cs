using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using System.Data;
using Dapper;

namespace ASSTMS_STKC.Data.Repositories
{
    public class LogRepository
    {
        private readonly SqlDatabaseContext _context;

        // コンストラクタで本物の Context を受け取る
        public LogRepository(SqlDatabaseContext context)
        {
            _context = context;
        }

        // 1. ログの新規登録 (INSERT)
        public async Task<int> InsertLog(string stockerId, string level, string message)
        {
            string sql = @"
                INSERT INTO Logs (Timestamp, StockerID, Level, Message)
                VALUES (@TimeStamp, @StockerId, @Level, @Message);";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    //LogId = log.LogId,
                    //Timestamp = log.Timestamp,
                    Timestamp = DateTime.Now,
                    StockerId = stockerId,
                    Level = level,
                    Message = message,
                });
            }

        }

        // 2. ログの取得 (SELECT)
        public async Task<List<DeviceLog>> GetAllLogs(string stockerId)

        {
            string sql = @"
                SELECT * 
                FROM Logs
                WHERE @StockerId IS NULL 
                    OR StockerId = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Query<DeviceLog>(sql, new{StockerId = stockerId}).ToList();
            }

        }
    }
}
