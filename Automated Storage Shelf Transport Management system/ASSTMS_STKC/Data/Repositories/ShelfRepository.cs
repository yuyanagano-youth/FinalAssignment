using ASSTMS_STKC.SharedModels.Models;
using Dapper;
using System.Data;

namespace ASSTMS_STKC.Data.Repositories
{
    public class ShelfRepository
    {
        private readonly SqlDatabaseContext _context;

        // コンストラクタで本物の Context を受け取る
        public ShelfRepository(SqlDatabaseContext context)
        {
            _context = context;
        }

        // 1. 保管棚状態の取得 (SELECT)
        public List<ShelfInfo> GetAllShelves(string stockerId)
        {
            string sql = @"
                SELECT * 
                FROM Shelves
                WHERE StockerID = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Query<ShelfInfo>(sql, new { StockerId = stockerId }).ToList();
            }

        }

        // 2. 入庫完了時の在庫更新 (UPDATE)
        public int InsertStock(string shelfId, string carrierId)
        {
            string sql = @"
                UPDATE Shelves 
                SET CarrierID = @CarrierId
                WHERE ShelfName = @ShelfId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    CarrierId = carrierId,
                    ShelfId = shelfId,
                });
            }
        }

        // 3. 出庫完了時の在庫更新 (UPDATE)
        public int DeleteStockByShelfId(string shelfId)
        {
            string sql = @"
                UPDATE Shelves 
                SET CarrierID = null
                WHERE ShelfName = @ShelfId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.Execute(sql, new
                {
                    ShelfId = shelfId,
                });
            }
        }
    }
}
