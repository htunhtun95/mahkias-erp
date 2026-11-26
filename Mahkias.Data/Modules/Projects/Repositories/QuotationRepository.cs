using Mahkias.Core.DataTableTypes;
using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

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
SELECT A.Id, A.ProjectId, A.DSNNo, A.PartNo, A.Description, A.Quantity, A.ActivityGroupId, A.ActivityTypeId
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
                        ActivityTypeId = reader.ReadNullableIntValue("ActivityTypeId"),
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

            await _connection.OpenAsync();
            SqlTransaction transaction = null;
            try
            {
                transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
                var id = await ExecuteCreateAsync(args, transaction, CancellationToken.None);
                if (id <= 0)
                {
                    await RollbackQuietlyAsync(transaction);
                    return 0;
                }

                await ReplaceChargesAsync(id, args.Charges, transaction, CancellationToken.None);
                await transaction.CommitAsync();
                return id;
            }
            catch
            {
                await RollbackQuietlyAsync(transaction);
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }

                _connection.Close();
            }
        }

        public async Task<int> ImportAsync(CreateQuotationArgs args, Func<int, CancellationToken, Task<CreateQuotationDocumentArgs>> attachFile, CancellationToken cancellationToken = default)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            await _connection.OpenAsync(cancellationToken);
            SqlTransaction transaction = null;
            try
            {
                transaction = (SqlTransaction)await _connection.BeginTransactionAsync(cancellationToken);
                var id = await ExecuteCreateAsync(args, transaction, cancellationToken);
                if (id <= 0)
                {
                    throw new InvalidOperationException("The quotation could not be saved.");
                }

                await ReplaceChargesAsync(id, args.Charges, transaction, cancellationToken);
                if (attachFile != null)
                {
                    await LinkUploadedFileAsync(id, attachFile, transaction, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return id;
            }
            catch
            {
                await RollbackQuietlyAsync(transaction);
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }

                _connection.Close();
            }
        }

        public async Task SaveExistingAsync(int quotationId, CreateQuotationArgs args, Func<int, CancellationToken, Task<CreateQuotationDocumentArgs>> attachFile, CancellationToken cancellationToken = default)
        {
            if (quotationId <= 0)
            {
                throw new InvalidOperationException("The quotation could not be saved.");
            }

            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            await _connection.OpenAsync(cancellationToken);
            SqlTransaction transaction = null;
            try
            {
                transaction = (SqlTransaction)await _connection.BeginTransactionAsync(cancellationToken);
                var updated = await ExecuteUpdateAsync(quotationId, args, transaction, cancellationToken);
                if (!updated)
                {
                    throw new InvalidOperationException("The quotation could not be saved.");
                }

                await ReplaceChargesAsync(quotationId, args.Charges, transaction, cancellationToken);
                if (attachFile != null)
                {
                    await LinkUploadedFileAsync(quotationId, attachFile, transaction, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await RollbackQuietlyAsync(transaction);
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }

                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<QuotationListItem>> GetByProjectAsync(int projectId)
        {
            var items = new List<QuotationListItem>();
            using var command = new SqlCommand(@"
SELECT Q.Id, Q.SupplierId, S.Name AS SupplierName, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
       Q.QuotedDate, Q.ValidUntil,
       Q.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, Q.ExchangeRate, Q.BaseTotalAmount,
       COUNT(I.Id) AS ItemCount, Q.BaseTotalAmount AS TotalPrice
FROM projects.Quotations Q
INNER JOIN projects.QuotationProjects QP ON QP.QuotationId = Q.Id
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = Q.CurrencyId
LEFT JOIN projects.QuotationItems I ON I.QuotationId = Q.Id
WHERE QP.ProjectId = @ProjectId
  AND Q.IsDeleted = 0
  AND S.IsDeleted = 0
GROUP BY Q.Id, Q.SupplierId, S.Name, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
         Q.QuotedDate, Q.ValidUntil,
         Q.CurrencyId, C.Code, C.Symbol, Q.ExchangeRate, Q.BaseTotalAmount
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
                        QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                        ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                        ItemCount = reader.ReadIntValue("ItemCount"),
                        TotalPrice = reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                        CurrencyId = reader.ReadIntValue("CurrencyId"),
                        CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                        ExchangeRate = reader.GetDecimal(reader.GetOrdinal("ExchangeRate")),
                        BaseTotalAmount = reader.GetDecimal(reader.GetOrdinal("BaseTotalAmount")),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        private static QuotationListItem ReadListItem(SqlDataReader reader)
        {
            return new QuotationListItem
            {
                Id = reader.ReadIntValue("Id"),
                SupplierId = reader.ReadIntValue("SupplierId"),
                SupplierName = reader.ReadStringValue("SupplierName"),
                Code = reader.ReadStringValue("Code"),
                SupplierReference = reader.ReadStringValue("SupplierReference"),
                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                ItemCount = reader.ReadIntValue("ItemCount"),
                TotalPrice = reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                CurrencyId = reader.ReadIntValue("CurrencyId"),
                CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                ExchangeRate = reader.GetDecimal(reader.GetOrdinal("ExchangeRate")),
                BaseTotalAmount = reader.GetDecimal(reader.GetOrdinal("BaseTotalAmount")),
                ProjectIds = ParseIds(reader.ReadStringValue("ProjectIds")),
                ProjectLabel = reader.ReadNullableStringValue("ProjectLabel"),
            };
        }

        private static List<int> ParseIds(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<int>();
            }

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.Parse(part.Trim()))
                .ToList();
        }

        public async Task<IReadOnlyList<QuotationListItem>> ListAsync()
        {
            var items = new List<QuotationListItem>();
            using var command = new SqlCommand(@"
SELECT Q.Id, Q.SupplierId, S.Name AS SupplierName, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
       Q.QuotedDate, Q.ValidUntil,
       Q.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, Q.ExchangeRate,
       COUNT(I.Id) AS ItemCount, Q.BaseTotalAmount AS TotalPrice, Q.BaseTotalAmount,
       ProjectIds = (
           SELECT STRING_AGG(CAST(P.Id AS nvarchar(20)), ',')
           FROM projects.QuotationProjects QP
           INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
           WHERE QP.QuotationId = Q.Id
       ),
       ProjectLabel = (
           SELECT STRING_AGG(COALESCE(NULLIF(LTRIM(RTRIM(P.Reference)), N''), P.Name), N', ')
           FROM projects.QuotationProjects QP
           INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
           WHERE QP.QuotationId = Q.Id
       )
FROM projects.Quotations Q
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = Q.CurrencyId
LEFT JOIN projects.QuotationItems I ON I.QuotationId = Q.Id
WHERE Q.IsDeleted = 0
  AND S.IsDeleted = 0
  AND (
      NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
      OR EXISTS (
          SELECT 1
          FROM projects.QuotationProjects QP
          INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
          WHERE QP.QuotationId = Q.Id
      )
  )
GROUP BY Q.Id, Q.SupplierId, S.Name, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
         Q.QuotedDate, Q.ValidUntil,
         Q.CurrencyId, C.Code, C.Symbol, Q.ExchangeRate, Q.BaseTotalAmount
ORDER BY TRY_CONVERT(bigint, SUBSTRING(Q.Code, NULLIF(PATINDEX(N'%[0-9]%', Q.Code), 0), 20)) DESC,
         Q.Code DESC, Q.Id DESC", _connection);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(ReadListItem(reader));
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
SELECT Q.Id, Q.SupplierId, S.Name AS SupplierName, Q.Code, Q.SupplierReference, Q.CreatedAt, Q.ModifiedAt,
       Q.QuotedDate, Q.ValidUntil,
       Q.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, Q.ExchangeRate, Q.BaseTotalAmount
FROM projects.Quotations Q
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = Q.CurrencyId
WHERE Q.Id = @Id
  AND Q.IsDeleted = 0;

SELECT I.Id, COALESCE(NULLIF(LTRIM(RTRIM(I.DsnNo)), N''), A.DSNNo) AS DsnNo,
       CASE
           WHEN I.IsAlternativePart = 1 AND NULLIF(LTRIM(RTRIM(AP.AlternativePartNo)), N'') IS NOT NULL THEN AP.AlternativePartNo
           ELSE I.PartNo
       END AS PartNo,
       I.IsAlternativePart, I.PartDescription,
       I.Condition, I.SerialNo, I.TSN, I.TSO, I.CSN, I.CSO, U.Name AS Unit, I.LeadTime, I.MinimumQty,
       I.UnitPrice, I.Quantity, I.TotalPrice, I.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol,
       I.ExchangeRate, I.BaseUnitPrice, I.BaseTotalPrice, I.ActivityId, A.DSNNo AS ActivityDsn, A.PartNo AS ActivityPart,
       I.ActivityTypeId, AT.Name AS ActivityType
FROM projects.QuotationItems I
LEFT JOIN projects.Activities A ON A.Id = I.ActivityId
LEFT JOIN projects.ActivityAlternativeParts AP ON AP.Id = I.AlternativePartId
LEFT JOIN dbo.Currencies C ON C.Id = I.CurrencyId
LEFT JOIN dbo.Units U ON U.Id = I.UnitId
LEFT JOIN projects.ActivityTypes AT ON AT.Id = I.ActivityTypeId
WHERE I.QuotationId = @Id
ORDER BY I.Id;

SELECT Id, Name, Basis, Amount, Rate, BaseAmount, SortOrder
FROM projects.QuotationCharges
WHERE QuotationId = @Id
ORDER BY SortOrder, Id;", _connection);
            command.Parameters.AddInt("Id", quotationId);
            _connection.Open();
            try
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
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
                        QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                        ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                        CurrencyId = reader.ReadIntValue("CurrencyId"),
                        CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                        ExchangeRate = reader.GetDecimal(reader.GetOrdinal("ExchangeRate")),
                        BaseTotalAmount = reader.GetDecimal(reader.GetOrdinal("BaseTotalAmount")),
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
                            IsAlternativePart = reader.ReadBooleanValue("IsAlternativePart"),
                            Description = reader.ReadStringValue("PartDescription"),
                            Condition = reader.ReadStringValue("Condition"),
                            SerialNo = reader.ReadStringValue("SerialNo"),
                            TSN = reader.ReadStringValue("TSN"),
                            TSO = reader.ReadStringValue("TSO"),
                            CSN = reader.ReadStringValue("CSN"),
                            CSO = reader.ReadStringValue("CSO"),
                            Unit = reader.ReadStringValue("Unit"),
                            LeadTime = reader.ReadNullableIntValue("LeadTime"),
                            MinimumQty = reader.ReadNullableIntValue("MinimumQty"),
                            UnitPrice = reader.ReadNullableDecimalValue("UnitPrice"),
                            Quantity = reader.ReadIntValue("Quantity"),
                            TotalPrice = reader.ReadNullableDecimalValue("TotalPrice"),
                            ActivityId = reader.ReadNullableIntValue("ActivityId"),
                            MappedActivity = string.IsNullOrWhiteSpace(mapped) ? null : mapped,
                            CurrencyId = reader.ReadNullableIntValue("CurrencyId"),
                            CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                            CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                            ExchangeRate = reader.GetDecimal(reader.GetOrdinal("ExchangeRate")),
                            BaseUnitPrice = reader.ReadNullableDecimalValue("BaseUnitPrice"),
                            BaseTotalPrice = reader.ReadNullableDecimalValue("BaseTotalPrice"),
                            ActivityTypeId = reader.ReadNullableIntValue("ActivityTypeId"),
                            ActivityType = reader.ReadStringValue("ActivityType"),
                        });
                    }

                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.Charges.Add(new QuotationChargeResult
                            {
                                Id = reader.ReadIntValue("Id"),
                                Name = reader.ReadStringValue("Name"),
                                Basis = reader.ReadStringValue("Basis"),
                                Amount = reader.ReadNullableDecimalValue("Amount"),
                                Rate = reader.ReadNullableDecimalValue("Rate"),
                                BaseAmount = reader.GetDecimal(reader.GetOrdinal("BaseAmount")),
                                SortOrder = reader.ReadIntValue("SortOrder"),
                            });
                        }
                    }

                    detail.ItemCount = detail.Items.Count;
                    detail.TotalPrice = detail.BaseTotalAmount;
                }
                }

                if (detail != null)
            {
                using var links = new SqlCommand(@"
SELECT P.Id,
       Label = COALESCE(NULLIF(LTRIM(RTRIM(P.Reference)), N''), P.Name)
FROM projects.QuotationProjects QP
INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
WHERE QP.QuotationId = @Id
ORDER BY Label, P.Id", _connection);
                links.Parameters.AddInt("Id", quotationId);
                using var linkReader = await links.ExecuteReaderAsync();
                var labels = new List<string>();
                while (await linkReader.ReadAsync())
                {
                    detail.ProjectIds.Add(linkReader.ReadIntValue("Id"));
                    var label = linkReader.ReadStringValue("Label");
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        labels.Add(label);
                    }
                }

                detail.ProjectLabel = labels.Count == 0 ? null : string.Join(", ", labels);
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
WITH ProjectActivities AS (
    SELECT A.Id,
           DsnKey = LOWER(LTRIM(RTRIM(A.DSNNo))),
           PartKey = LOWER(LTRIM(RTRIM(A.PartNo)))
    FROM projects.Activities A
    WHERE A.ProjectId = @ProjectId
      AND A.IsDeleted = 0
),
DsnCounts AS (
    SELECT DsnKey, COUNT(*) AS Cnt
    FROM ProjectActivities
    WHERE NULLIF(DsnKey, N'') IS NOT NULL
    GROUP BY DsnKey
),
AltParts AS (
    SELECT AP.ActivityId,
           MainKey = LOWER(LTRIM(RTRIM(AP.MainPartNo))),
           AltKey = LOWER(LTRIM(RTRIM(AP.AlternativePartNo)))
    FROM projects.ActivityAlternativeParts AP
    INNER JOIN ProjectActivities A ON A.Id = AP.ActivityId
),
AltMatchKeys AS (
    SELECT DISTINCT ActivityId, PartKey
    FROM (
        SELECT ActivityId, MainKey AS PartKey FROM AltParts
        UNION ALL
        SELECT ActivityId, AltKey FROM AltParts
    ) Keys
),
AltOnlyKeys AS (
    SELECT DISTINCT ActivityId, AltKey
    FROM AltParts
    WHERE AltKey <> MainKey
),
ProjectItems AS (
    SELECT Q.SupplierId, Q.Id AS QuotationId, I.Id AS ItemId, Q.Code AS QuotationRef, S.Name AS SupplierName,
           Q.SupplierReference AS SupplierQuotationRef, I.PartNo, I.PartDescription AS Description,
           I.SerialNo, I.TSN, I.TSO, I.CSN, I.CSO,
           I.Condition, U.Name AS Unit, I.LeadTime, I.MinimumQty, I.Quantity, Q.QuotedDate, Q.ValidUntil,
           I.UnitPrice, I.TotalPrice,
           COALESCE(I.BaseUnitPrice, I.UnitPrice) AS BaseUnitPrice,
           COALESCE(I.BaseTotalPrice, I.TotalPrice) AS BaseTotalPrice,
           C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol,
           RecordedAt = (SELECT MAX(v) FROM (VALUES (I.CreatedAt), (I.ModifiedAt), (Q.CreatedAt), (Q.ModifiedAt)) AS Stamp(v)),
           DsnKey = LOWER(LTRIM(RTRIM(I.DsnNo))),
           PartKey = LOWER(LTRIM(RTRIM(I.PartNo)))
    FROM projects.QuotationProjects QP
    INNER JOIN projects.Quotations Q ON Q.Id = QP.QuotationId
    INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
    INNER JOIN projects.QuotationItems I ON I.QuotationId = Q.Id
    LEFT JOIN dbo.Currencies C ON C.Id = I.CurrencyId
    LEFT JOIN dbo.Units U ON U.Id = I.UnitId
    WHERE QP.ProjectId = @ProjectId
      AND Q.IsDeleted = 0
      AND S.IsDeleted = 0
      AND I.UnitPrice IS NOT NULL
),
DsnMatched AS (
    SELECT A.Id AS ActivityId, I.SupplierId, I.QuotationId, I.ItemId, I.QuotationRef, I.SupplierName, I.SupplierQuotationRef,
           I.PartNo, I.Description, I.SerialNo, I.TSN, I.TSO, I.CSN, I.CSO, I.Condition, I.Unit, I.LeadTime, I.MinimumQty, I.Quantity, I.QuotedDate, I.ValidUntil,
           I.UnitPrice, I.TotalPrice, I.BaseUnitPrice, I.BaseTotalPrice, I.CurrencyCode, I.CurrencySymbol, I.RecordedAt,
           CASE WHEN AF.ActivityId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS AlternativePart
    FROM ProjectActivities A
    INNER JOIN ProjectItems I
        ON I.DsnKey = A.DsnKey
       AND NULLIF(A.DsnKey, N'') IS NOT NULL
       AND NULLIF(I.DsnKey, N'') IS NOT NULL
    INNER JOIN DsnCounts DC ON DC.DsnKey = A.DsnKey
    LEFT JOIN AltMatchKeys AK ON AK.ActivityId = A.Id AND AK.PartKey = I.PartKey
    LEFT JOIN AltOnlyKeys AF ON AF.ActivityId = A.Id AND AF.AltKey = I.PartKey
    WHERE DC.Cnt = 1
       OR I.PartKey = A.PartKey
       OR AK.ActivityId IS NOT NULL
),
AltMatched AS (
    SELECT A.Id AS ActivityId, I.SupplierId, I.QuotationId, I.ItemId, I.QuotationRef, I.SupplierName, I.SupplierQuotationRef,
           I.PartNo, I.Description, I.SerialNo, I.TSN, I.TSO, I.CSN, I.CSO, I.Condition, I.Unit, I.LeadTime, I.MinimumQty, I.Quantity, I.QuotedDate, I.ValidUntil,
           I.UnitPrice, I.TotalPrice, I.BaseUnitPrice, I.BaseTotalPrice, I.CurrencyCode, I.CurrencySymbol, I.RecordedAt,
           CASE WHEN AF.ActivityId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS AlternativePart
    FROM ProjectActivities A
    INNER JOIN (SELECT DISTINCT ActivityId, AltKey FROM AltParts) AP ON AP.ActivityId = A.Id
    INNER JOIN ProjectItems I ON I.PartKey = AP.AltKey
    LEFT JOIN DsnCounts ItemDsn ON ItemDsn.DsnKey = I.DsnKey AND NULLIF(I.DsnKey, N'') IS NOT NULL
    LEFT JOIN AltOnlyKeys AF ON AF.ActivityId = A.Id AND AF.AltKey = I.PartKey
    WHERE NULLIF(I.DsnKey, N'') IS NULL
       OR I.DsnKey = A.DsnKey
       OR ItemDsn.DsnKey IS NULL
),
Matched AS (
    SELECT * FROM DsnMatched
    UNION
    SELECT * FROM AltMatched
),
Ranked AS (
    SELECT *,
           ROW_NUMBER() OVER (PARTITION BY ActivityId ORDER BY BaseUnitPrice ASC, QuotationId ASC, ItemId ASC) AS QuoteRank
    FROM Matched
)
SELECT ActivityId, SupplierId, ItemId, QuotationId, QuotationRef, SupplierName, SupplierQuotationRef, PartNo, Description, SerialNo, TSN, TSO, CSN, CSO, Condition, Unit, LeadTime, MinimumQty, Quantity, QuotedDate, ValidUntil, AlternativePart,
       UnitPrice, TotalPrice, CurrencyCode, CurrencySymbol, BaseUnitPrice, BaseTotalPrice, RecordedAt,
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
                        SupplierId = reader.ReadIntValue("SupplierId"),
                        ItemId = reader.ReadIntValue("ItemId"),
                        QuotationId = reader.ReadIntValue("QuotationId"),
                        QuotationRef = reader.ReadStringValue("QuotationRef"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        SupplierQuotationRef = reader.ReadStringValue("SupplierQuotationRef"),
                        PartNo = reader.ReadStringValue("PartNo"),
                        Description = reader.ReadStringValue("Description"),
                        SerialNo = reader.ReadStringValue("SerialNo"),
                        TSN = reader.ReadStringValue("TSN"),
                        TSO = reader.ReadStringValue("TSO"),
                        CSN = reader.ReadStringValue("CSN"),
                        CSO = reader.ReadStringValue("CSO"),
                        Condition = reader.ReadStringValue("Condition"),
                        Unit = reader.ReadStringValue("Unit"),
                        LeadTime = reader.ReadNullableIntValue("LeadTime"),
                        MinimumQty = reader.ReadNullableIntValue("MinimumQty"),
                        Quantity = reader.ReadIntValue("Quantity"),
                        QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                        ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                        AlternativePart = reader.ReadBooleanValue("AlternativePart"),
                        UnitPrice = reader.ReadNullableDecimalValue("UnitPrice"),
                        TotalPrice = reader.ReadNullableDecimalValue("TotalPrice"),
                        CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                        BaseUnitPrice = reader.ReadNullableDecimalValue("BaseUnitPrice"),
                        BaseTotalPrice = reader.ReadNullableDecimalValue("BaseTotalPrice"),
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

            await _connection.OpenAsync();
            SqlTransaction transaction = null;
            try
            {
                transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
                var updated = await ExecuteUpdateAsync(quotationId, args, transaction, CancellationToken.None);
                if (!updated)
                {
                    await RollbackQuietlyAsync(transaction);
                    return false;
                }

                await ReplaceChargesAsync(quotationId, args.Charges, transaction, CancellationToken.None);
                await ReplaceProjectLinksAsync(quotationId, args.ProjectIds, transaction, CancellationToken.None);
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await RollbackQuietlyAsync(transaction);
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }

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

        public async Task<IReadOnlyList<PartPriceHistoryItem>> GetPriceHistoryAsync(IReadOnlyCollection<string> partNos, IReadOnlyCollection<string> alternatePartNos)
        {
            var primary = DistinctPartKeys(partNos);
            var alternates = DistinctPartKeys(alternatePartNos).Where(part => !primary.Contains(part)).ToList();
            var keys = primary.Concat(alternates).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (keys.Length == 0)
            {
                return Array.Empty<PartPriceHistoryItem>();
            }

            var alternateKeys = new HashSet<string>(alternates, StringComparer.OrdinalIgnoreCase);
            var items = new List<PartPriceHistoryItem>();
            using var command = new SqlCommand(@"
SELECT I.Id, I.QuotationId, Q.Code AS QuotationReference, S.Name AS SupplierName,
       Q.SupplierReference AS SupplierQuotationRef,
       I.PartNo, I.PartDescription, I.SerialNo, I.TSN, I.TSO, I.CSN, I.CSO, I.Condition, U.Name AS Unit, I.LeadTime,
       C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol,
       I.UnitPrice, I.Quantity, I.TotalPrice, Q.QuotedDate, Q.ValidUntil, I.CreatedAt,
       ProjectIds = (
           SELECT STRING_AGG(CAST(P.Id AS nvarchar(20)), ',') WITHIN GROUP (ORDER BY P.Id)
           FROM projects.QuotationProjects QP
           INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
           WHERE QP.QuotationId = Q.Id
       ),
       ProjectLabel = (
           SELECT STRING_AGG(COALESCE(NULLIF(LTRIM(RTRIM(P.Reference)), N''), P.Name), N', ') WITHIN GROUP (ORDER BY P.Id)
           FROM projects.QuotationProjects QP
           INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
           WHERE QP.QuotationId = Q.Id
       )
FROM projects.QuotationItems I
INNER JOIN projects.Quotations Q ON Q.Id = I.QuotationId
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId
INNER JOIN (
    SELECT DISTINCT PartKey = LOWER(LTRIM(RTRIM(TextValue)))
    FROM @PartNos
) Parts ON Parts.PartKey = LOWER(LTRIM(RTRIM(I.PartNo)))
LEFT JOIN dbo.Currencies C ON C.Id = I.CurrencyId
LEFT JOIN dbo.Units U ON U.Id = I.UnitId
WHERE Q.IsDeleted = 0
  AND S.IsDeleted = 0
  AND I.UnitPrice IS NOT NULL
  AND (
      NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
      OR EXISTS (
          SELECT 1
          FROM projects.QuotationProjects QP
          INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
          WHERE QP.QuotationId = Q.Id
      )
  )
ORDER BY COALESCE(Q.QuotedDate, I.CreatedAt) DESC, I.Id DESC", _connection);
            command.Parameters.AddTable("PartNos", keys.ToTextDatatableArgs(), "dbo.TextTableType");
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var partNo = reader.ReadStringValue("PartNo");
                    items.Add(new PartPriceHistoryItem
                    {
                        Id = reader.ReadIntValue("Id"),
                        QuotationId = reader.ReadIntValue("QuotationId"),
                        QuotationReference = reader.ReadStringValue("QuotationReference"),
                        SupplierName = reader.ReadStringValue("SupplierName"),
                        SupplierQuotationRef = reader.ReadStringValue("SupplierQuotationRef"),
                        PartNo = partNo,
                        PartDescription = reader.ReadStringValue("PartDescription"),
                        SerialNo = reader.ReadStringValue("SerialNo"),
                        TSN = reader.ReadStringValue("TSN"),
                        TSO = reader.ReadStringValue("TSO"),
                        CSN = reader.ReadStringValue("CSN"),
                        CSO = reader.ReadStringValue("CSO"),
                        Condition = reader.ReadStringValue("Condition"),
                        Unit = reader.ReadStringValue("Unit"),
                        LeadTime = reader.ReadNullableIntValue("LeadTime"),
                        CurrencyCode = reader.ReadStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadStringValue("CurrencySymbol"),
                        ProjectIds = ParseIds(reader.ReadStringValue("ProjectIds")),
                        ProjectLabel = reader.ReadStringValue("ProjectLabel"),
                        Alternative = alternateKeys.Contains(partNo?.Trim() ?? string.Empty),
                        UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                        Quantity = reader.ReadIntValue("Quantity"),
                        TotalPrice = reader.ReadNullableDecimalValue("TotalPrice") ?? 0m,
                        QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                        ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
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

        private static List<string> DistinctPartKeys(IReadOnlyCollection<string> values)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return keys;
            }

            foreach (var value in values)
            {
                var key = value?.Trim();
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                {
                    continue;
                }

                keys.Add(key);
            }

            return keys;
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

        private async Task ReplaceChargesAsync(int quotationId, IReadOnlyList<QuotationChargeWrite> charges, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            if (charges != null)
            {
                using (var delete = new SqlCommand("DELETE FROM projects.QuotationCharges WHERE QuotationId = @QuotationId", _connection, transaction))
                {
                    delete.Parameters.AddInt("QuotationId", quotationId);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (var charge in charges.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name)))
                {
                    using var insert = new SqlCommand(@"
INSERT INTO projects.QuotationCharges (QuotationId, Name, Basis, Amount, Rate, BaseAmount, SortOrder)
SELECT @QuotationId, @Name, N'Amount', @Amount, NULL, ROUND(@Amount * Q.ExchangeRate, 2), @SortOrder
FROM projects.Quotations Q
WHERE Q.Id = @QuotationId;", _connection, transaction);
                    insert.Parameters.AddInt("QuotationId", quotationId);
                    insert.Parameters.AddNVarChar("Name", charge.Name.Trim(), 200, false);
                    insert.Parameters.AddNullableDecimal("Amount", charge.Amount, 18, 2);
                    insert.Parameters.AddInt("SortOrder", charge.SortOrder);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            using var update = new SqlCommand(@"
UPDATE projects.Quotations
SET BaseTotalAmount = ROUND(
    ISNULL((SELECT SUM(BaseTotalPrice) FROM projects.QuotationItems WHERE QuotationId = @QuotationId), 0)
    + ISNULL((SELECT SUM(BaseAmount) FROM projects.QuotationCharges WHERE QuotationId = @QuotationId), 0), 2)
WHERE Id = @QuotationId;", _connection, transaction);
            update.Parameters.AddInt("QuotationId", quotationId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<int> ExecuteCreateAsync(CreateQuotationArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand("dbo.CreateQuotation", _connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            BindCreate(command, args);
            var id = await command.ExecuteScalarAsync(cancellationToken);
            return id == null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
        }

        private async Task<bool> ExecuteCreateDocumentAsync(CreateQuotationDocumentArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand("dbo.CreateQuotationDocument", _connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("QuotationId", args.QuotationId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
            command.Parameters.AddBigInt("FileSize", args.FileSize);
            command.Parameters.AddBit("IsOriginal", args.IsOriginal);
            command.Parameters.AddNullableInt("SortOrder", args.SortOrder);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken);
        }

        private async Task LinkUploadedFileAsync(int quotationId, Func<int, CancellationToken, Task<CreateQuotationDocumentArgs>> attachFile, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            var document = await attachFile(quotationId, cancellationToken);
            if (document == null)
            {
                return;
            }

            document.QuotationId = quotationId;
            var linked = document is UpdateQuotationDocumentArgs update && update.DocumentId > 0
                ? await ExecuteUpdateDocumentAsync(update, transaction, cancellationToken)
                : await ExecuteCreateDocumentAsync(document, transaction, cancellationToken);
            if (!linked)
            {
                throw new InvalidOperationException("The quotation file could not be saved.");
            }
        }

        private async Task<bool> ExecuteUpdateAsync(int quotationId, CreateQuotationArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand("dbo.ReplaceQuotationItems", _connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddInt("Id", quotationId);
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.Add(OptionalText("SupplierReference", NullIfBlank(args.SupplierReference), 100));
            command.Parameters.AddNullableInt("CurrencyId", args.CurrencyId);
            command.Parameters.AddNullableDateTime("QuotedDate", args.QuotedDate);
            command.Parameters.AddNullableDateTime("ValidUntil", args.ValidUntil);
            command.Parameters.AddTable("Items", ItemTable(args.Items), "dbo.DefaultGenericTableType");
            var updated = await command.ExecuteScalarAsync(cancellationToken);
            return updated != null && updated != DBNull.Value && Convert.ToInt32(updated) > 0;
        }

        private async Task<bool> ExecuteUpdateDocumentAsync(UpdateQuotationDocumentArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand("dbo.UpdateQuotationDocument", _connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("QuotationId", args.QuotationId);
            command.Parameters.AddInt("DocumentId", args.DocumentId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
            command.Parameters.AddBigInt("FileSize", args.FileSize);
            command.Parameters.AddBit("IsOriginal", args.IsOriginal);
            command.Parameters.AddNullableInt("SortOrder", args.SortOrder);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken);
        }

        private async Task ReplaceProjectLinksAsync(int quotationId, IReadOnlyList<int> projectIds, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand(@"
DELETE FROM projects.QuotationProjects
WHERE QuotationId = @Id;

INSERT INTO projects.QuotationProjects (QuotationId, ProjectId)
SELECT @Id, P.Id
FROM projects.Projects AS P
INNER JOIN @ProjectIds AS Ids ON Ids.NumericID = P.Id
WHERE P.IsDeleted = 0;", _connection, transaction);
            command.Parameters.AddInt("Id", quotationId);
            command.Parameters.AddTable("ProjectIds", ProjectIdTable(projectIds), "dbo.NumberTableType");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void BindCreate(SqlCommand command, CreateQuotationArgs args)
        {
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.Add(OptionalText("SupplierReference", NullIfBlank(args.SupplierReference), 100));
            command.Parameters.Add(OptionalText("DriveFileLink", NullIfBlank(args.DriveFileLink), 1000));
            command.Parameters.AddNullableInt("CurrencyId", args.CurrencyId);
            command.Parameters.AddNullableDateTime("QuotedDate", args.QuotedDate);
            command.Parameters.AddNullableDateTime("ValidUntil", args.ValidUntil);
            command.Parameters.AddTable("ProjectIds", ProjectIdTable(args.ProjectIds), "dbo.NumberTableType");
            command.Parameters.AddTable("Items", ItemTable(args.Items), "dbo.DefaultGenericTableType");
        }

        private static async Task RollbackQuietlyAsync(SqlTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }

            try
            {
                await transaction.RollbackAsync();
            }
            catch (InvalidOperationException)
            {
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
                    TextValue4 = item.Condition,
                    TextValue5 = item.LeadTime?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    TextValue6 = item.Unit,
                    TextValue7 = UsageJson(item),
                    NumericValue5 = item.CurrencyId,
                    NumericValue6 = item.MinimumQty,
                    NumericValue7 = item.ActivityTypeId,
                    DecimalValue1 = item.UnitPrice,
                    DecimalValue2 = item.TotalPrice
                })
                .ToList();
            return rows.ToGenericDatatableNullableArgs();
        }

        private static string UsageJson(QuotationItemWrite item)
        {
            var serialNo = NullIfBlank(item?.SerialNo);
            var tsn = NullIfBlank(item?.TSN);
            var tso = NullIfBlank(item?.TSO);
            var csn = NullIfBlank(item?.CSN);
            var cso = NullIfBlank(item?.CSO);
            if (serialNo == null && tsn == null && tso == null && csn == null && cso == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(new
            {
                SerialNo = serialNo,
                TSN = tsn,
                TSO = tso,
                CSN = csn,
                CSO = cso,
            });
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
