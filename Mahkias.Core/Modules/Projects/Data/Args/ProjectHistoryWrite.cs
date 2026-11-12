namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class ProjectHistoryWrite
    {
        public int ProjectId { get; set; }

        public string Action { get; set; }

        public string Summary { get; set; }

        public string ActorName { get; set; }

        public string ActorEmail { get; set; }

        public int? UserId { get; set; }
    }
}
