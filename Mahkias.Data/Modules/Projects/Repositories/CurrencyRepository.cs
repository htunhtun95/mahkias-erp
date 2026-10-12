using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class CurrencyRepository : AdoRepository<Currency>, ICurrencyRepository
    {
        public CurrencyRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<CurrencyResult>> ListActiveAsync()
        {
            var currencies = new List<CurrencyResult>();
            using var command = new SqlCommand(@"
SELECT Id, Code, Name, Symbol, IsDefault
FROM dbo.Currencies
WHERE IsActive = 1
ORDER BY CASE Code
    WHEN N'USD' THEN 1
    WHEN N'EUR' THEN 2
    WHEN N'GBP' THEN 3
    WHEN N'THB' THEN 4
    WHEN N'MYR' THEN 5
    ELSE 6
END, Code", _connection);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    currencies.Add(new CurrencyResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Code = reader.ReadStringValue("Code"),
                        Name = reader.ReadStringValue("Name"),
                        Symbol = reader.ReadStringValue("Symbol"),
                        IsDefault = reader.ReadBooleanValue("IsDefault"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return currencies;
        }

        public override Currency PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
