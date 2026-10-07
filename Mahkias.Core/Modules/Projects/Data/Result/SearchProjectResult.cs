namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class SearchProjectResult
    {
        public IEnumerable<SearchProject_ProjectResult> Items { get; set; }

        public int TotalResults { get; set; }
    }

    public class SearchProject_ProjectResult
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Reference { get; set; }

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public int FiscalYear { get; set; }

        public int ProjectStatusId { get; set; }

        public string Status { get; set; }
        public string ActivitiesName { get; set; }
        public DateTime? Deadline { get; set; }
    }
}
