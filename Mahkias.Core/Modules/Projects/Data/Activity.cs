using Mahkias.Core.Data;

namespace Mahkias.Core.Modules.Projects.Data
{
    public class Activity : EntityBase
    {
        public int ProjectId { get; set; }

        public string PartNo { get; set; }

        public decimal? Budget { get; set; }

        public string Description { get; set; }

        public string DSNNo { get; set; }

        public decimal? Quantity { get; set; }

        public int? ActivityTypeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public string DeletedBy { get; set; }

        public virtual Project Project { get; set; }

        public virtual ActivityType Type { get; set; }
    }
}
