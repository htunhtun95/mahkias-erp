namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class ActivityImportCandidate
    {
        public int RowNumber { get; set; }
        public string PartNo { get; set; }
        public string DSNNo { get; set; }
        public string Description { get; set; }
        public decimal? Budget { get; set; }
        public int? Quantity { get; set; }
        public int? TypeId { get; set; }
        public string Label { get; set; }
        public string Unit { get; set; }
        public string Condition { get; set; }
        public string SerialNo { get; set; }
        public string TSN { get; set; }
        public string TSO { get; set; }
        public string CSN { get; set; }
        public string CSO { get; set; }
    }
}
