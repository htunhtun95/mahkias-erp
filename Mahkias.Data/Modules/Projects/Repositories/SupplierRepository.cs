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

SELECT Q.Id, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.QuotedDate, Q.ValidUntil,
       (SELECT COUNT(*) FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id) AS ItemCount,
       (SELECT STRING_AGG(P.Name, N', ') WITHIN GROUP (ORDER BY P.Name)
        FROM projects.QuotationProjects QP
        INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
        WHERE QP.QuotationId = Q.Id) AS ProjectNames,
       (SELECT MIN(QP.ProjectId) FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id) AS ProjectId
FROM projects.Quotations Q
WHERE Q.SupplierId = @Id
  AND Q.IsDeleted = 0
ORDER BY Q.CreatedAt DESC, Q.Id DESC;

SELECT QP.QuotationId, P.Id AS ProjectId, P.Reference, P.Name
FROM projects.QuotationProjects QP
INNER JOIN projects.Quotations Q ON Q.Id = QP.QuotationId AND Q.SupplierId = @Id AND Q.IsDeleted = 0
INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
ORDER BY P.Name, P.Id");
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
                            SupplierReference = reader.ReadStringValue("SupplierReference"),
                            CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                            QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                            ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                            ItemCount = reader.ReadIntValue("ItemCount"),
                            ProjectNames = reader.ReadStringValue("ProjectNames"),
                            ProjectId = reader.IsDBNull(projectOrdinal) ? null : reader.GetInt32(projectOrdinal),
                        });
                    }
                }

                if (supplier != null && await reader.NextResultAsync())
                {
                    var quotations = supplier.Quotations.ToDictionary(quotation => quotation.Id);
                    while (await reader.ReadAsync())
                    {
                        var quotationId = reader.ReadIntValue("QuotationId");
                        if (!quotations.TryGetValue(quotationId, out var quotation))
                        {
                            continue;
                        }

                        quotation.Projects.Add(new SupplierQuotationProjectResult
                        {
                            Id = reader.ReadIntValue("ProjectId"),
                            Reference = reader.ReadStringValue("Reference"),
                            Name = reader.ReadStringValue("Name"),
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
            var name = args.Name?.Trim();
            _connection.Open();
            try
            {
                var reference = await ResolveReferenceAsync(_connection, args.Reference, name, null);
                using var command = new SqlCommand(@"
INSERT INTO projects.Suppliers (Name, Reference)
OUTPUT INSERTED.Id
VALUES (@Name, @Reference)", _connection);
                command.Parameters.AddNVarChar("Name", name, 250);
                command.Parameters.AddNVarChar("Reference", reference, SupplierReferenceRules.MaxLength);
                try
                {
                    var id = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(id);
                }
                catch (SqlException exception) when (IsDuplicateKey(exception))
                {
                    throw new DuplicateSupplierReferenceException(reference);
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> UpdateAsync(int id, CreateSupplierArgs args)
        {
            var name = args.Name?.Trim();
            _connection.Open();
            try
            {
                var reference = await ResolveReferenceAsync(_connection, args.Reference, name, id);
                using var command = new SqlCommand(@"
UPDATE projects.Suppliers
SET Name = @Name,
    Reference = @Reference,
    ModifiedAt = GETDATE()
WHERE Id = @Id AND IsDeleted = 0", _connection);
                command.Parameters.AddInt("Id", id);
                command.Parameters.AddNVarChar("Name", name, 250);
                command.Parameters.AddNVarChar("Reference", reference, SupplierReferenceRules.MaxLength);
                try
                {
                    return await command.ExecuteNonQueryAsync() > 0;
                }
                catch (SqlException exception) when (IsDuplicateKey(exception))
                {
                    throw new DuplicateSupplierReferenceException(reference);
                }
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

        private static async Task<string> ResolveReferenceAsync(SqlConnection connection, string requested, string name, int? exceptId)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                var reference = requested.Trim();
                if (reference.Length > SupplierReferenceRules.MaxLength)
                {
                    reference = reference.Substring(0, SupplierReferenceRules.MaxLength);
                }
                if (await ReferenceInUseAsync(connection, reference, exceptId))
                {
                    throw new DuplicateSupplierReferenceException(reference);
                }
                return reference;
            }

            var slug = SupplierReferenceRules.Slug(name);
            var taken = await MatchingReferencesAsync(connection, slug, exceptId);
            return SupplierReferenceRules.NextAvailable(slug, taken);
        }

        private static async Task<bool> ReferenceInUseAsync(SqlConnection connection, string reference, int? exceptId)
        {
            using var command = new SqlCommand(@"
SELECT 1
FROM projects.Suppliers
WHERE IsDeleted = 0
  AND Reference IS NOT NULL
  AND LOWER(LTRIM(RTRIM(Reference))) = LOWER(@Reference)
  AND (@ExceptId IS NULL OR Id <> @ExceptId)", connection);
            command.Parameters.AddNVarChar("Reference", reference, SupplierReferenceRules.MaxLength);
            command.Parameters.Add(new SqlParameter("@ExceptId", SqlDbType.Int) { Value = (object)exceptId ?? DBNull.Value });
            return await command.ExecuteScalarAsync() != null;
        }

        private static async Task<List<string>> MatchingReferencesAsync(SqlConnection connection, string slug, int? exceptId)
        {
            var references = new List<string>();
            using var command = new SqlCommand(@"
SELECT Reference
FROM projects.Suppliers
WHERE IsDeleted = 0
  AND Reference IS NOT NULL
  AND (@ExceptId IS NULL OR Id <> @ExceptId)
  AND (
    LOWER(LTRIM(RTRIM(Reference))) = LOWER(@Slug)
    OR LOWER(LTRIM(RTRIM(Reference))) LIKE LOWER(@Prefix)
  )", connection);
            command.Parameters.AddNVarChar("Slug", slug, SupplierReferenceRules.MaxLength);
            command.Parameters.AddNVarChar("Prefix", slug + "-%", SupplierReferenceRules.MaxLength + 2);
            command.Parameters.Add(new SqlParameter("@ExceptId", SqlDbType.Int) { Value = (object)exceptId ?? DBNull.Value });
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                references.Add(reader.ReadStringValue("Reference"));
            }
            return references;
        }

        private static bool IsDuplicateKey(SqlException exception)
        {
            return exception.Number is 2601 or 2627;
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
