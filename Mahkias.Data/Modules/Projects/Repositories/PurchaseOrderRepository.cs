using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class PurchaseOrderRepository : AdoRepository<PurchaseOrderEntity>, IPurchaseOrderRepository
    {
        public PurchaseOrderRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<PurchaseOrderStatusResult>> GetStatusesAsync()
        {
            var items = new List<PurchaseOrderStatusResult>();
            using var command = new SqlCommand(@"
SELECT Id, Name, Code, SortOrder
FROM dbo.PurchaseOrderStatuses
ORDER BY SortOrder, Id;", _connection);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new PurchaseOrderStatusResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Name = reader.ReadNullableStringValue("Name"),
                        Code = reader.ReadNullableStringValue("Code"),
                        SortOrder = reader.ReadIntValue("SortOrder"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public async Task<IReadOnlyList<PurchaseOrderListItem>> GetByProjectAsync(int projectId)
        {
            var items = new List<PurchaseOrderListItem>();
            using var command = new SqlCommand(@"
SELECT P.Id, P.SupplierId, S.Name AS SupplierName, P.Code, P.PoDate, P.ShipToAddress,
       P.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, P.ExchangeRate,
       P.SubTotal, P.BaseSubTotal, P.Total, P.BaseTotalAmount,
       ST.Id AS StatusId, ST.Code AS StatusCode, ST.Name AS StatusName,
       COUNT(I.Id) AS ItemCount
FROM projects.PurchaseOrders P
INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = P.Id
INNER JOIN projects.Suppliers S ON S.Id = P.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = P.CurrencyId
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = P.StatusId
LEFT JOIN projects.PurchaseOrderItems I ON I.PurchaseOrderId = P.Id
WHERE PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0
  AND S.IsDeleted = 0
GROUP BY P.Id, P.SupplierId, S.Name, P.Code, P.PoDate, P.ShipToAddress,
         P.CurrencyId, C.Code, C.Symbol, P.ExchangeRate,
         P.SubTotal, P.BaseSubTotal, P.Total, P.BaseTotalAmount,
         ST.Id, ST.Code, ST.Name
ORDER BY P.Id DESC;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
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

        public async Task<IReadOnlyList<PurchaseOrderTraceLine>> GetLinesByProjectAsync(int projectId)
        {
            var items = new List<PurchaseOrderTraceLine>();
            using var command = new SqlCommand(@"
SELECT P.Id AS PurchaseOrderId, P.Code, P.PoDate, S.Name AS SupplierName,
       ST.Code AS StatusCode, ST.Name AS StatusName,
       I.Id AS LineId, I.ActivityId, I.QuotationId, Q.Code AS QuotationCode,
       I.DsnNo, I.PartNo, I.PartDescription, I.Condition, I.Unit, I.LeadTime,
       I.Quantity, I.UnitPrice, I.TotalPrice, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol
FROM projects.PurchaseOrderItems I
INNER JOIN projects.PurchaseOrders P ON P.Id = I.PurchaseOrderId
INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = P.Id
INNER JOIN projects.Suppliers S ON S.Id = P.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = I.CurrencyId
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = P.StatusId
LEFT JOIN projects.Quotations Q ON Q.Id = I.QuotationId AND Q.IsDeleted = 0
WHERE PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0
  AND S.IsDeleted = 0
ORDER BY P.Id DESC, I.SortOrder, I.Id;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new PurchaseOrderTraceLine
                    {
                        PurchaseOrderId = reader.ReadIntValue("PurchaseOrderId"),
                        Code = reader.ReadNullableStringValue("Code"),
                        PoDate = reader.ReadNullableDateTimeValue("PoDate"),
                        SupplierName = reader.ReadNullableStringValue("SupplierName"),
                        LineId = reader.ReadIntValue("LineId"),
                        ActivityId = reader.ReadNullableIntValue("ActivityId"),
                        QuotationId = reader.ReadNullableIntValue("QuotationId"),
                        QuotationCode = reader.ReadNullableStringValue("QuotationCode"),
                        DsnNo = reader.ReadNullableStringValue("DsnNo"),
                        PartNo = reader.ReadNullableStringValue("PartNo"),
                        Description = reader.ReadNullableStringValue("PartDescription"),
                        Condition = reader.ReadNullableStringValue("Condition"),
                        Unit = reader.ReadNullableStringValue("Unit"),
                        LeadTime = reader.ReadNullableStringValue("LeadTime"),
                        Quantity = reader.ReadDecimalValue("Quantity"),
                        UnitPrice = reader.ReadDecimalValue("UnitPrice"),
                        TotalPrice = reader.ReadDecimalValue("TotalPrice"),
                        CurrencyCode = reader.ReadNullableStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadNullableStringValue("CurrencySymbol"),
                        StatusCode = reader.ReadNullableStringValue("StatusCode"),
                        StatusName = reader.ReadNullableStringValue("StatusName"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public async Task<PurchaseOrderDetailResult> GetDetailAsync(int purchaseOrderId)
        {
            using var command = new SqlCommand(@"
SELECT P.Id, P.SupplierId, S.Name AS SupplierName, P.Code, P.PoDate, P.ShipToAddress,
       P.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, P.ExchangeRate,
       P.SubTotal, P.BaseSubTotal, P.Total, P.BaseTotalAmount,
       ST.Id AS StatusId, ST.Code AS StatusCode, ST.Name AS StatusName,
       (SELECT COUNT(*) FROM projects.PurchaseOrderItems I WHERE I.PurchaseOrderId = P.Id) AS ItemCount
FROM projects.PurchaseOrders P
INNER JOIN projects.Suppliers S ON S.Id = P.SupplierId
INNER JOIN dbo.Currencies C ON C.Id = P.CurrencyId
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = P.StatusId
WHERE P.Id = @Id AND P.IsDeleted = 0;

SELECT I.Id, I.ActivityId, I.QuotationId, Q.Code AS QuotationCode, I.DsnNo, I.PartNo, I.PartDescription,
       I.Condition, I.Unit, I.LeadTime, I.Quantity, I.UnitPrice, I.TotalPrice,
       I.CurrencyId, C.Code AS CurrencyCode, C.Symbol AS CurrencySymbol, I.ExchangeRate,
       I.BaseUnitPrice, I.BaseTotalPrice, I.SortOrder
FROM projects.PurchaseOrderItems I
INNER JOIN dbo.Currencies C ON C.Id = I.CurrencyId
LEFT JOIN projects.Quotations Q ON Q.Id = I.QuotationId
WHERE I.PurchaseOrderId = @Id
ORDER BY I.SortOrder, I.Id;

SELECT Id, Name, Basis, Amount, Rate, BaseAmount, SortOrder
FROM projects.PurchaseOrderCharges
WHERE PurchaseOrderId = @Id
ORDER BY SortOrder, Id;

SELECT H.Id, H.Action, H.Summary, H.ActorName, H.ActorEmail, H.UserId, H.CreatedAt,
       ST.Code AS StatusCode, ST.Name AS StatusName
FROM projects.PurchaseOrderHistory H
LEFT JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = H.StatusId
WHERE H.PurchaseOrderId = @Id
ORDER BY H.CreatedAt DESC, H.Id DESC;", _connection);
            command.Parameters.AddInt("Id", purchaseOrderId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                var detail = new PurchaseOrderDetailResult();
                CopyListItem(ReadListItem(reader), detail);
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    detail.Items.Add(new PurchaseOrderItemResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        ActivityId = reader.ReadNullableIntValue("ActivityId"),
                        QuotationId = reader.ReadNullableIntValue("QuotationId"),
                        QuotationCode = reader.ReadNullableStringValue("QuotationCode"),
                        DsnNo = reader.ReadNullableStringValue("DsnNo"),
                        PartNo = reader.ReadNullableStringValue("PartNo"),
                        Description = reader.ReadNullableStringValue("PartDescription"),
                        Condition = reader.ReadNullableStringValue("Condition"),
                        Unit = reader.ReadNullableStringValue("Unit"),
                        LeadTime = reader.ReadNullableStringValue("LeadTime"),
                        Quantity = reader.ReadDecimalValue("Quantity"),
                        UnitPrice = reader.ReadDecimalValue("UnitPrice"),
                        TotalPrice = reader.ReadDecimalValue("TotalPrice"),
                        CurrencyId = reader.ReadIntValue("CurrencyId"),
                        CurrencyCode = reader.ReadNullableStringValue("CurrencyCode"),
                        CurrencySymbol = reader.ReadNullableStringValue("CurrencySymbol"),
                        ExchangeRate = reader.ReadDecimalValue("ExchangeRate"),
                        BaseUnitPrice = reader.ReadDecimalValue("BaseUnitPrice"),
                        BaseTotalPrice = reader.ReadDecimalValue("BaseTotalPrice"),
                        SortOrder = reader.ReadIntValue("SortOrder"),
                    });
                }

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    detail.Charges.Add(new PurchaseOrderChargeResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Name = reader.ReadNullableStringValue("Name"),
                        Basis = reader.ReadNullableStringValue("Basis"),
                        Amount = reader.ReadNullableDecimalValue("Amount"),
                        Rate = reader.ReadNullableDecimalValue("Rate"),
                        BaseAmount = reader.ReadDecimalValue("BaseAmount"),
                        SortOrder = reader.ReadIntValue("SortOrder"),
                    });
                }

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    detail.History.Add(new PurchaseOrderHistoryResult
                    {
                        Id = reader.ReadIntValue("Id"),
                        Action = reader.ReadNullableStringValue("Action"),
                        Summary = reader.ReadNullableStringValue("Summary"),
                        StatusCode = reader.ReadNullableStringValue("StatusCode"),
                        StatusName = reader.ReadNullableStringValue("StatusName"),
                        ActorName = reader.ReadNullableStringValue("ActorName"),
                        ActorEmail = reader.ReadNullableStringValue("ActorEmail"),
                        UserId = reader.ReadNullableIntValue("UserId"),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                    });
                }

                return detail;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<PurchaseOrderActivityMatch>> GetProjectActivitiesAsync(int projectId)
        {
            var items = new List<PurchaseOrderActivityMatch>();
            using var command = new SqlCommand(@"
SELECT Id, DSNNo, PartNo, Description
FROM projects.Activities
WHERE ProjectId = @ProjectId AND IsDeleted = 0;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new PurchaseOrderActivityMatch
                    {
                        Id = reader.ReadIntValue("Id"),
                        DsnNo = reader.ReadNullableStringValue("DSNNo"),
                        PartNo = reader.ReadNullableStringValue("PartNo"),
                        Description = reader.ReadNullableStringValue("Description"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public async Task<int> CreateAsync(CreatePurchaseOrderArgs args)
        {
            return await RunAsync(async (transaction, cancellationToken) =>
            {
                var id = await InsertNewAsync(args, transaction, cancellationToken);
                await ReplaceItemsAsync(id, ProjectId(args), args.Items, transaction, cancellationToken);
                await ReplaceChargesAsync(id, args.Charges, transaction, cancellationToken);
                return id;
            }, CancellationToken.None);
        }

        public async Task<int> ImportAsync(CreatePurchaseOrderArgs args, Func<int, CancellationToken, Task<CreatePurchaseOrderDocumentArgs>> attachFile, CancellationToken cancellationToken = default)
        {
            return await RunAsync(async (transaction, token) =>
            {
                var id = await InsertNewAsync(args, transaction, token);
                await ReplaceItemsAsync(id, ProjectId(args), args.Items, transaction, token);
                await ReplaceChargesAsync(id, args.Charges, transaction, token);
                if (attachFile != null)
                {
                    var document = await attachFile(id, token);
                    if (document != null)
                    {
                        document.PurchaseOrderId = id;
                        await InsertDocumentAsync(document, transaction, token);
                    }
                }

                return id;
            }, cancellationToken);
        }

        public async Task SaveExistingAsync(int purchaseOrderId, CreatePurchaseOrderArgs args, Func<int, CancellationToken, Task<CreatePurchaseOrderDocumentArgs>> attachFile, CancellationToken cancellationToken = default)
        {
            await RunAsync(async (transaction, token) =>
            {
                var status = await ResolveUpdateStatusAsync(purchaseOrderId, args, transaction, token);
                var updated = await UpdateHeaderAsync(purchaseOrderId, args, status.Id, transaction, token);
                if (!updated)
                {
                    throw new InvalidOperationException("The purchase order could not be saved.");
                }

                await InsertHistoryAsync(purchaseOrderId, status, args, transaction, token);
                await ReplaceItemsAsync(purchaseOrderId, ProjectId(args), args.Items, transaction, token);
                await ReplaceChargesAsync(purchaseOrderId, args.Charges, transaction, token);
                if (attachFile != null)
                {
                    var document = await attachFile(purchaseOrderId, token);
                    if (document != null)
                    {
                        document.PurchaseOrderId = purchaseOrderId;
                        await InsertDocumentAsync(document, transaction, token);
                    }
                }

                return purchaseOrderId;
            }, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int purchaseOrderId, string actorName, string actorEmail, int? actorUserId)
        {
            return await RunAsync(async (transaction, token) =>
            {
                var current = await CurrentStatusAsync(purchaseOrderId, transaction, token);
                if (current == null)
                {
                    return false;
                }

                await InsertHistoryAsync(purchaseOrderId, new PurchaseOrderStatusChoice
                {
                    Id = current.Id,
                    Code = current.Code,
                    Name = current.Name,
                    Action = "Deleted",
                    Summary = "Purchase order deleted.",
                }, actorName, actorEmail, actorUserId, transaction, token);

                using var command = new SqlCommand(@"
UPDATE projects.PurchaseOrders
SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), ModifiedAt = GETDATE()
WHERE Id = @Id AND IsDeleted = 0;", _connection, transaction);
                command.Parameters.AddInt("Id", purchaseOrderId);
                return await command.ExecuteNonQueryAsync(token) > 0;
            }, CancellationToken.None);
        }

        public async Task<string> NextCodeAsync()
        {
            using var command = new SqlCommand(@"
SELECT CASE
    WHEN EXISTS (SELECT 1 FROM projects.PurchaseOrders) THEN CAST(IDENT_CURRENT(N'projects.PurchaseOrders') AS INT) + 1
    ELSE 1
END", _connection);
            _connection.Open();
            try
            {
                var next = Convert.ToInt32(await command.ExecuteScalarAsync());
                return PurchaseOrderCode.Format(next);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<PurchaseOrderDocumentResult>> ListDocumentsAsync(int projectId, int purchaseOrderId)
        {
            var items = new List<PurchaseOrderDocumentResult>();
            using var command = new SqlCommand(@"
SELECT D.DocumentId, D.PurchaseOrderId, A.file_name AS FileName, A.file_type AS FileType, A.drive_file_id AS GoogleDriveFileId, A.file_url AS WebViewLink, A.created_at AS UploadedAt, A.file_size AS FileSize, A.sort_order AS SortOrder, D.IsOriginal, D.AssetId
FROM projects.PurchaseOrderDocuments D
INNER JOIN media.assets A ON A.id = D.AssetId
INNER JOIN projects.PurchaseOrders P ON P.Id = D.PurchaseOrderId
INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = P.Id
WHERE D.PurchaseOrderId = @PurchaseOrderId
  AND PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0
ORDER BY A.sort_order, A.created_at, D.DocumentId;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(ReadDocument(reader));
                }
            }
            finally
            {
                _connection.Close();
            }

            return items;
        }

        public async Task<bool> RemoveDocumentAsync(int projectId, int purchaseOrderId, int documentId)
        {
            using var command = new SqlCommand(@"
DECLARE @AssetId UNIQUEIDENTIFIER;
SELECT @AssetId = D.AssetId
FROM projects.PurchaseOrderDocuments D
INNER JOIN projects.PurchaseOrders P ON P.Id = D.PurchaseOrderId
INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = P.Id
WHERE D.DocumentId = @DocumentId
  AND D.PurchaseOrderId = @PurchaseOrderId
  AND PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0;

DELETE FROM projects.PurchaseOrderDocuments WHERE DocumentId = @DocumentId AND AssetId = @AssetId;
DECLARE @Deleted INT = @@ROWCOUNT;

IF @Deleted > 0 AND @AssetId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM projects.QuotationDocuments WHERE AssetId = @AssetId)
   AND NOT EXISTS (SELECT 1 FROM projects.PurchaseOrderDocuments WHERE AssetId = @AssetId)
   AND NOT EXISTS (SELECT 1 FROM projects.ProjectDocuments WHERE AssetId = @AssetId)
BEGIN
    DELETE FROM media.assets WHERE id = @AssetId;
END
SELECT @Deleted;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
            command.Parameters.AddInt("DocumentId", documentId);
            _connection.Open();
            try
            {
                var deleted = await command.ExecuteScalarAsync();
                return deleted != null && deleted != DBNull.Value && Convert.ToInt32(deleted) > 0;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<PurchaseOrderDocumentResult> AddDocumentAsync(CreatePurchaseOrderDocumentArgs args)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            await _connection.OpenAsync();
            try
            {
                return await InsertDocumentAsync(args, null, CancellationToken.None);
            }
            finally
            {
                _connection.Close();
            }
        }

        public override PurchaseOrderEntity PopulateRecord(SqlDataReader reader)
        {
            return new PurchaseOrderEntity();
        }

        private async Task<T> RunAsync<T>(Func<SqlTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
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
                var result = await work(transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

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

        private static int ProjectId(CreatePurchaseOrderArgs args)
        {
            var projectId = args.ProjectIds?.FirstOrDefault(id => id > 0) ?? 0;
            if (projectId <= 0)
            {
                throw new InvalidOperationException("The purchase order needs a project.");
            }

            return projectId;
        }

        private async Task<int> InsertHeaderAsync(CreatePurchaseOrderArgs args, int statusId, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            if (args.SupplierId <= 0)
            {
                throw new InvalidOperationException("Choose a supplier.");
            }

            if (args.Items == null || args.Items.Count == 0)
            {
                throw new InvalidOperationException("Add at least one purchase order line.");
            }

            var projectId = ProjectId(args);
            using var command = new SqlCommand(@"
DECLARE @BaseCurrencyId INT;
SELECT @BaseCurrencyId = Id FROM dbo.Currencies WHERE IsDefault = 1;
IF @BaseCurrencyId IS NULL
BEGIN
    ;THROW 50001, N'No default currency is configured.', 1;
END

DECLARE @CurrencyId INT = @RequestedCurrencyId;
IF @CurrencyId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE Id = @CurrencyId AND IsActive = 1)
BEGIN
    SET @CurrencyId = @BaseCurrencyId;
END

DECLARE @Rate DECIMAL(18, 6) = 1.0;
IF @CurrencyId <> @BaseCurrencyId
BEGIN
    SELECT TOP (1) @Rate = Rate
    FROM dbo.ExchangeRates
    WHERE FromCurrencyId = @CurrencyId
      AND ToCurrencyId = @BaseCurrencyId
      AND EffectiveDate <= CONVERT(DATE, GETDATE())
    ORDER BY EffectiveDate DESC, ExchangeRateId DESC;
END

IF NOT EXISTS (SELECT 1 FROM projects.Projects WHERE Id = @ProjectId)
BEGIN
    ;THROW 50021, N'Project was not found.', 1;
END

IF NOT EXISTS (SELECT 1 FROM projects.Suppliers WHERE Id = @SupplierId AND IsDeleted = 0)
BEGIN
    ;THROW 50022, N'Supplier was not found.', 1;
END

DECLARE @Code NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@RequestedCode)), N'');
IF @Code IS NOT NULL AND EXISTS (
    SELECT 1 FROM projects.PurchaseOrders WHERE IsDeleted = 0 AND Code = @Code
)
BEGIN
    ;THROW 50024, N'That PO number is already in use.', 1;
END

INSERT INTO projects.PurchaseOrders (SupplierId, Code, PoDate, ShipToAddress, CurrencyId, ExchangeRate, StatusId)
VALUES (
    @SupplierId,
    COALESCE(@Code, N'T' + REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N'')),
    @PoDate,
    NULLIF(LTRIM(RTRIM(@ShipToAddress)), N''),
    @CurrencyId,
    @Rate,
    @StatusId
);

DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);
IF @Code IS NULL
BEGIN
    UPDATE projects.PurchaseOrders
    SET Code = CASE
            WHEN @Id <= 99999 THEN N'PO-' + RIGHT(N'00000' + CAST(@Id AS NVARCHAR(10)), 5)
            ELSE N'PO-' + CAST(@Id AS NVARCHAR(20))
        END
    WHERE Id = @Id;
END

INSERT INTO projects.PurchaseOrderProjects (PurchaseOrderId, ProjectId)
VALUES (@Id, @ProjectId);

SELECT @Id;", _connection, transaction);
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.AddNVarChar("RequestedCode", args.Code ?? string.Empty, 50);
            command.Parameters.AddNullableInt("RequestedCurrencyId", args.CurrencyId);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNullableDateTime("PoDate", args.PoDate);
            command.Parameters.AddNVarChar("ShipToAddress", args.ShipToAddress ?? string.Empty, 500);
            command.Parameters.AddInt("StatusId", statusId);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (id <= 0)
            {
                throw new InvalidOperationException("The purchase order could not be saved.");
            }

            return id;
        }

        private async Task<bool> UpdateHeaderAsync(int purchaseOrderId, CreatePurchaseOrderArgs args, int statusId, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            if (args.SupplierId <= 0)
            {
                throw new InvalidOperationException("Choose a supplier.");
            }

            if (args.Items == null || args.Items.Count == 0)
            {
                throw new InvalidOperationException("Add at least one purchase order line.");
            }

            using var command = new SqlCommand(@"
DECLARE @BaseCurrencyId INT;
SELECT @BaseCurrencyId = Id FROM dbo.Currencies WHERE IsDefault = 1;
DECLARE @CurrencyId INT = @RequestedCurrencyId;
IF @CurrencyId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE Id = @CurrencyId AND IsActive = 1)
BEGIN
    SET @CurrencyId = @BaseCurrencyId;
END
DECLARE @Rate DECIMAL(18, 6) = 1.0;
IF @CurrencyId <> @BaseCurrencyId
BEGIN
    SELECT TOP (1) @Rate = Rate
    FROM dbo.ExchangeRates
    WHERE FromCurrencyId = @CurrencyId
      AND ToCurrencyId = @BaseCurrencyId
      AND EffectiveDate <= CONVERT(DATE, GETDATE())
    ORDER BY EffectiveDate DESC, ExchangeRateId DESC;
END

DECLARE @Code NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@RequestedCode)), N'');
IF @Code IS NOT NULL AND EXISTS (
    SELECT 1 FROM projects.PurchaseOrders WHERE IsDeleted = 0 AND Code = @Code AND Id <> @Id
)
BEGIN
    ;THROW 50024, N'That PO number is already in use.', 1;
END

UPDATE projects.PurchaseOrders
SET SupplierId = @SupplierId,
    Code = COALESCE(@Code, Code),
    PoDate = @PoDate,
    ShipToAddress = NULLIF(LTRIM(RTRIM(@ShipToAddress)), N''),
    CurrencyId = @CurrencyId,
    ExchangeRate = @Rate,
    StatusId = @StatusId,
    ModifiedAt = GETDATE()
WHERE Id = @Id AND IsDeleted = 0;", _connection, transaction);
            command.Parameters.AddInt("Id", purchaseOrderId);
            command.Parameters.AddInt("SupplierId", args.SupplierId);
            command.Parameters.AddNVarChar("RequestedCode", args.Code ?? string.Empty, 50);
            command.Parameters.AddNullableInt("RequestedCurrencyId", args.CurrencyId);
            command.Parameters.AddNullableDateTime("PoDate", args.PoDate);
            command.Parameters.AddNVarChar("ShipToAddress", args.ShipToAddress ?? string.Empty, 500);
            command.Parameters.AddInt("StatusId", statusId);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private async Task ReplaceItemsAsync(int purchaseOrderId, int projectId, IReadOnlyList<PurchaseOrderItemWrite> items, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using (var delete = new SqlCommand("DELETE FROM projects.PurchaseOrderItems WHERE PurchaseOrderId = @PurchaseOrderId", _connection, transaction))
            {
                delete.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            var sort = 0;
            foreach (var item in items.Where(line => line != null))
            {
                var activityId = item.ActivityId > 0 ? item.ActivityId : null;
                var quotationId = activityId.HasValue && item.QuotationId > 0 ? item.QuotationId : null;
                if (activityId == null && (string.IsNullOrWhiteSpace(item.DsnNo) || string.IsNullOrWhiteSpace(item.PartNo)))
                {
                    throw new InvalidOperationException("Each line that is not linked to an activity needs a DSN No and a Part No.");
                }

                if (item.Quantity < 0 || item.UnitPrice < 0 || item.TotalPrice < 0)
                {
                    throw new InvalidOperationException("Purchase order amounts must be zero or more.");
                }

                using var insert = new SqlCommand(@"
DECLARE @HeaderCurrencyId INT;
DECLARE @BaseCurrencyId INT;
SELECT @HeaderCurrencyId = CurrencyId FROM projects.PurchaseOrders WHERE Id = @PurchaseOrderId;
SELECT @BaseCurrencyId = Id FROM dbo.Currencies WHERE IsDefault = 1;
DECLARE @LineCurrencyId INT = ISNULL(@CurrencyId, @HeaderCurrencyId);
IF NOT EXISTS (SELECT 1 FROM dbo.Currencies WHERE Id = @LineCurrencyId AND IsActive = 1)
BEGIN
    SET @LineCurrencyId = @HeaderCurrencyId;
END
DECLARE @Rate DECIMAL(18, 6) = 1.0;
IF @LineCurrencyId <> @BaseCurrencyId
BEGIN
    SELECT TOP (1) @Rate = Rate
    FROM dbo.ExchangeRates
    WHERE FromCurrencyId = @LineCurrencyId
      AND ToCurrencyId = @BaseCurrencyId
      AND EffectiveDate <= CONVERT(DATE, GETDATE())
    ORDER BY EffectiveDate DESC, ExchangeRateId DESC;
END

INSERT INTO projects.PurchaseOrderItems (
    PurchaseOrderId, ActivityId, QuotationId, DsnNo, PartNo, PartDescription, Condition, Unit, LeadTime,
    Quantity, UnitPrice, TotalPrice, CurrencyId, ExchangeRate, BaseUnitPrice, BaseTotalPrice, SortOrder)
SELECT @PurchaseOrderId,
       A.Id,
       CASE WHEN A.Id IS NULL THEN NULL ELSE Q.Id END,
       NULLIF(LTRIM(RTRIM(@DsnNo)), N''),
       NULLIF(LTRIM(RTRIM(@PartNo)), N''),
       NULLIF(LTRIM(RTRIM(@Description)), N''),
       NULLIF(LTRIM(RTRIM(@Condition)), N''),
       NULLIF(LTRIM(RTRIM(@Unit)), N''),
       NULLIF(LTRIM(RTRIM(@LeadTime)), N''),
       @Quantity, @UnitPrice, @TotalPrice, @LineCurrencyId, @Rate,
       ROUND(@UnitPrice * @Rate, 2), ROUND(@TotalPrice * @Rate, 2), @SortOrder
FROM (SELECT 1 AS Anchor) AS Anchor
LEFT JOIN projects.Activities A
    ON A.Id = @ActivityId
   AND A.ProjectId = @ProjectId
   AND A.IsDeleted = 0
LEFT JOIN projects.Quotations Q
    ON Q.Id = @QuotationId
   AND Q.IsDeleted = 0
WHERE (@ActivityId IS NULL AND NULLIF(LTRIM(RTRIM(@DsnNo)), N'') IS NOT NULL AND NULLIF(LTRIM(RTRIM(@PartNo)), N'') IS NOT NULL)
   OR (A.Id IS NOT NULL AND (@QuotationId IS NULL OR Q.Id IS NOT NULL));", _connection, transaction);
                insert.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                insert.Parameters.AddInt("ProjectId", projectId);
                insert.Parameters.AddNullableInt("ActivityId", activityId);
                insert.Parameters.AddNullableInt("QuotationId", quotationId);
                insert.Parameters.AddNullableInt("CurrencyId", item.CurrencyId);
                insert.Parameters.AddNVarChar("DsnNo", item.DsnNo ?? string.Empty, 100);
                insert.Parameters.AddNVarChar("PartNo", TrimPart(item.PartNo), 200);
                insert.Parameters.AddNVarChar("Description", item.PartDescription ?? string.Empty, 500);
                insert.Parameters.AddNVarChar("Condition", item.Condition ?? string.Empty, 100);
                insert.Parameters.AddNVarChar("Unit", item.Unit ?? string.Empty, 100);
                insert.Parameters.AddNVarChar("LeadTime", item.LeadTime ?? string.Empty, 100);
                insert.Parameters.AddNullableDecimal("Quantity", item.Quantity, 18, 2);
                insert.Parameters.AddNullableDecimal("UnitPrice", item.UnitPrice, 18, 2);
                insert.Parameters.AddNullableDecimal("TotalPrice", item.TotalPrice, 18, 2);
                insert.Parameters.AddInt("SortOrder", sort++);
                var saved = await insert.ExecuteNonQueryAsync(cancellationToken);
                if (saved == 0)
                {
                    throw new InvalidOperationException(activityId == null
                        ? "Each custom purchase order line needs a DSN No and a Part No."
                        : "Each linked purchase order line needs an activity on this project. A quotation, when set, must still exist.");
                }
            }
        }

        private async Task ReplaceChargesAsync(int purchaseOrderId, IReadOnlyList<PurchaseOrderChargeWrite> charges, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            if (charges != null)
            {
                using (var delete = new SqlCommand("DELETE FROM projects.PurchaseOrderCharges WHERE PurchaseOrderId = @PurchaseOrderId", _connection, transaction))
                {
                    delete.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (var charge in charges.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Name)))
                {
                    using var insert = new SqlCommand(@"
INSERT INTO projects.PurchaseOrderCharges (PurchaseOrderId, Name, Basis, Amount, Rate, BaseAmount, SortOrder)
SELECT @PurchaseOrderId, @Name, N'Amount', @Amount, NULL, ROUND(@Amount * P.ExchangeRate, 2), @SortOrder
FROM projects.PurchaseOrders P
WHERE P.Id = @PurchaseOrderId;", _connection, transaction);
                    insert.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                    insert.Parameters.AddNVarChar("Name", charge.Name.Trim(), 200, false);
                    insert.Parameters.AddNullableDecimal("Amount", charge.Amount, 18, 2);
                    insert.Parameters.AddInt("SortOrder", charge.SortOrder);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            using var update = new SqlCommand(@"
UPDATE P
SET BaseSubTotal = ROUND(ISNULL((SELECT SUM(BaseTotalPrice) FROM projects.PurchaseOrderItems WHERE PurchaseOrderId = P.Id), 0), 2),
    SubTotal = ROUND(CASE
        WHEN P.ExchangeRate = 0 THEN 0
        ELSE ISNULL((SELECT SUM(BaseTotalPrice) FROM projects.PurchaseOrderItems WHERE PurchaseOrderId = P.Id), 0) / P.ExchangeRate
    END, 2)
FROM projects.PurchaseOrders P
WHERE P.Id = @PurchaseOrderId;

UPDATE P
SET BaseTotalAmount = ROUND(P.BaseSubTotal + ISNULL((SELECT SUM(BaseAmount) FROM projects.PurchaseOrderCharges WHERE PurchaseOrderId = P.Id), 0), 2),
    Total = ROUND(P.SubTotal + ISNULL((SELECT SUM(Amount) FROM projects.PurchaseOrderCharges WHERE PurchaseOrderId = P.Id), 0), 2)
FROM projects.PurchaseOrders P
WHERE P.Id = @PurchaseOrderId;", _connection, transaction);
            update.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<PurchaseOrderDocumentResult> InsertDocumentAsync(CreatePurchaseOrderDocumentArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1
    FROM projects.PurchaseOrders P
    INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = P.Id
    WHERE P.Id = @PurchaseOrderId AND PP.ProjectId = @ProjectId AND P.IsDeleted = 0
)
BEGIN
    ;THROW 50023, N'Purchase order was not found on this project.', 1;
END

DECLARE @AssetId UNIQUEIDENTIFIER = NEWID();
DECLARE @Original BIT = @IsOriginal;
IF @Original = 0 AND NOT EXISTS (
    SELECT 1 FROM projects.PurchaseOrderDocuments WHERE PurchaseOrderId = @PurchaseOrderId AND IsOriginal = 1
)
BEGIN
    SET @Original = 1;
END
DECLARE @Sort INT = @SortOrder;
IF @Sort IS NULL
BEGIN
    SELECT @Sort = ISNULL(MAX(A.sort_order), 0) + 1
    FROM projects.PurchaseOrderDocuments D
    INNER JOIN media.assets A ON A.id = D.AssetId
    WHERE D.PurchaseOrderId = @PurchaseOrderId;
END
IF @Original = 1
BEGIN
    UPDATE projects.PurchaseOrderDocuments SET IsOriginal = 0 WHERE PurchaseOrderId = @PurchaseOrderId;
END

INSERT INTO media.assets (id, file_url, file_name, file_type, file_size, sort_order, drive_file_id)
VALUES (@AssetId, ISNULL(@WebViewLink, N''), @FileName, @FileType, @FileSize, ISNULL(@Sort, 0), @GoogleDriveFileId);

INSERT INTO projects.PurchaseOrderDocuments (PurchaseOrderId, AssetId, IsOriginal)
VALUES (@PurchaseOrderId, @AssetId, @Original);

SELECT D.DocumentId, D.PurchaseOrderId, A.file_name AS FileName, A.file_type AS FileType, A.drive_file_id AS GoogleDriveFileId, A.file_url AS WebViewLink, A.created_at AS UploadedAt, A.file_size AS FileSize, A.sort_order AS SortOrder, D.IsOriginal, D.AssetId
FROM projects.PurchaseOrderDocuments D
INNER JOIN media.assets A ON A.id = D.AssetId
WHERE D.DocumentId = CAST(SCOPE_IDENTITY() AS INT);", _connection, transaction);
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("PurchaseOrderId", args.PurchaseOrderId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink ?? string.Empty, 1000, false);
            command.Parameters.AddBigInt("FileSize", args.FileSize);
            command.Parameters.AddBit("IsOriginal", args.IsOriginal);
            command.Parameters.AddNullableInt("SortOrder", args.SortOrder);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return ReadDocument(reader);
        }

        private static PurchaseOrderListItem ReadListItem(SqlDataReader reader)
        {
            return new PurchaseOrderListItem
            {
                Id = reader.ReadIntValue("Id"),
                SupplierId = reader.ReadIntValue("SupplierId"),
                SupplierName = reader.ReadNullableStringValue("SupplierName"),
                Code = reader.ReadNullableStringValue("Code"),
                PoDate = reader.ReadNullableDateTimeValue("PoDate"),
                ShipToAddress = reader.ReadNullableStringValue("ShipToAddress"),
                ItemCount = reader.ReadIntValue("ItemCount"),
                CurrencyId = reader.ReadIntValue("CurrencyId"),
                CurrencyCode = reader.ReadNullableStringValue("CurrencyCode"),
                CurrencySymbol = reader.ReadNullableStringValue("CurrencySymbol"),
                ExchangeRate = reader.ReadDecimalValue("ExchangeRate"),
                SubTotal = reader.ReadDecimalValue("SubTotal"),
                BaseSubTotal = reader.ReadDecimalValue("BaseSubTotal"),
                Total = reader.ReadDecimalValue("Total"),
                BaseTotalAmount = reader.ReadDecimalValue("BaseTotalAmount"),
                StatusId = reader.ReadIntValue("StatusId"),
                StatusCode = reader.ReadNullableStringValue("StatusCode"),
                StatusName = reader.ReadNullableStringValue("StatusName"),
            };
        }

        private static void CopyListItem(PurchaseOrderListItem source, PurchaseOrderDetailResult target)
        {
            target.Id = source.Id;
            target.SupplierId = source.SupplierId;
            target.SupplierName = source.SupplierName;
            target.Code = source.Code;
            target.PoDate = source.PoDate;
            target.ShipToAddress = source.ShipToAddress;
            target.ItemCount = source.ItemCount;
            target.CurrencyId = source.CurrencyId;
            target.CurrencyCode = source.CurrencyCode;
            target.CurrencySymbol = source.CurrencySymbol;
            target.ExchangeRate = source.ExchangeRate;
            target.SubTotal = source.SubTotal;
            target.BaseSubTotal = source.BaseSubTotal;
            target.Total = source.Total;
            target.BaseTotalAmount = source.BaseTotalAmount;
            target.StatusId = source.StatusId;
            target.StatusCode = source.StatusCode;
            target.StatusName = source.StatusName;
        }

        private static PurchaseOrderDocumentResult ReadDocument(SqlDataReader reader)
        {
            return new PurchaseOrderDocumentResult
            {
                DocumentId = reader.ReadIntValue("DocumentId"),
                PurchaseOrderId = reader.ReadIntValue("PurchaseOrderId"),
                FileName = reader.ReadNullableStringValue("FileName"),
                FileType = reader.ReadNullableStringValue("FileType"),
                GoogleDriveFileId = reader.ReadNullableStringValue("GoogleDriveFileId"),
                WebViewLink = reader.ReadNullableStringValue("WebViewLink"),
                UploadedAt = reader.ReadNullableDateTimeValue("UploadedAt"),
                AssetId = reader.ReadGuidValue("AssetId"),
                FileSize = reader["FileSize"] == DBNull.Value ? 0 : Convert.ToInt64(reader["FileSize"]),
                SortOrder = reader.ReadIntValue("SortOrder"),
                IsOriginal = reader.ReadBooleanValue("IsOriginal"),
            };
        }

        public async Task OrderDocumentsAsync(int projectId, int purchaseOrderId, IReadOnlyList<DocumentOrderItem> documents, int? originalDocumentId)
        {
            await _connection.OpenAsync();
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var item in documents ?? Array.Empty<DocumentOrderItem>())
                {
                    using var command = new SqlCommand(@"
UPDATE A
SET A.sort_order = @SortOrder
FROM media.assets AS A
INNER JOIN projects.PurchaseOrderDocuments AS D ON D.AssetId = A.id
INNER JOIN projects.PurchaseOrders AS P ON P.Id = D.PurchaseOrderId
INNER JOIN projects.PurchaseOrderProjects AS PP ON PP.PurchaseOrderId = P.Id
WHERE D.DocumentId = @DocumentId
  AND D.PurchaseOrderId = @PurchaseOrderId
  AND PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0;", _connection, transaction);
                    command.Parameters.AddInt("ProjectId", projectId);
                    command.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                    command.Parameters.AddInt("DocumentId", item.DocumentId);
                    command.Parameters.AddInt("SortOrder", item.SortOrder);
                    await command.ExecuteNonQueryAsync();
                }

                if (originalDocumentId is > 0)
                {
                    using var mark = new SqlCommand(@"
UPDATE D
SET D.IsOriginal = CASE WHEN D.DocumentId = @OriginalDocumentId THEN 1 ELSE 0 END
FROM projects.PurchaseOrderDocuments AS D
INNER JOIN projects.PurchaseOrders AS P ON P.Id = D.PurchaseOrderId
INNER JOIN projects.PurchaseOrderProjects AS PP ON PP.PurchaseOrderId = P.Id
WHERE D.PurchaseOrderId = @PurchaseOrderId
  AND PP.ProjectId = @ProjectId
  AND P.IsDeleted = 0;", _connection, transaction);
                    mark.Parameters.AddInt("ProjectId", projectId);
                    mark.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
                    mark.Parameters.AddInt("OriginalDocumentId", originalDocumentId.Value);
                    await mark.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }

        private async Task<int> InsertNewAsync(CreatePurchaseOrderArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            var status = await ResolveCreateStatusAsync(args, transaction, cancellationToken);
            var id = await InsertHeaderAsync(args, status.Id, transaction, cancellationToken);
            await InsertHistoryAsync(id, status, args, transaction, cancellationToken);
            return id;
        }

        private async Task<PurchaseOrderStatusChoice> ResolveCreateStatusAsync(CreatePurchaseOrderArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            var code = string.IsNullOrWhiteSpace(args.StatusCode) ? "Draft" : args.StatusCode.Trim();
            var status = await FindStatusAsync(code, transaction, cancellationToken);
            return new PurchaseOrderStatusChoice
            {
                Id = status.Id,
                Code = status.Code,
                Name = status.Name,
                Action = "Created",
                Summary = "Created as " + status.Name + ".",
            };
        }

        private async Task<PurchaseOrderStatusChoice> ResolveUpdateStatusAsync(int purchaseOrderId, CreatePurchaseOrderArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            var current = await CurrentStatusAsync(purchaseOrderId, transaction, cancellationToken);
            if (current == null)
            {
                throw new InvalidOperationException("The purchase order could not be saved.");
            }

            var requestedCode = string.IsNullOrWhiteSpace(args.StatusCode) ? current.Code : args.StatusCode.Trim();
            var requested = await FindStatusAsync(requestedCode, transaction, cancellationToken);
            if (string.Equals(current.Code, "Sent", StringComparison.OrdinalIgnoreCase)
                && string.Equals(requested.Code, "Sent", StringComparison.OrdinalIgnoreCase))
            {
                var revised = await FindStatusAsync("Revised", transaction, cancellationToken);
                return new PurchaseOrderStatusChoice
                {
                    Id = revised.Id,
                    Code = revised.Code,
                    Name = revised.Name,
                    Action = "Revised",
                    Summary = "Edited while the status was Sent, so it changed to Revised.",
                };
            }

            if (!string.Equals(current.Code, requested.Code, StringComparison.OrdinalIgnoreCase))
            {
                return new PurchaseOrderStatusChoice
                {
                    Id = requested.Id,
                    Code = requested.Code,
                    Name = requested.Name,
                    Action = "StatusChanged",
                    Summary = "Status changed from " + current.Name + " to " + requested.Name + ".",
                };
            }

            return new PurchaseOrderStatusChoice
            {
                Id = requested.Id,
                Code = requested.Code,
                Name = requested.Name,
                Action = "Edited",
                Summary = "Purchase order updated.",
            };
        }

        private async Task<PurchaseOrderStatusChoice> CurrentStatusAsync(int purchaseOrderId, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand(@"
SELECT ST.Id, ST.Code, ST.Name
FROM projects.PurchaseOrders P
INNER JOIN dbo.PurchaseOrderStatuses ST ON ST.Id = P.StatusId
WHERE P.Id = @Id AND P.IsDeleted = 0;", _connection, transaction);
            command.Parameters.AddInt("Id", purchaseOrderId);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new PurchaseOrderStatusChoice
            {
                Id = reader.ReadIntValue("Id"),
                Code = reader.ReadNullableStringValue("Code"),
                Name = reader.ReadNullableStringValue("Name"),
            };
        }

        private async Task<PurchaseOrderStatusChoice> FindStatusAsync(string code, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand(@"
SELECT Id, Code, Name
FROM dbo.PurchaseOrderStatuses
WHERE Code = @Code;", _connection, transaction);
            command.Parameters.AddNVarChar("Code", code ?? string.Empty, 50);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Choose a purchase order status.");
            }

            return new PurchaseOrderStatusChoice
            {
                Id = reader.ReadIntValue("Id"),
                Code = reader.ReadNullableStringValue("Code"),
                Name = reader.ReadNullableStringValue("Name"),
            };
        }

        private async Task InsertHistoryAsync(int purchaseOrderId, PurchaseOrderStatusChoice status, CreatePurchaseOrderArgs args, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            await InsertHistoryAsync(purchaseOrderId, status, args?.ActorName, args?.ActorEmail, args?.ActorUserId, transaction, cancellationToken);
        }

        private async Task InsertHistoryAsync(int purchaseOrderId, PurchaseOrderStatusChoice status, string actorName, string actorEmail, int? actorUserId, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            using var command = new SqlCommand(@"
INSERT INTO projects.PurchaseOrderHistory
    (PurchaseOrderId, Action, Summary, StatusId, ActorName, ActorEmail, UserId)
VALUES
    (@PurchaseOrderId, @Action, @Summary, @StatusId,
     NULLIF(LTRIM(RTRIM(@ActorName)), N''),
     NULLIF(LTRIM(RTRIM(@ActorEmail)), N''),
     @UserId);", _connection, transaction);
            command.Parameters.AddInt("PurchaseOrderId", purchaseOrderId);
            command.Parameters.AddNVarChar("Action", status.Action ?? string.Empty, 40);
            command.Parameters.AddNVarChar("Summary", status.Summary ?? string.Empty, 400);
            command.Parameters.AddInt("StatusId", status.Id);
            command.Parameters.AddNVarChar("ActorName", actorName ?? string.Empty, 200);
            command.Parameters.AddNVarChar("ActorEmail", actorEmail ?? string.Empty, 320);
            command.Parameters.AddNullableInt("UserId", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private sealed class PurchaseOrderStatusChoice
        {
            public int Id { get; set; }

            public string Code { get; set; }

            public string Name { get; set; }

            public string Action { get; set; }

            public string Summary { get; set; }
        }

        private static string TrimPart(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 200 ? text : text.Substring(0, 200);
        }
    }
}
