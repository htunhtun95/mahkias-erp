namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class CreateActivityArgs
    {
        public int ProjectId { get; set; }
        public string PartNo { get; set; }
        public decimal? Budget { get; set; }
        public string Description { get; set; }
        public string DSNNo { get; set; }
        public int? Quantity { get; set; }
        public int? TypeId { get; set; }
    }

    public class ActivityBatchUpdate
    {
        public int Id { get; set; }
        public CreateActivityArgs Args { get; set; }
    }
}
