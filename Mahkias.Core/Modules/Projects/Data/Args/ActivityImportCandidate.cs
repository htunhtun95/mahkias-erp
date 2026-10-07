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
    }
}
