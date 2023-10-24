using Dapper;
using Microsoft.SqlServer.Server;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;

namespace XFrame.Sql.MsSql.Integrations
{
    internal class TableParameter<TRow> : SqlMapper.IDynamicParameters
        where TRow : class
    {
        private readonly string _name;
        private readonly IEnumerable<TRow> _rows;
        private readonly SqlMapper.IDynamicParameters _otherParameters;

        private static readonly Dictionary<SqlDbType, Action<SqlDataRecord, int, object>> SqlDataRecordSetters =
            new Dictionary<SqlDbType, Action<SqlDataRecord, int, object>>
            {
                {SqlDbType.NText, (r, i, o) => r.SetString(i, (string)o)},
                {SqlDbType.DateTimeOffset, (r, i, o) => r.SetDateTimeOffset(i, (DateTimeOffset)o)},
                {SqlDbType.Int, (r, i, o) => r.SetInt32(i, (int)o)},
                {SqlDbType.BigInt, (r, i, o) => r.SetInt64(i, (long)o)},
                {SqlDbType.UniqueIdentifier, (r, i, o) => r.SetGuid(i, (Guid)o)},
            };
        private static readonly Dictionary<Type, SqlDbType> SqlDbTypes = new Dictionary<Type, SqlDbType>
            {
                {typeof(Guid), SqlDbType.UniqueIdentifier},
                {typeof(int), SqlDbType.Int},
                {typeof(string), SqlDbType.NText},
                {typeof(long), SqlDbType.BigInt},
                {typeof(DateTime), SqlDbType.DateTime},
                {typeof(DateTimeOffset), SqlDbType.DateTimeOffset},
            };
        private static readonly SqlMetaData[] SqlMetaDatas;
        private static readonly Dictionary<string, PropertyInfo> PropertyInfos;

        static TableParameter()
        {
            var rowType = typeof(TRow);
            PropertyInfos = rowType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(pi => pi.Name != "GlobalSequenceNumber") // TODO: remove
                .ToDictionary(pi => pi.Name);
            SqlMetaDatas = PropertyInfos
                .Select(pi => new SqlMetaData(pi.Key, SqlDbTypes[pi.Value.PropertyType]))
                .OrderBy(m => m.Name)
                .ToArray();
        }

        public TableParameter(string name, IEnumerable<TRow> rows, object otherParameters)
            : this(name, rows, new DynamicParameters(otherParameters))
        {
        }

        public TableParameter(string name, IEnumerable<TRow> rows, SqlMapper.IDynamicParameters otherParameters)
        {
            _name = name;
            _rows = rows;
            _otherParameters = otherParameters;
        }

        public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            var sqlDataRecords = _rows
                .Select(CreateSqlDataRecord)
                .ToList();

            var sqlParameter = CreateSqlParameter(_name, command, sqlDataRecords);

            command.Parameters.Add(sqlParameter);

            _otherParameters.AddParameters(command, identity);
        }

        private static SqlParameter CreateSqlParameter(string name, IDbCommand command, List<SqlDataRecord> sqlDataRecords)
        {
            var sqlParameter = (SqlParameter)command.CreateParameter();
            sqlParameter.SqlDbType = SqlDbType.Structured;
            sqlParameter.ParameterName = name;
            sqlParameter.TypeName = $"{typeof(TRow).Name.ToLowerInvariant()}_list_type";
            sqlParameter.Value = sqlDataRecords;
            return sqlParameter;
        }

        public static SqlDataRecord CreateSqlDataRecord(TRow eventData)
        {
            var sqlDataRecord = new SqlDataRecord(SqlMetaDatas);

            foreach (var pair in SqlMetaDatas.Select((md, i) => new { Index = i, MetaData = md }))
            {
                object propertyValue = null;
                try
                {
                    var propertyInfo = PropertyInfos[pair.MetaData.Name];
                    propertyValue = propertyInfo.GetValue(eventData, null);
                    if (!ReferenceEquals(propertyValue, null))
                    {
                        var setter = SqlDataRecordSetters[pair.MetaData.SqlDbType];
                        setter(sqlDataRecord, pair.Index, propertyValue);
                    }
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"Failed to configure property '{pair.MetaData.Name}' with value '{propertyValue}'",
                        exception);
                }
            }

            return sqlDataRecord;
        }
    }
}
