namespace Mahkias.Core.Modules.Projects
{
    public static class PurchaseOrderCode
    {
        public static string Format(int id)
        {
            if (id < 1)
            {
                id = 1;
            }

            return id <= 99999 ? "PO-" + id.ToString("00000") : "PO-" + id.ToString();
        }
    }
}
