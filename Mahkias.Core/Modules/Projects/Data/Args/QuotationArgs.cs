namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class CreateSupplierArgs
    {
        public string Name { get; set; }

        public string Reference { get; set; }
    }

    public class QuotationItemWrite
    {
        public int? ActivityId { get; set; }

        public int? ActivityGroupId { get; set; }

        public bool IsAlternativePart { get; set; }

        public int? AlternativePartId { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string PartDescription { get; set; }

        public string Condition { get; set; }

        public string SerialNo { get; set; }

        public string TSN { get; set; }

        public string TSO { get; set; }

        public string CSN { get; set; }

        public string CSO { get; set; }

        public string Unit { get; set; }

        public int? LeadTime { get; set; }

        public int? MinimumQty { get; set; }

        public decimal? UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal? TotalPrice { get; set; }

        public int? CurrencyId { get; set; }
    }

    public class QuotationChargeWrite
    {
        public string Name { get; set; }

        public decimal Amount { get; set; }

        public int SortOrder { get; set; }
    }

    public class CreateQuotationArgs
    {
        public int SupplierId { get; set; }

        public string SupplierReference { get; set; }

        public string DriveFileLink { get; set; }

        public int? CurrencyId { get; set; }

        public DateTime? QuotedDate { get; set; }

        public DateTime? ValidUntil { get; set; }

        public IReadOnlyList<int> ProjectIds { get; set; }

        public IReadOnlyList<QuotationItemWrite> Items { get; set; }

        public IReadOnlyList<QuotationChargeWrite> Charges { get; set; }
    }
}
