using System.Text.Json.Serialization;

namespace Mahkias.Api.Modules.Projects
{
    public class CreateSupplierRequest
    {
        public string Name { get; set; }

        public string Reference { get; set; }
    }

    public class QuotationItemRequest
    {
        [JsonPropertyName("dsnNo")]
        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public string Condition { get; set; }

        public string LeadTime { get; set; }

        public int? MinimumQty { get; set; }

        public decimal? UnitPrice { get; set; }

        public int? Quantity { get; set; }

        public decimal? TotalPrice { get; set; }

        public int? ActivityId { get; set; }

        public List<int> ActivityIds { get; set; }

        public int? ActivityGroupId { get; set; }

        public int? CurrencyId { get; set; }
    }

    public class CreateQuotationRequest
    {
        public int SupplierId { get; set; }

        public string SupplierReference { get; set; }

        public string DriveFileLink { get; set; }

        public List<int> ProjectIds { get; set; } = new List<int>();

        public int? CurrencyId { get; set; }

        public DateTime? QuotedDate { get; set; }

        public DateTime? ValidUntil { get; set; }

        public List<QuotationItemRequest> Items { get; set; } = new List<QuotationItemRequest>();

        public string DuplicateHandling { get; set; }
    }

    public class QuotationImportForm
    {
        public int SupplierId { get; set; }

        public string SupplierReference { get; set; }

        public int? CurrencyId { get; set; }

        public string QuotedDate { get; set; }

        public string ValidUntil { get; set; }

        public string Items { get; set; }

        public IFormFile File { get; set; }

        public int DocumentId { get; set; }
    }

    public class QuotationExcelUploadForm
    {
        public IFormFile File { get; set; }

        public string DsnNoColumn { get; set; }

        public string PartNoColumn { get; set; }

        public string DescriptionColumn { get; set; }

        public string ConditionColumn { get; set; }

        public string LeadTimeColumn { get; set; }

        public string MinimumQtyColumn { get; set; }

        public string QuotedDateColumn { get; set; }

        public string ValidUntilColumn { get; set; }

        public string UnitPriceColumn { get; set; }

        public string QuantityColumn { get; set; }

        public string TotalPriceColumn { get; set; }

        public string CurrencyColumn { get; set; }

        public string DefaultDsnNo { get; set; }

        public string DefaultPartNo { get; set; }

        public string DefaultDescription { get; set; }

        public string DefaultCondition { get; set; }

        public string DefaultLeadTime { get; set; }

        public string DefaultMinimumQty { get; set; }

        public string DefaultQuotedDate { get; set; }

        public string DefaultValidUntil { get; set; }

        public string DefaultUnitPrice { get; set; }

        public string DefaultQuantity { get; set; }

        public string DefaultTotalPrice { get; set; }

        public int? CurrencyId { get; set; }

        public string SupplierReferenceColumn { get; set; }

        public string DefaultSupplierReference { get; set; }

        public string DuplicateHandling { get; set; }
    }
}
