namespace Mahkias.Api.Modules.Projects
{
    public class PurchaseOrderItemRequest
    {
        public int? ActivityId { get; set; }

        public int? QuotationId { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public string Condition { get; set; }

        public string Unit { get; set; }

        public string LeadTime { get; set; }

        public decimal? Quantity { get; set; }

        public decimal? UnitPrice { get; set; }

        public decimal? TotalPrice { get; set; }

        public int? CurrencyId { get; set; }
    }

    public class CreatePurchaseOrderRequest
    {
        public string Code { get; set; }

        public int SupplierId { get; set; }

        public DateTime? PoDate { get; set; }

        public string ShipToAddress { get; set; }

        public int? CurrencyId { get; set; }

        public List<int> ProjectIds { get; set; }

        public List<PurchaseOrderItemRequest> Items { get; set; }

        public List<QuotationChargeRequest> Charges { get; set; }
    }

    public class PurchaseOrderImportForm
    {
        public string Code { get; set; }

        public int SupplierId { get; set; }

        public string PoDate { get; set; }

        public string ShipToAddress { get; set; }

        public int? CurrencyId { get; set; }

        public string Items { get; set; }

        public string Charges { get; set; }

        public IFormFile File { get; set; }
    }
}
