using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class SupplierRepository : AdoRepository<Supplier>, ISupplierRepository
    {
        public SupplierRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<SupplierResult>> SearchAsync(string search)
        {
            var suppliers = new List<SupplierResult>();
            var term = search?.Trim() ?? string.Empty;
            using var command = new SqlCommand(@"
SELECT S.Id, S.Name, S.Reference, S.CreatedAt, S.ModifiedAt, COUNT(Q.Id) AS QuotationCount
FROM projects.Suppliers S
LEFT JOIN projects.Quotations Q ON Q.SupplierId = S.Id AND Q.IsDeleted = 0
WHERE S.IsDeleted = 0
  AND (@Search = N''
   OR S.Name LIKE @Like
   OR ISNULL(S.Reference, N'') LIKE @Like)
GROUP BY S.Id, S.Name, S.Reference, S.CreatedAt, S.ModifiedAt
ORDER BY S.Name, S.Id");
            command.Parameters.AddNVarChar("Search", term, 250);
            command.Parameters.AddNVarChar("Like", "%" + term + "%", 260);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suppliers.Add(ReadSupplier(reader));
                }
            }
            finally
            {
                _connection.Close();
            }

            return suppliers;
        }

        public async Task<SupplierDetailResult> GetAsync(int id)
        {
            SupplierDetailResult supplier = null;
            using var command = new SqlCommand(@"
SELECT S.Id, S.Name, S.Reference, S.CreatedAt, S.ModifiedAt,
       (SELECT COUNT(*) FROM projects.Quotations Q WHERE Q.SupplierId = S.Id AND Q.IsDeleted = 0) AS QuotationCount,
       (SELECT COUNT(DISTINCT QP.ProjectId)
        FROM projects.QuotationProjects QP
        INNER JOIN projects.Quotations Q ON Q.Id = QP.QuotationId AND Q.IsDeleted = 0
        INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
        WHERE Q.SupplierId = S.Id) AS ProjectCount
FROM projects.Suppliers S
WHERE S.Id = @Id
  AND S.IsDeleted = 0;

SELECT Q.Id, Q.Code, Q.CreatedAt,
       (SELECT COUNT(*) FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id) AS ItemCount,
       (SELECT STRING_AGG(P.Name, N', ') WITHIN GROUP (ORDER BY P.Name)
        FROM projects.QuotationProjects QP
        INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
        WHERE QP.QuotationId = Q.Id) AS ProjectNames,
       (SELECT MIN(QP.ProjectId) FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id) AS ProjectId
FROM projects.Quotations Q
WHERE Q.SupplierId = @Id
  AND Q.IsDeleted = 0
ORDER BY Q.CreatedAt DESC, Q.Id DESC");
            command.Parameters.AddInt("Id", id);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    supplier = new SupplierDetailResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Name = reader.ReadStringValue("Name"),
                        Reference = reader.ReadStringValue("Reference"),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                        ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                        QuotationCount = reader.ReadIntValue("QuotationCount"),
                        ProjectCount = reader.ReadIntValue("ProjectCount"),
                    };
                }

                if (supplier != null && await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var projectOrdinal = reader.GetOrdinal("ProjectId");
                        supplier.Quotations.Add(new SupplierQuotationResult
                        {
                            Id = reader.ReadIntValue("Id"),
                            Code = reader.ReadStringValue("Code"),
                            CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                            ItemCount = reader.ReadIntValue("ItemCount"),
                            ProjectNames = reader.ReadStringValue("ProjectNames"),
                            ProjectId = reader.IsDBNull(projectOrdinal) ? null : reader.GetInt32(projectOrdinal),
                        });
                    }
                }
            }
            finally
            {
                _connection.Close();
            }

            return supplier;
        }

        public async Task<int> CreateAsync(CreateSupplierArgs args)
        {
            using var command = new SqlCommand(@"
INSERT INTO projects.Suppliers (Name, Reference)
OUTPUT INSERTED.Id
VALUES (@Name, @Reference)");
            command.Parameters.AddNVarChar("Name", args.Name?.Trim(), 250);
            command.Parameters.Add(new SqlParameter("@Reference", SqlDbType.NVarChar, 100)
            {
                Value = string.IsNullOrWhiteSpace(args.Reference) ? DBNull.Value : args.Reference.Trim(),
            });
            command.Connection = _connection;
            _connection.Open();
            try
            {
                var id = await command.ExecuteScalarAsync();
                return Convert.ToInt32(id);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> UpdateAsync(int id, CreateSupplierArgs args)
        {
            using var command = new SqlCommand(@"
UPDATE projects.Suppliers
SET Name = @Name,
    Reference = @Reference,
    ModifiedAt = GETDATE()
WHERE Id = @Id AND IsDeleted = 0");
            command.Parameters.AddInt("Id", id);
            command.Parameters.AddNVarChar("Name", args.Name?.Trim(), 250);
            command.Parameters.Add(new SqlParameter("@Reference", SqlDbType.NVarChar, 100)
            {
                Value = string.IsNullOrWhiteSpace(args.Reference) ? DBNull.Value : args.Reference.Trim(),
            });
            command.Connection = _connection;
            _connection.Open();
            try
            {
                return await command.ExecuteNonQueryAsync() > 0;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<SupplierDeleteResult> DeleteAsync(int id)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            _connection.Open();
            SqlTransaction transaction = null;
            try
            {
                transaction = _connection.BeginTransaction();
                using (var countCommand = new SqlCommand("SELECT COUNT(*) FROM projects.Quotations WHERE SupplierId = @Id AND IsDeleted = 0", _connection, transaction))
                {
                    countCommand.Parameters.AddInt("Id", id);
                    var quotations = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                    if (quotations > 0)
                    {
                        transaction.Rollback();
                        return new SupplierDeleteResult { Found = true, Deleted = false };
                    }
                }

                using var deleteCommand = new SqlCommand(@"
UPDATE projects.Suppliers
SET IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = NULL
WHERE Id = @Id AND IsDeleted = 0", _connection, transaction);
                deleteCommand.Parameters.AddInt("Id", id);
                var deleted = await deleteCommand.ExecuteNonQueryAsync();
                if (deleted == 0)
                {
                    transaction.Rollback();
                    return new SupplierDeleteResult { Found = false, Deleted = false };
                }

                transaction.Commit();
                return new SupplierDeleteResult { Found = true, Deleted = true };
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
                _connection.Close();
            }
        }

        public override Supplier PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private static SupplierResult ReadSupplier(SqlDataReader reader)
        {
            return new SupplierResult
            {
                Id = reader.ReadIntValue("Id"),
                Name = reader.ReadStringValue("Name"),
                Reference = reader.ReadStringValue("Reference"),
                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                QuotationCount = reader.ReadIntValue("QuotationCount"),
            };
        }
    }
}
