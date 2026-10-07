using Mahkias.Core.Modules.Main.Data.Args;

namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class SearchProjectArgs : SearchArgs
    {
        public string SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; }
        public string SortDirection { get; set; }

        public string Statuses { get; set; }
        public int[] StatusIds { get; set; }

        public string Types { get; set; }
    }
}
