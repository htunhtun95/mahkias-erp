namespace Mahkias.Core.Modules.Main.Data.Args
{
    public class SearchArgs
    {
        public SearchArgs()
        {
            Limit = 100;
        }

        public int Index { get; set; }
        public int Limit { get; set; }

        public int StartIndex
        {
            get
            {
                return Index * Limit;
            }
        }

        public string OrderBy { get; set; }
        public string OrderByDirection { get; set; }
        public string Keywords { get; set; }
    }
}
