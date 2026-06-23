using System.Data;
using Microsoft.Data.SqlClient;

namespace ASSTMS_STKC.Data
{
    public class SqlDatabaseContext
    {
        private readonly string _connectionString;

        public SqlDatabaseContext(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "appsettings.json に DefaultConnection が見つかりません");
        }

        public IDbConnection CreateConnection()
        {
            // 本物の SQL Server への接続オブジェクトを生成して返す
            return new SqlConnection(_connectionString);
        }
    }
}
