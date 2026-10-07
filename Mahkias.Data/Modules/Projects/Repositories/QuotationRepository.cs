using Mahkias.Core.DataTableTypes;
using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class QuotationRepository : AdoRepository<QuotationEntity>, IQuotationRepository
    {
        public QuotationRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<QuotationMapActivity>> GetActivitiesForMappingAsync(IReadOnlyList<int> projectIds)
        {
            var ids = (projectIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                return Array.Empty<QuotationMapActivity>();
            }

            var activities = new List<QuotationMapActivity>();
            using var command = new SqlCommand();
            var parameters = new string[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                parameters[i] = "@P" + i;
                command.Parameters.AddInt("P" + i, ids[i]);
            }

            command.CommandText = $@"
SELECT A.Id, A.ProjectId, A.DSNNo, A.PartNo, A.Description, A.Quantity, A.ActivityGroupId
FROM projects.Activities A
WHERE A.IsDeleted = 0
  AND A.ProjectId IN ({string.Join(", ", parameters)});

SELECT AP.Id, AP.ActivityId, AP.MainPartNo, AP.AlternativePartNo
FROM projects.ActivityAlternativeParts AP
INNER JOIN projects.Activities A ON A.Id = AP.ActivityId AND A.IsDeleted = 0
WHERE A.ProjectId IN ({string.Join(", ", parameters)})
ORDER BY AP.ActivityId, AP.SortOrder, AP.Id;";
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    activities.Add(new QuotationMapActivity
                    {
                        Id = reader.ReadIntValue("Id"),
                        ProjectId = reader.ReadIntValue("ProjectId"),
                        DsnNo = reader.ReadStringValue("DSNNo"),
                        PartNo = reader.ReadStringValue("PartNo"),
                        Description = reader.ReadStringValue("Description"),
                        Quantity = reader.ReadNullableDecimalValue("Quantity"),
                        ActivityGroupId = reader.ReadNullableIntValue("ActivityGroupId"),
                    });
                }

                if (await reader.NextResultAsync())
                {
                    var lookup = activities.ToDictionary(activity => activity.Id);
                    while (await reader.ReadAsync())
                    {
                        var activityId = reader.ReadIntValue("ActivityId");
                        if (!lookup.TryGetValue(activityId, out var activity))
                        {
                            continue;
                        }

                        activity.Alternatives.Add(new QuotationMapAlternative
                        {
                            Id = reader.ReadIntValue("Id"),
                            ActivityId = activityId,
                            MainPartNo = reader.ReadStringValue("MainPartNo"),
                            AlternativePartNo = reader.ReadStringValue("AlternativePartNo"),
                        });
                    }
                }
            }
            finally
            {
                _connection.Close();
            }

            return activities;
        }

        public async Task<int> CreateAsync(CreateQuotationArgs args)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            using var command = new SqlCommand("dbo.CreateQuotation", _connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.Add(OptionalText("SupplierReference", NullIfBlank(args.SupplierReference), 100));
            command.Parameters.Add(OptionalText("DriveFileLink", NullIfBlank(args.DriveFileLink), 1000));
            command.Parameters.AddTable("ProjectIds", ProjectIdTable(args.ProjectIds), "dbo.NumberTableType");
            command.Parameters.AddTable("Items", ItemTable(args.Items), "dbo.DefaultGenericTableType");
            _connection.Open();
            try
            {
                var id = await command.ExecuteScalarAsync();
                return id == null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<QuotationListItem>> GetByProjectAsync(int projectId)
        {
            var items = new List<QuotationListItem>();
            using var command = new SqlCommand(@"
SELECT Q.Id, Q.SupplierId, S.Name AS SupplierName, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
       COUNT(I.Id) AS ItemCount, ISNULL(SUM(I.TotalPrice), 0) AS TotalPrice
FROM projects.Quotations Q
INNER JOIN projects.QuotationProjects QP ON QP.QuotationId = Q.Id
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
LEFT JOIN projects.QuotationItems I ON I.QuotationId = Q.Id
WHERE QP.ProjectId = @ProjectId
  AND Q.IsDeleted = 0
  AND S.IsDeleted = 0
GROUP BY Q.Id, Q.SupplierId, S.Name, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt
ORDER BY Q.CreatedAt DESC, Q.Id DESC");
            command.Parameters.AddInt("ProjectId", projectId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new QuotationListItem
                    {
                        Id = reader.ReadIntValue("Id"),
                        SupplierId = reader.ReadIntValue("SupplierId"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        Code = reader.ReadStringValue("Code"),
                        SupplierReference = reader.ReadStringValue("SupplierReference"),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                        ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                        ItemCount = reader.ReadIntValue("ItemCount"),
                        TotalPrice = reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public async Task<QuotationDetailResult> GetDetailAsync(int quotationId)
        {
            QuotationDetailResult detail = null;
            using var command = new SqlCommand(@"
SELECT Q.Id, Q.SupplierId, S.Name AS SupplierName, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt
FROM projects.Quotations Q
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
WHERE Q.Id = @Id
  AND Q.IsDeleted = 0;

SELECT I.Id, COALESCE(NULLIF(LTRIM(RTRIM(I.DsnNo)), N''), A.DSNNo) AS DsnNo, I.PartNo, I.PartDescription,
       I.UnitPrice, I.Quantity, I.TotalPrice, I.ActivityId, A.DSNNo AS ActivityDsn, A.PartNo AS ActivityPart
FROM projects.QuotationItems I
LEFT JOIN projects.Activities A ON A.Id = I.ActivityId
WHERE I.QuotationId = @Id
ORDER BY I.Id;", _connection);
            command.Parameters.AddInt("Id", quotationId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    detail = new QuotationDetailResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        SupplierId = reader.ReadIntValue("SupplierId"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        Code = reader.ReadStringValue("Code"),
                        SupplierReference = reader.ReadStringValue("SupplierReference"),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                        ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                    };
                }

                if (detail != null && await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var activityDsn = reader.ReadStringValue("ActivityDsn");
                        var activityPart = reader.ReadStringValue("ActivityPart");
                        var mapped = string.Join(" · ", new[] { activityDsn, activityPart }.Where(value => !string.IsNullOrWhiteSpace(value)));
                        detail.Items.Add(new QuotationLineResult
                        {
                            Id = reader.ReadIntValue("Id"),
                            DsnNo = reader.ReadStringValue("DsnNo"),
                            PartNo = reader.ReadStringValue("PartNo"),
                            Description = reader.ReadStringValue("PartDescription"),
                            UnitPrice = reader.ReadNullableDecimalValue("UnitPrice"),
                            Quantity = reader.ReadIntValue("Quantity"),
                            TotalPrice = reader.ReadNullableDecimalValue("TotalPrice"),
                            ActivityId = reader.ReadNullableIntValue("ActivityId"),
                            MappedActivity = string.IsNullOrWhiteSpace(mapped) ? null : mapped,
                        });
                    }

                    detail.ItemCount = detail.Items.Count;
                    detail.TotalPrice = detail.Items.Sum(item => item.TotalPrice ?? 0);
                }
            }
            finally
            {
                _connection.Close();
            }

            return detail;
        }

        public async Task<IReadOnlyList<ActivityQuotationLink>> GetActivityLinksAsync(int projectId)
        {
            var links = new List<ActivityQuotationLink>();
            using var command = new SqlCommand(@"
WITH Matched AS (
    SELECT A.Id AS ActivityId, Q.Id AS QuotationId, I.Id AS ItemId, Q.Code AS QuotationRef, S.Name AS SupplierName,
           Q.SupplierReference AS SupplierQuotationRef, I.PartNo, I.PartDescription AS Description,
           I.Quantity, I.UnitPrice, I.TotalPrice,
           RecordedAt = (SELECT MAX(v) FROM (VALUES (I.CreatedAt), (I.ModifiedAt), (Q.CreatedAt), (Q.ModifiedAt)) AS Stamp(v)),
           CASE WHEN EXISTS (
                SELECT 1
                FROM projects.ActivityAlternativeParts AP
                WHERE AP.ActivityId = A.Id
                  AND LOWER(LTRIM(RTRIM(I.PartNo))) = LOWER(LTRIM(RTRIM(AP.AlternativePartNo)))
                  AND LOWER(LTRIM(RTRIM(I.PartNo))) <> LOWER(LTRIM(RTRIM(AP.MainPartNo)))
           ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS AlternativePart
    FROM projects.Activities A
    INNER JOIN projects.QuotationProjects QP ON QP.ProjectId = A.ProjectId
    INNER JOIN projects.Quotations Q ON Q.Id = QP.QuotationId
    INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
    INNER JOIN projects.QuotationItems I ON I.QuotationId = Q.Id
    WHERE A.ProjectId = @ProjectId
      AND A.IsDeleted = 0
      AND Q.IsDeleted = 0
      AND S.IsDeleted = 0
      AND I.UnitPrice IS NOT NULL
      AND (
            (
                NULLIF(LTRIM(RTRIM(A.DSNNo)), N'') IS NOT NULL
                AND NULLIF(LTRIM(RTRIM(I.DsnNo)), N'') IS NOT NULL
                AND LOWER(LTRIM(RTRIM(A.DSNNo))) = LOWER(LTRIM(RTRIM(I.DsnNo)))
                AND (
                    (
                        SELECT COUNT(*)
                        FROM projects.Activities SameDsn
                        WHERE SameDsn.ProjectId = A.ProjectId
                          AND SameDsn.IsDeleted = 0
                          AND LOWER(LTRIM(RTRIM(SameDsn.DSNNo))) = LOWER(LTRIM(RTRIM(A.DSNNo)))
                    ) = 1
                    OR LOWER(LTRIM(RTRIM(I.PartNo))) = LOWER(LTRIM(RTRIM(A.PartNo)))
                    OR EXISTS (
                        SELECT 1
                        FROM projects.ActivityAlternativeParts AP
                        WHERE AP.ActivityId = A.Id
                          AND (
                                LOWER(LTRIM(RTRIM(I.PartNo))) = LOWER(LTRIM(RTRIM(AP.MainPartNo)))
                                OR LOWER(LTRIM(RTRIM(I.PartNo))) = LOWER(LTRIM(RTRIM(AP.AlternativePartNo)))
                          )
                    )
                )
            )
            OR EXISTS (
                SELECT 1
                FROM projects.ActivityAlternativeParts AP
                WHERE AP.ActivityId = A.Id
                  AND LOWER(LTRIM(RTRIM(I.PartNo))) = LOWER(LTRIM(RTRIM(AP.AlternativePartNo)))
                  AND (
                        NULLIF(LTRIM(RTRIM(I.DsnNo)), N'') IS NULL
                        OR LOWER(LTRIM(RTRIM(I.DsnNo))) = LOWER(LTRIM(RTRIM(A.DSNNo)))
                        OR NOT EXISTS (
                            SELECT 1
                            FROM projects.Activities Other
                            WHERE Other.ProjectId = A.ProjectId
                              AND Other.IsDeleted = 0
                              AND NULLIF(LTRIM(RTRIM(Other.DSNNo)), N'') IS NOT NULL
                              AND LOWER(LTRIM(RTRIM(Other.DSNNo))) = LOWER(LTRIM(RTRIM(I.DsnNo)))
                        )
                  )
            )
      )
),
Ranked AS (
    SELECT *,
           ROW_NUMBER() OVER (PARTITION BY ActivityId ORDER BY UnitPrice ASC, QuotationId ASC, ItemId ASC) AS QuoteRank
    FROM Matched
)
SELECT ActivityId, ItemId, QuotationId, QuotationRef, SupplierName, SupplierQuotationRef, PartNo, Description, Quantity, AlternativePart, UnitPrice, TotalPrice, RecordedAt,
       CASE WHEN QuoteRank = 1 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS Lowest
FROM Ranked
ORDER BY ActivityId, QuoteRank, QuotationId", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    links.Add(new ActivityQuotationLink
                    {
                        ActivityId = reader.ReadIntValue("ActivityId"),
                        ItemId = reader.ReadIntValue("ItemId"),
                        QuotationId = reader.ReadIntValue("QuotationId"),
                        QuotationRef = reader.ReadStringValue("QuotationRef"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        SupplierQuotationRef = reader.ReadStringValue("SupplierQuotationRef"),
                        PartNo = reader.ReadStringValue("PartNo"),
                        Description = reader.ReadStringValue("Description"),
                        Quantity = reader.ReadIntValue("Quantity"),
                        AlternativePart = reader.ReadBooleanValue("AlternativePart"),
                        UnitPrice = reader.ReadNullableDecimalValue("UnitPrice"),
                        TotalPrice = reader.ReadNullableDecimalValue("TotalPrice"),
                        Lowest = reader.ReadBooleanValue("Lowest"),
                        RecordedAt = reader.ReadDateTimeValue("RecordedAt"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return links;
        }

        public async Task<bool> UpdateAsync(int quotationId, CreateQuotationArgs args)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            using var command = new SqlCommand("dbo.ReplaceQuotationItems", _connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddInt("Id", quotationId);
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.Add(OptionalText("SupplierReference", NullIfBlank(args.SupplierReference), 100));
            command.Parameters.AddTable("Items", ItemTable(args.Items), "dbo.DefaultGenericTableType");
            _connection.Open();
            try
            {
                var updated = await command.ExecuteScalarAsync();
                return updated != null && updated != DBNull.Value && Convert.ToInt32(updated) > 0;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> DeleteAsync(int quotationId)
        {
            using var command = new SqlCommand(@"
UPDATE projects.Quotations
SET IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = NULL
WHERE Id = @Id AND IsDeleted = 0", _connection);
            command.Parameters.AddInt("Id", quotationId);
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

        public async Task<IReadOnlyList<PartPriceHistoryItem>> GetPriceHistoryAsync(string partNo)
        {
            var items = new List<PartPriceHistoryItem>();
            using var command = new SqlCommand(@"
SELECT I.Id, I.QuotationId, Q.Code AS QuotationReference, S.Name AS SupplierName,
       I.PartNo, I.PartDescription, I.UnitPrice, I.Quantity, I.TotalPrice, I.CreatedAt
FROM projects.QuotationItems I
INNER JOIN projects.Quotations Q ON Q.Id = I.QuotationId
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
WHERE I.PartNo = @PartNo
  AND Q.IsDeleted = 0
  AND S.IsDeleted = 0
ORDER BY I.CreatedAt DESC, I.Id DESC");
            command.Parameters.AddNVarChar("PartNo", partNo?.Trim(), 100);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new PartPriceHistoryItem
                    {
                        Id = reader.ReadIntValue("Id"),
                        QuotationId = reader.ReadIntValue("QuotationId"),
                        QuotationReference = reader.ReadStringValue("QuotationReference"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        PartNo = reader.ReadStringValue("PartNo"),
                        PartDescription = reader.ReadStringValue("PartDescription"),
                        UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                        Quantity = reader.ReadIntValue("Quantity"),
                        TotalPrice = reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public override QuotationEntity PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        public async Task<string> NextCodeAsync()
        {
            using var command = new SqlCommand(@"
SELECT CASE
    WHEN EXISTS (SELECT 1 FROM projects.Quotations) THEN CAST(IDENT_CURRENT(N'projects.Quotations') AS INT) + 1
    ELSE 1
END", _connection);
            _connection.Open();
            try
            {
                var next = Convert.ToInt32(await command.ExecuteScalarAsync());
                return QuotationCode.Format(next);
            }
            finally
            {
                _connection.Close();
            }
        }

        private static DataTable ProjectIdTable(IReadOnlyList<int> projectIds)
        {
            return (projectIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray().ToNumericDatatableArgs();
        }

        private static DataTable ItemTable(IReadOnlyList<QuotationItemWrite> items)
        {
            var rows = (items ?? Array.Empty<QuotationItemWrite>())
                .Where(item => item != null)
                .Select(item => new DefaultGenericTableType
                {
                    NumericValue1 = item.ActivityId,
                    NumericValue2 = item.ActivityGroupId,
                    NumericValue3 = item.AlternativePartId,
                    NumericValue4 = item.Quantity,
                    BitValue1 = item.IsAlternativePart,
                    TextValue1 = item.DsnNo,
                    TextValue2 = item.PartNo,
                    TextValue3 = item.PartDescription,
                    DecimalValue1 = item.UnitPrice,
                    DecimalValue2 = item.TotalPrice
                })
                .ToList();
            return rows.ToGenericDatatableNullableArgs();
        }

        private static SqlParameter OptionalText(string name, string value, int size)
        {
            return new SqlParameter("@" + name, SqlDbType.NVarChar, size)
            {
                Value = (object)value ?? DBNull.Value,
            };
        }

        private static string NullIfBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
