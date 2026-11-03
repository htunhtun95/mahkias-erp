namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class PurchaseOrderListItem
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; }

        public string Code { get; set; }

        public DateTime? PoDate { get; set; }

        public string ShipToAddress { get; set; }

        public int ItemCount { get; set; }

        public int CurrencyId { get; set; }

        public string CurrencyCode { get; set; }

        public string CurrencySymbol { get; set; }

        public decimal ExchangeRate { get; set; }

        public decimal SubTotal { get; set; }

        public decimal BaseSubTotal { get; set; }

        public decimal Total { get; set; }

        public decimal BaseTotalAmount { get; set; }
    }

    public class PurchaseOrderItemResult
    {
        public int Id { get; set; }

        public int? ActivityId { get; set; }

        public int? QuotationId { get; set; }

        public string QuotationCode { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public string Condition { get; set; }

        public string Unit { get; set; }

        public string LeadTime { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public int CurrencyId { get; set; }

        public string CurrencyCode { get; set; }

        public string CurrencySymbol { get; set; }

        public decimal ExchangeRate { get; set; }

        public decimal BaseUnitPrice { get; set; }

        public decimal BaseTotalPrice { get; set; }

        public int SortOrder { get; set; }
    }

    public class PurchaseOrderChargeResult
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Basis { get; set; }

        public decimal? Amount { get; set; }

        public decimal? Rate { get; set; }

        public decimal BaseAmount { get; set; }

        public int SortOrder { get; set; }
    }

    public class PurchaseOrderDocumentResult
    {
        public int DocumentId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }

        public DateTime? UploadedAt { get; set; }
    }

    public class PurchaseOrderDetailResult : PurchaseOrderListItem
    {
        public List<PurchaseOrderItemResult> Items { get; set; } = new();

        public List<PurchaseOrderChargeResult> Charges { get; set; } = new();
    }

    public class PurchaseOrderActivityMatch
    {
        public int Id { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }
    }
}
