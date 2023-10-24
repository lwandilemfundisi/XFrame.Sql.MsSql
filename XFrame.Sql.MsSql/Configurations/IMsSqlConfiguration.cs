using XFrame.Resilience;

namespace XFrame.Sql.MsSql.Configurations
{
    public interface IMsSqlConfiguration : ISqlConfiguration<IMsSqlConfiguration>
    {
        RepeatDelay ServerBusyRepeatDelay { get; }

        IMsSqlConfiguration SetServerBusyRepeatDelay(RepeatDelay repeatDelay);
    }
}
