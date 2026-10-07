using Mahkias.Core.Data;

namespace Mahkias.Core.Modules.Projects.Data
{
    public class Supplier : EntityBase
    {
        public string Name { get; set; }

        public string Reference { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }
    }
}
