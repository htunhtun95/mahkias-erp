using Mahkias.Core.Data;

namespace Mahkias.Core.Modules.Projects.Data
{
    public class Project : EntityBase
    {
        public string Name { get; set; }

        public string Reference { get; set; }

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public string DeletedBy { get; set; }

        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
