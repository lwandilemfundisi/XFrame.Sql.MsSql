using System.Data;
using System.Data.SqlClient;

namespace XFrame.Sql.MsSql.Connections
{
    public class MsSqlConnectionFactory : IMsSqlConnectionFactory
    {
        public async Task<IDbConnection> OpenConnectionAsync(
            string connectionString,
            CancellationToken cancellationToken)
        {
            var sqlConnection = new SqlConnection(connectionString);
            await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return sqlConnection;
        }
    }
}
