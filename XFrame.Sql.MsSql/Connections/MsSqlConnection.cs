using Microsoft.Extensions.Logging;
using XFrame.Common;
using XFrame.Resilience;
using XFrame.Sql.MsSql.Configurations;
using XFrame.Sql.MsSql.Integrations;
using XFrame.Sql.MsSql.ResilienceStrategies;

namespace XFrame.Sql.MsSql.Connections
{
    public class MsSqlConnection
        : SqlConnection<IMsSqlConfiguration, IMsSqlErrorResilientStrategy, IMsSqlConnectionFactory>, IMsSqlConnection
    {
        public MsSqlConnection(
            ILogger<MsSqlConnection> logger,
            IMsSqlConfiguration configuration,
            IMsSqlConnectionFactory connectionFactory,
            ITransientFaultHandler<IMsSqlErrorResilientStrategy> transientFaultHandler)
            : base(logger, configuration, connectionFactory, transientFaultHandler)
        {
        }

        public override Task<IReadOnlyCollection<TResult>> InsertMultipleAsync<TResult, TRow>(
            Label label,
            string connectionStringName,
            CancellationToken cancellationToken,
            string sql,
            IEnumerable<TRow> rows)
        {
            Logger.LogTrace(
                "Using optimized table type to insert with SQL: {Sql}",
                sql);
            var tableParameter = new TableParameter<TRow>("@rows", rows, new { });
            return QueryAsync<TResult>(label, connectionStringName, cancellationToken, sql, tableParameter);
        }
    }
}
