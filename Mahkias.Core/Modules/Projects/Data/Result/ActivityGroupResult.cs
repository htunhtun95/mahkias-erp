namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class ActivityGroupResult
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? TypeId { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ActivityGroupAssignResult
    {
        public int Updated { get; set; }

        public bool Missing { get; set; }

        public string Error { get; set; }
    }
}
