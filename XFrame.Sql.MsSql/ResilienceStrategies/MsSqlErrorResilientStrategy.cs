using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Data.SqlClient;
using XFrame.Resilience;
using XFrame.Sql.MsSql.Configurations;

namespace XFrame.Sql.MsSql.ResilienceStrategies
{
    public class MsSqlErrorResilientStrategy : IMsSqlErrorResilientStrategy
    {
        private readonly ILogger<MsSqlErrorResilientStrategy> _logger;
        private readonly IMsSqlConfiguration _msSqlConfiguration;

        public MsSqlErrorResilientStrategy(
            ILogger<MsSqlErrorResilientStrategy> logger,
            IMsSqlConfiguration msSqlConfiguration)
        {
            _logger = logger;
            _msSqlConfiguration = msSqlConfiguration;
        }

        public virtual Repeat CheckRetry(
            Exception exception,
            TimeSpan totalExecutionTime,
            int currentRepeatCount)
        {
            // List of possible errors inspired by Azure SqlDatabaseTransientErrorDetectionStrategy

            var sqlException = exception as SqlException;
            if (sqlException == null || currentRepeatCount > _msSqlConfiguration.TransientRetryCount)
            {
                return Repeat.No;
            }

            var repeat = Enumerable.Empty<Repeat>()
                .Concat(CheckErrorCode(sqlException))
                .Concat(CheckInnerException(sqlException))
                .FirstOrDefault();

            return repeat ?? Repeat.No;
        }

        private IEnumerable<Repeat> CheckErrorCode(SqlException sqlException)
        {
            foreach (SqlError sqlExceptionError in sqlException.Errors)
            {
                switch (sqlExceptionError.Number)
                {
                    // SQL Error Code: 40501
                    // The service is currently busy. Repeat the request after 10 seconds.
                    case 40501:
                        {
                            var delay = _msSqlConfiguration.ServerBusyRepeatDelay.PickDelay();
                            _logger.LogWarning(
                                "MSSQL server returned error 40501 which means it too busy and asked us to wait 10 seconds! Trying to wait {Seconds} seconds.",
                                delay.TotalSeconds);
                            yield return Repeat.YesAfter(delay);
                            yield break;
                        }

                    // SQL Error Code: 40613
                    // Database XXXX on server YYYY is not currently available. Please repeat the connection later. If the problem persists, contact customer
                    // support, and provide them the session tracing ID of ZZZZZ.
                    case 40613:

                    // SQL Error Code: 40540
                    // The service has encountered an error processing your request. Please try again.
                    case 40540:

                    // SQL Error Code: 40197
                    // The service has encountered an error processing your request. Please try again.
                    case 40197:

                    // SQL Error Code: 40143
                    // The service has encountered an error processing your request. Please try again.
                    case 40143:

                    // SQL Error Code: 18401
                    // Login failed for user '%s'. Reason: Server is in script upgrade mode. Only administrator can connect at this time.
                    // Devnote: this can happen when SQL is going through recovery (e.g. after failover)
                    case 18401:

                    // SQL Error Code: 10929
                    // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d. 
                    // However, the server is currently too busy to support requests greater than %d for this database.
                    case 10929:

                    // SQL Error Code: 10928
                    // Resource ID: %d. The %s limit for the database is %d and has been reached.
                    case 10928:

                    // SQL Error Code: 10060
                    // A network-related or instance-specific error occurred while establishing a connection to SQL Server.
                    // The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server
                    // is configured to allow remote connections. (provider: TCP Provider, error: 0 - A connection attempt failed
                    // because the connected party did not properly respond after a period of time, or established connection failed
                    // because connected host has failed to respond.)"}
                    case 10060:

                    // SQL Error Code: 10054
                    // A transport-level error has occurred when sending the request to the server.
                    // (provider: TCP Provider, error: 0 - An existing connection was forcibly closed by the remote host.)
                    case 10054:

                    // SQL Error Code: 10053
                    // A transport-level error has occurred when receiving results from the server.
                    // An established connection was aborted by the software in your host machine.
                    case 10053:

                    // SQL Error Code: 233
                    // The client was unable to establish a connection because of an error during connection initialization process before login.
                    // Possible causes include the following: the client tried to connect to an unsupported version of SQL Server; the server was too busy
                    // to accept new connections; or there was a resource limitation (insufficient memory or maximum allowed connections) on the server.
                    // (provider: TCP Provider, error: 0 - An existing connection was forcibly closed by the remote host.)
                    case 233:

                    // SQL Error Code: 64
                    // A connection was successfully established with the server, but then an error occurred during the login process.
                    // (provider: TCP Provider, error: 0 - The specified network name is no longer available.)
                    case 64:
                        yield return Repeat.YesAfter(_msSqlConfiguration.TransientRepeatDelay.PickDelay());
                        yield break;
                }
            }
        }

        private IEnumerable<Repeat> CheckInnerException(SqlException sqlException)
        {
            // Prelogin failure can happen due to waits expiring on windows handles. Or
            // due to bugs in the gateway code, a dropped database with a pooled connection
            // when reset results in a timeout error instead of immediate failure.

            var win32Exception = sqlException.InnerException as Win32Exception;
            if (win32Exception == null) yield break;

            if (win32Exception.NativeErrorCode == 0x102 || win32Exception.NativeErrorCode == 0x121)
            {
                yield return Repeat.YesAfter(_msSqlConfiguration.TransientRepeatDelay.PickDelay());
            }
        }
    }
}
