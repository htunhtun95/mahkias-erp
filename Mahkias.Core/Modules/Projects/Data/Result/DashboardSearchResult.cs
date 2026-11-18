namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class DashboardSearchResult
    {
        public int ProjectTotal { get; set; }

        public int ActiveProjectCount { get; set; }

        public int SupplierTotal { get; set; }

        public int QuotationCount { get; set; }

        public decimal QuotedValue { get; set; }

        public int OpenPurchaseOrderCount { get; set; }

        public int TotalCount { get; set; }

        public List<DashboardTrackRow> Items { get; set; } = new();
    }

    public class DashboardTrackRow
    {
        public int Id { get; set; }

        public string Dataset { get; set; }

        public string Status { get; set; }

        public string Code { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; }

        public string SupplierReference { get; set; }

        public int ProjectId { get; set; }

        public string ProjectName { get; set; }

        public string ProjectReference { get; set; }

        public int ItemCount { get; set; }

        public decimal BaseTotalAmount { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? ValidUntil { get; set; }
    }
}
