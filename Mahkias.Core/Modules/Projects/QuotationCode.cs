namespace Mahkias.Core.Modules.Projects
{
    public static class QuotationCode
    {
        public static string Format(int id)
        {
            if (id < 1)
            {
                id = 1;
            }

            return id <= 99999 ? "Q-" + id.ToString("00000") : "Q-" + id.ToString();
        }
    }
}
