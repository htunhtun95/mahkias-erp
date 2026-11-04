namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class PurchaseOrderItemWrite
    {
        public int? ActivityId { get; set; }

        public int? QuotationId { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string PartDescription { get; set; }

        public string Condition { get; set; }

        public string Unit { get; set; }

        public string LeadTime { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        public int? CurrencyId { get; set; }
    }

    public class PurchaseOrderChargeWrite
    {
        public string Name { get; set; }

        public decimal Amount { get; set; }

        public int SortOrder { get; set; }
    }

    public class CreatePurchaseOrderArgs
    {
        public string Code { get; set; }

        public int SupplierId { get; set; }

        public DateTime? PoDate { get; set; }

        public string ShipToAddress { get; set; }

        public int? CurrencyId { get; set; }

        public IReadOnlyList<int> ProjectIds { get; set; }

        public IReadOnlyList<PurchaseOrderItemWrite> Items { get; set; }

        public IReadOnlyList<PurchaseOrderChargeWrite> Charges { get; set; }

        public string StatusCode { get; set; }

        public string ActorName { get; set; }

        public string ActorEmail { get; set; }

        public int? ActorUserId { get; set; }
    }

    public class CreatePurchaseOrderDocumentArgs
    {
        public int ProjectId { get; set; }

        public int PurchaseOrderId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }
    }
}
