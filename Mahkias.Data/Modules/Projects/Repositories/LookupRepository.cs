using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class LookupRepository : AdoRepository<LookupValue>, ILookupRepository
    {
        public LookupRepository(string connectionString) : base(connectionString)
        {
        }

        public Task<IReadOnlyList<LookupValueResult>> ListUnitsAsync()
        {
            return ListAsync(@"
SELECT Id, Name, Code, IsDefault
FROM dbo.Units
ORDER BY Id");
        }

        public Task<IReadOnlyList<LookupValueResult>> ListConditionsAsync()
        {
            return ListAsync(@"
SELECT Id, Name, Code, CAST(0 AS BIT) AS IsDefault
FROM dbo.Conditions
ORDER BY Id");
        }

        public override LookupValue PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private async Task<IReadOnlyList<LookupValueResult>> ListAsync(string sql)
        {
            var values = new List<LookupValueResult>();
            using var command = new SqlCommand(sql, _connection);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    values.Add(new LookupValueResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Name = reader.ReadStringValue("Name"),
                        Code = reader.ReadStringValue("Code"),
                        IsDefault = reader.ReadBooleanValue("IsDefault"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return values;
        }
    }
}
