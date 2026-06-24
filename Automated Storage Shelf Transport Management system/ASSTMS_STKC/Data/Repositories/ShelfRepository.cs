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
        public async Task<List<ShelfInfo>> GetAllShelves(string stockerId)
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
        public async Task<int> InsertStock(string shelfId, string carrierId)
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
        public async Task<int> DeleteStockByShelfId(string shelfId)
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

        //4. 空き棚判定 (SELECT)
        public async Task<bool> HasEmptyShelf(string stockerId)
        {
            string sql = @"
                SELECT COUNT(1)
                FROM Shelves
                WHERE CarrierId IS NULL
                AND StockerId = @StockerId;";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new {StockerId = stockerId}) > 0;
            }

        }

        //5. 入庫時在個チェック判定 (SELECT)
        public async Task<bool> ExistsCarrier(string stockerId)
        {
            string sql = @"
                SELECT COUNT(1)
                FROM Shelves
                WHERE CarrierId = @CarrierId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new { StockerId = stockerId }) > 0;
            }

        }

        //6. 指定棚にキャリアがあるか判定 (SELECT)
        public async Task<bool> ExistsCarrierInSourceShelf(string shelfId, string carrierId)
        {
            string sql = @"
                SELECT COUNT(1)
                FROM Shelves
                WHERE ShelfId = @ShelfId
                AND CarrierId = @CarrierId";

            using (IDbConnection db = _context.CreateConnection())
            {
                return db.ExecuteScalar<int>(sql, new { ShelfId = shelfId, CarrierId = carrierId}) > 0;
            }

        }

        //7. 指定棚が空いてるか判定 (SELECT)
        public async Task<bool> IsShelfEmpty(string shelfId)
        {
            string sql = @"
                SELECT CarrierId
                FROM Shelves
                WHERE ShelfId = @ShelfId";

            using (IDbConnection db = _context.CreateConnection())
            {
                var carrierId = db.QueryFirstOrDefault<string>(sql, new { ShelfId = shelfId });

                return string.IsNullOrEmpty(carrierId);
            }

        }
    }
}
