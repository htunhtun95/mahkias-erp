namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class ActivityLabelResult
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public string Name { get; set; }

        public int TypeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<int> ActivityIds { get; set; } = new List<int>();

        public List<int> ActivityGroupIds { get; set; } = new List<int>();
    }
}
