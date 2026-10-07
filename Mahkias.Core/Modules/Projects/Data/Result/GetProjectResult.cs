namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class GetProjectResult
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Reference { get; set; }

        public string Description { get; set; }

        public int FiscalYear { get; set; }
        public DateTime? Deadline { get; set; }

        public int ProjectStatusId { get; set; }
        public string ProjectStatus { get; set; }
        public string ProjectStatusSlug { get; set; }
        public int ProjectTypeId { get; set; }
        public string ProjectType { get; set; }
    }
}
