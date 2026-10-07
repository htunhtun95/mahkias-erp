namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class SupplierResult
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Reference { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public int QuotationCount { get; set; }
    }

    public class SupplierQuotationResult
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public string ProjectNames { get; set; }

        public int? ProjectId { get; set; }

        public int ItemCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class SupplierDetailResult
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Reference { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public int QuotationCount { get; set; }

        public int ProjectCount { get; set; }

        public List<SupplierQuotationResult> Quotations { get; set; } = new List<SupplierQuotationResult>();
    }

    public class SupplierDeleteResult
    {
        public bool Found { get; set; }

        public bool Deleted { get; set; }
    }

    public class QuotationMapAlternative
    {
        public int Id { get; set; }

        public int ActivityId { get; set; }

        public string MainPartNo { get; set; }

        public string AlternativePartNo { get; set; }
    }

    public class QuotationMapActivity
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public decimal? Quantity { get; set; }

        public int? ActivityGroupId { get; set; }

        public List<QuotationMapAlternative> Alternatives { get; set; } = new List<QuotationMapAlternative>();
    }

    public class QuotationMapGroup
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public string Name { get; set; }
    }

    public class QuotationListItem
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; }

        public string Code { get; set; }

        public string SupplierReference { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public int ItemCount { get; set; }

        public decimal TotalPrice { get; set; }
    }

    public class QuotationLineResult
    {
        public int Id { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public decimal? UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal? TotalPrice { get; set; }

        public int? ActivityId { get; set; }

        public string MappedActivity { get; set; }
    }

    public class QuotationDetailResult : QuotationListItem
    {
        public List<QuotationLineResult> Items { get; set; } = new List<QuotationLineResult>();
    }

    public class ActivityQuotationLink
    {
        public int ActivityId { get; set; }

        public int ItemId { get; set; }

        public int QuotationId { get; set; }

        public string QuotationRef { get; set; }

        public string SupplierName { get; set; }

        public string SupplierQuotationRef { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        public bool AlternativePart { get; set; }

        public decimal? UnitPrice { get; set; }

        public decimal? TotalPrice { get; set; }

        public bool Lowest { get; set; }

        public DateTime RecordedAt { get; set; }
    }

    public class PartPriceHistoryItem
    {
        public int Id { get; set; }

        public int QuotationId { get; set; }

        public string QuotationReference { get; set; }

        public string SupplierName { get; set; }

        public string PartNo { get; set; }

        public string PartDescription { get; set; }

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
