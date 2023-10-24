using XFrame.Resilience;

namespace XFrame.Sql.MsSql.Configurations
{
    public class MsSqlConfiguration : SqlConfiguration<IMsSqlConfiguration>, IMsSqlConfiguration
    {
        public static MsSqlConfiguration New => new();

        private MsSqlConfiguration()
        {
        }

        // From official documentation on MSDN: "The service is currently busy. Repeat the request after 10 seconds"
        public RepeatDelay ServerBusyRepeatDelay { get; private set; } = RepeatDelay.Between(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(15));

        public IMsSqlConfiguration SetServerBusyRepeatDelay(RepeatDelay repeatDelay)
        {
            ServerBusyRepeatDelay = repeatDelay;

            return this;
        }
    }
}
