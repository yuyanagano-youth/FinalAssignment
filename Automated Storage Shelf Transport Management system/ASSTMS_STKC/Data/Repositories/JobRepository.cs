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
            string newJobId = "JOB" + DateTime.Now.ToString("yyyyMMddHHmmss");

            string sql = @"
                INSERT INTO Jobs (JobID, StockerID, CarrierID, SourceLocation, DestLocation, JobStatus, CreatedAt)
                VALUES (@JobId, @StockerId, @CarrierId, @SourceLocation, @DestLocation, @Status, @CreatedAt);";

            using (IDbConnection db = _context.CreateConnection())
            {
                db.Execute(sql, new
                {
                    JobID = newJobId,
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
            //NULLの場合は全件取得
            string sql = @"
                SELECT * 
                FROM Jobs
                WHERE @StockerID IS NULL 
                    OR StockerID = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                var result = await db.QueryAsync<JobInfo>(sql, new { StockerId = stockerId });
                return result.ToList();
            }

        }

        // 3. 一番古いJOBの取得 (SELECT)
        public async Task<JobInfo?> GetOldestUnprocessedJob(string stockerId)
        {
            //ステータスが「PENDING」のジョブを古い順に並べて一件取得
            string sql = @"
                SELECT j.* FROM Jobs j
                INNER JOIN Stockers s ON j.StockerID = s.StockerID
                WHERE j.JobStatus = 'PENDING'
                AND s.StockerID = @StockerId
                ORDER BY CreatedAt ASC";

            using (IDbConnection db = _context.CreateConnection())
            {
                return await db.QueryFirstOrDefaultAsync<JobInfo>(sql, new {StockerID = stockerId});
            }
        }

        // 4. JOBステータスの変更 (UPDATE)
        public async Task<int> UpdateJobStatus(string jobId, string status)
        {
            string sql = @"
                UPDATE Jobs 
                SET JobStatus = @Status
                WHERE JobID = @JobId";

            using (IDbConnection db = _context.CreateConnection())
            {
               return await db.ExecuteAsync(sql, new
                {
                    JobID = jobId,
                    Status = status
                });
            }
        }

        // 5. JOB削除 (DELETE)
        public async Task<bool> DeleteOrCancelJob(string jobId)
        {
            string sql = @"
               DELETE FROM Jobs
                WHERE JobID = @JobId
                AND JobStatus <> @JobStatus";

            using (IDbConnection db = _context.CreateConnection())
            {
                return await db.ExecuteAsync(sql, new{JobID = jobId, JobStatus = "PENDING" }) > 0;
            }
        }

        // 6. 入庫ジョブ（IN_PORT）が既に存在するか (SELECT)
        public async Task<bool> ExistsInboundJob(string carrierId)
        {
            string sql = @"
               SELECT COUNT(1)
                FROM Jobs
                WHERE CarrierID = @CarrierId
                AND SourceLocation = 'IN_PORT'
                AND JobStatus IN ('PENDING', 'RUNNING')";

            using (IDbConnection db = _context.CreateConnection())
            {
                return await db.ExecuteScalarAsync<int>(sql, new { CarrierId = carrierId }) > 0;
            }

        }

        // 7. 出庫ジョブ（OUT_PORT）が既に存在するか (SELECT)
        public async Task<bool> ExistsOutboundJob(string carrierId)
        {
            string sql = @"
                SELECT COUNT(1)
                FROM Jobs
                WHERE CarrierId = @CarrierId
                AND DestLocation = 'OUT_PORT'
                AND JobStatus IN ('PENDING', 'RUNNING')";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new { CarrierId = carrierId }) > 0;
            }

        }
    }
}