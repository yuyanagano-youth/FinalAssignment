using ASSTMS_STKC.SharedModels;
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

        // 0. JOBの新規登録 (INSERT)
        public string InsertJob(JobCreateReq req)
        {
            //実際の採番は別クラス
            string newJobId = "JOB" + DateTime.Now.ToString("yyyyMMddHHmmss");

            string sql = @"
                INSERT INTO T_Jobs (JobId, StockerId, CarrierId, Source, Destination, Status, CreatedAt)
                VALUES (@JobId, @StockerId, @CarrierId, @Source, @Destination, @Status, @CreatedAt);";

            using (IDbConnection db = _context.CreateConnection())
            {
                db.Execute(sql, new
                {
                    JobId = newJobId,
                    StockerId = req.StockerId,
                    CarrierId = req.CarrierId,
                    Source = req.Source,
                    Destination = req.Destination,
                    Status = "WAITING",
                    CreatedAt = DateTime.Now
                });
            }

            return newJobId;
        }
    }
}