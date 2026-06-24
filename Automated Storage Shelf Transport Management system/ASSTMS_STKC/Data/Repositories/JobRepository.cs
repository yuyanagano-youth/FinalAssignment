using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using Dapper;
using System.Data;

namespace ASSTMS_STKC.Data.Repositories
{
    public class JobRepository
    {
        private readonly SqlDatabaseContext _context;

        // コンストラクタで本物の Context を受け取る
        public JobRepository(SqlDatabaseContext context)
        {
            _context = context;
        }

        // 1. JOBの新規登録 (INSERT)
        public async Task<string> InsertJob(JobCreateReq req)
        {
            //実際の採番は別クラス
            string newJobId = "JOB" + DateTime.Now.ToString("yyyyMMddHHmmss");

            string sql = @"
                INSERT INTO Jobs (JobID, StockerID, CarrierID, SourceLocation, DestLocation, JobStatus, CreatedAt)
                VALUES (@JobId, @StockerId, @CarrierId, @SourceLocation, @DestLocation, @Status, @CreatedAt);";

            using (IDbConnection db = _context.CreateConnection())
            {
                db.Execute(sql, new
                {
                    JobId = newJobId,
                    StockerID = req.StockerId,
                    CarrierID = req.CarrierId,
                    SourceLocation = req.Source,
                    DestLocation = req.Destination,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now
                });
            }

            return newJobId;
        }

        // 2. JOBの一覧取得 (SELECT)
        public async Task<List<JobInfo>> GetAllJobs(string? stockerId)
        {
            string sql = @"
                SELECT * 
                FROM Jobs
                WHERE @StockerId IS NULL 
                    OR StockerId = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                var result = await db.QueryAsync<JobInfo>(sql, new { StockerId = stockerId });
                return result.ToList();
            }

        }

        // 3. 選択JOB取得 (SELECT)
        public async Task<JobInfo?> GetJobById(string jobId)
        {
            string sql = @"
                SELECT * 
                FROM Jobs
                WHERE JobId = @JobId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.QueryFirstOrDefault<JobInfo>(sql, new{JobId = jobId});
            }
        }

        // 4. 一番古いJOBの取得 (SELECT)
        public async Task<JobInfo?> GetOldestUnprocessedJob()
        {
            string sql = @"
                SELECT TOP 1 * 
                FROM Jobs
                ORDER BY ClosedAt ASC";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.QueryFirstOrDefault<JobInfo>(sql);
            }
        }

        // 5. JOBステータスの変更 (UPDATE)
        public async Task<int> UpdateJobStatus(string jobId, string status)
        {
            string sql = @"
                UPDATE Jobs 
                SET JobStatus = @Status
                WHERE JobId = @JobId";

            using (IDbConnection db = _context.CreateConnection())
            {
               return db.Execute(sql, new
                {
                    JobId = jobId,
                    Status = status
                });
            }
        }

        // 6. JOB削除 (DELETE)
        public async Task<int> DeleteOrCancelJob(string jobId)
        {
            string sql = @"
               DELETE FROM Jobs
                WHERE JobId = @JobId
                AND JobStatus = @JobStatus";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    JobId = jobId,
                    JobStatus = "PENDING"
                });
            }
        }

        // 7. 入庫ジョブ（IN_PORT）が既に存在するか (SELECT)
        public async Task<bool> ExistsInboundJob(string carrierId)
        {
            string sql = @"
               SELECT COUNT(1)
                FROM Jobs
                WHERE CarrierId = @CarrierId
                AND Source = 'IN_PORT'
                AND JobStatus IN ('WAITING', 'IN_PROGRESS')";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new { CarrierId = carrierId }) > 0;
            }

        }

        // 8. 出庫ジョブ（OUT_PORT）が既に存在するか (SELECT)
        public async Task<bool> ExistsOutboundJob(string carrierId)
        {
            string sql = @"
                SELECT COUNT(1)
                FROM Jobs
                WHERE CarrierId = @CarrierId
                AND Destination = 'OUT_PORT'
                AND JobStatus IN ('WAITING', 'IN_PROGRESS')";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new { CarrierId = carrierId }) > 0;
            }

        }
    }
}