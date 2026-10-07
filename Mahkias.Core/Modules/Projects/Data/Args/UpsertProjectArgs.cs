namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class UpsertProjectArgs
    {
        public string Name { get; set; }

        public string Reference { get; set; }

        public string Description { get; set; }

        public int FiscalYear { get; set; }

        public int? ProjectStatusId { get; set; }
        public int? ProjectTypeId { get; set; }
        public DateTime? Deadline { get; set; }
    }
}
