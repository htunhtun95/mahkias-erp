using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class DashboardRepository : AdoRepository<DashboardEntity>, IDashboardRepository
    {
        public DashboardRepository(string connectionString) : base(connectionString)
        {
        }

        public override DashboardEntity PopulateRecord(SqlDataReader reader) => new();

        public async Task<DashboardSearchResult> SearchAsync(DashboardSearchArgs args)
        {
            var result = new DashboardSearchResult();
            var purchaseOrders = IsPurchaseOrders(args?.Dataset);
            var status = NormalizeStatus(purchaseOrders, args?.Status);
            var pageNumber = args?.PageNumber < 1 ? 1 : args?.PageNumber ?? 1;
            var pageSize = args?.PageSize is 10 or 20 or 50 or 100 ? args.PageSize : 10;
            var offset = (pageNumber - 1) * pageSize;
            var from = args?.From?.Date;
            var toExclusive = args?.To?.Date.AddDays(1);

            using var command = new SqlCommand(purchaseOrders ? PurchaseOrderSql : QuotationSql, _connection);
            command.Parameters.AddNVarChar("Status", status, 20);
            command.Parameters.AddNullableDateTime("From", from);
            command.Parameters.AddNullableDateTime("ToExclusive", toExclusive);
            command.Parameters.AddInt("Offset", offset);
            command.Parameters.AddInt("PageSize", pageSize);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.ProjectTotal = reader.ReadIntValue("ProjectTotal");
                    result.ActiveProjectCount = reader.ReadIntValue("ActiveProjectCount");
                    result.SupplierTotal = reader.ReadIntValue("SupplierTotal");
                    result.QuotationCount = reader.ReadIntValue("QuotationCount");
                    result.QuotedValue = reader.ReadDecimalValue("QuotedValue");
                    result.OpenPurchaseOrderCount = reader.ReadIntValue("OpenPurchaseOrderCount");
                }

                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.TotalCount = reader.ReadIntValue("TotalCount");
                }

                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Items.Add(new DashboardTrackRow
                        {
                            Id = reader.ReadIntValue("Id"),
                            Dataset = purchaseOrders ? "purchase-orders" : "quotations",
                            Status = reader.ReadStringValue("Status"),
                            Code = reader.ReadStringValue("Code"),
                            SupplierId = reader.ReadIntValue("SupplierId"),
                            SupplierName = reader.ReadStringValue("SupplierName"),
                            SupplierReference = reader.ReadStringValue("SupplierReference"),
                            ProjectId = reader.ReadIntValue("ProjectId"),
                            ProjectName = reader.ReadStringValue("ProjectName"),
                            ProjectReference = reader.ReadStringValue("ProjectReference"),
                            ItemCount = reader.ReadIntValue("ItemCount"),
                            BaseTotalAmount = reader.ReadDecimalValue("BaseTotalAmount"),
                            UpdatedAt = reader.ReadNullableDateTimeValue("UpdatedAt"),
                            CreatedAt = reader.ReadNullableDateTimeValue("CreatedAt"),
                            ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                        });
                    }
                }
            }
            finally
            {
                _connection.Close();
            }

            return result;
        }

        private static bool IsPurchaseOrders(string dataset)
        {
            return string.Equals(dataset?.Trim(), "purchase-orders", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatus(bool purchaseOrders, string status)
        {
            var value = (status ?? string.Empty).Trim().ToLowerInvariant();
            if (purchaseOrders)
            {
                return value is "draft" or "sent" or "revised" or "cancelled" ? value : string.Empty;
            }

            return value is "pending" or "processing" ? value : string.Empty;
        }

        private const string SummarySql = @"
SELECT
    (SELECT COUNT(*) FROM projects.Projects WHERE IsDeleted = 0) AS ProjectTotal,
    (SELECT COUNT(*) FROM projects.Suppliers WHERE IsDeleted = 0) AS SupplierTotal,
    (SELECT COUNT(*)
     FROM projects.Projects P
     WHERE P.IsDeleted = 0
       AND (
            EXISTS (
                SELECT 1
                FROM projects.QuotationProjects QP
                INNER JOIN projects.Quotations Q ON Q.Id = QP.QuotationId AND Q.IsDeleted = 0
                INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId AND S.IsDeleted = 0
                WHERE QP.ProjectId = P.Id)
            OR EXISTS (
                SELECT 1
                FROM projects.PurchaseOrderProjects PP
                INNER JOIN projects.PurchaseOrders O ON O.Id = PP.PurchaseOrderId AND O.IsDeleted = 0
                INNER JOIN projects.Suppliers S ON S.Id = O.SupplierId AND S.IsDeleted = 0
                INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = O.StatusId
                WHERE PP.ProjectId = P.Id
                  AND ST.Code <> N'Cancelled'))) AS ActiveProjectCount,
    (SELECT COUNT(*)
     FROM projects.Quotations Q
     INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId AND S.IsDeleted = 0
     WHERE Q.IsDeleted = 0
       AND (
            NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
            OR EXISTS (
                SELECT 1
                FROM projects.QuotationProjects QP
                INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
                WHERE QP.QuotationId = Q.Id))
       AND (@From IS NULL OR Q.CreatedAt >= @From)
       AND (@ToExclusive IS NULL OR Q.CreatedAt < @ToExclusive)) AS QuotationCount,
    (SELECT ISNULL(SUM(Q.BaseTotalAmount), 0)
     FROM projects.Quotations Q
     INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId AND S.IsDeleted = 0
     WHERE Q.IsDeleted = 0
       AND (
            NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
            OR EXISTS (
                SELECT 1
                FROM projects.QuotationProjects QP
                INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
                WHERE QP.QuotationId = Q.Id))
       AND (@From IS NULL OR Q.CreatedAt >= @From)
       AND (@ToExclusive IS NULL OR Q.CreatedAt < @ToExclusive)) AS QuotedValue,
    (SELECT COUNT(*)
     FROM projects.PurchaseOrders O
     INNER JOIN projects.Suppliers S ON S.Id = O.SupplierId AND S.IsDeleted = 0
     INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = O.StatusId
     WHERE O.IsDeleted = 0
       AND ST.Code <> N'Cancelled'
       AND (
            NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderProjects PP WHERE PP.PurchaseOrderId = O.Id)
            OR EXISTS (
                SELECT 1
                FROM projects.PurchaseOrderProjects PP
                INNER JOIN projects.Projects P ON P.Id = PP.ProjectId AND P.IsDeleted = 0
                WHERE PP.PurchaseOrderId = O.Id))
       AND (@From IS NULL OR O.PoDate >= @From)
       AND (@ToExclusive IS NULL OR O.PoDate < @ToExclusive)) AS OpenPurchaseOrderCount;
";

        private const string QuotationSql = SummarySql + @"
SELECT COUNT(*) AS TotalCount
FROM projects.Quotations Q
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId AND S.IsDeleted = 0
WHERE Q.IsDeleted = 0
  AND (
        NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
        OR EXISTS (
            SELECT 1
            FROM projects.QuotationProjects QP
            INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
            WHERE QP.QuotationId = Q.Id))
  AND (@From IS NULL OR Q.CreatedAt >= @From)
  AND (@ToExclusive IS NULL OR Q.CreatedAt < @ToExclusive)
  AND (
        @Status = N''
        OR (@Status = N'pending' AND NOT EXISTS (SELECT 1 FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id))
        OR (@Status = N'processing' AND EXISTS (SELECT 1 FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id)));

SELECT Q.Id,
       CASE WHEN EXISTS (SELECT 1 FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id) THEN N'processing' ELSE N'pending' END AS Status,
       Q.Code,
       Q.SupplierId,
       S.Name AS SupplierName,
       Q.SupplierReference,
       Linked.Id AS ProjectId,
       Linked.Name AS ProjectName,
       Linked.Reference AS ProjectReference,
       (SELECT COUNT(*) FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id) AS ItemCount,
       Q.BaseTotalAmount,
       COALESCE(Q.ModifiedAt, Q.CreatedAt) AS UpdatedAt,
       Q.CreatedAt,
       Q.ValidUntil
FROM projects.Quotations Q
INNER JOIN projects.Suppliers S ON S.Id = Q.SupplierId AND S.IsDeleted = 0
OUTER APPLY (
    SELECT TOP 1 P.Id, P.Name, P.Reference
    FROM projects.QuotationProjects QP
    INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
    WHERE QP.QuotationId = Q.Id
    ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(P.Name)), N'') IS NULL THEN 1 ELSE 0 END, P.Name, P.Id
) Linked
WHERE Q.IsDeleted = 0
  AND (
        NOT EXISTS (SELECT 1 FROM projects.QuotationProjects QP WHERE QP.QuotationId = Q.Id)
        OR EXISTS (
            SELECT 1
            FROM projects.QuotationProjects QP
            INNER JOIN projects.Projects P ON P.Id = QP.ProjectId AND P.IsDeleted = 0
            WHERE QP.QuotationId = Q.Id))
  AND (@From IS NULL OR Q.CreatedAt >= @From)
  AND (@ToExclusive IS NULL OR Q.CreatedAt < @ToExclusive)
  AND (
        @Status = N''
        OR (@Status = N'pending' AND NOT EXISTS (SELECT 1 FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id))
        OR (@Status = N'processing' AND EXISTS (SELECT 1 FROM projects.QuotationItems I WHERE I.QuotationId = Q.Id)))
ORDER BY COALESCE(Q.ModifiedAt, Q.CreatedAt) DESC, Q.Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
";

        private const string PurchaseOrderSql = SummarySql + @"
SELECT COUNT(*) AS TotalCount
FROM projects.PurchaseOrders O
INNER JOIN projects.Suppliers S ON S.Id = O.SupplierId AND S.IsDeleted = 0
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = O.StatusId
WHERE O.IsDeleted = 0
  AND (
        NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderProjects PP WHERE PP.PurchaseOrderId = O.Id)
        OR EXISTS (
            SELECT 1
            FROM projects.PurchaseOrderProjects PP
            INNER JOIN projects.Projects P ON P.Id = PP.ProjectId AND P.IsDeleted = 0
            WHERE PP.PurchaseOrderId = O.Id))
  AND (@From IS NULL OR O.PoDate >= @From)
  AND (@ToExclusive IS NULL OR O.PoDate < @ToExclusive)
  AND (@Status = N'' OR LOWER(ST.Code) = @Status);

SELECT O.Id,
       LOWER(ST.Code) AS Status,
       O.Code,
       O.SupplierId,
       S.Name AS SupplierName,
       CAST(N'' AS NVARCHAR(100)) AS SupplierReference,
       Linked.Id AS ProjectId,
       Linked.Name AS ProjectName,
       Linked.Reference AS ProjectReference,
       (SELECT COUNT(*) FROM projects.PurchaseOrderItems I WHERE I.PurchaseOrderId = O.Id) AS ItemCount,
       O.BaseTotalAmount,
       O.PoDate AS UpdatedAt,
       O.PoDate AS CreatedAt,
       CAST(NULL AS DATETIME) AS ValidUntil
FROM projects.PurchaseOrders O
INNER JOIN projects.Suppliers S ON S.Id = O.SupplierId AND S.IsDeleted = 0
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = O.StatusId
OUTER APPLY (
    SELECT TOP 1 P.Id, P.Name, P.Reference
    FROM projects.PurchaseOrderProjects PP
    INNER JOIN projects.Projects P ON P.Id = PP.ProjectId AND P.IsDeleted = 0
    WHERE PP.PurchaseOrderId = O.Id
    ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(P.Name)), N'') IS NULL THEN 1 ELSE 0 END, P.Name, P.Id
) Linked
WHERE O.IsDeleted = 0
  AND (
        NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderProjects PP WHERE PP.PurchaseOrderId = O.Id)
        OR EXISTS (
            SELECT 1
            FROM projects.PurchaseOrderProjects PP
            INNER JOIN projects.Projects P ON P.Id = PP.ProjectId AND P.IsDeleted = 0
            WHERE PP.PurchaseOrderId = O.Id))
  AND (@From IS NULL OR O.PoDate >= @From)
  AND (@ToExclusive IS NULL OR O.PoDate < @ToExclusive)
  AND (@Status = N'' OR LOWER(ST.Code) = @Status)
ORDER BY O.PoDate DESC, O.Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
";
    }
}
