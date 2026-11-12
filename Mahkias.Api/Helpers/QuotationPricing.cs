namespace Mahkias.Api.Helpers
{
    public static class QuotationPricing
    {
        public static int EffectiveQuantity(int? minimumQty, int quantity, int fallback = 1)
        {
            var hasMinimum = minimumQty is > 0;
            var hasQuantity = quantity > 0;
            if (hasMinimum && hasQuantity)
            {
                return quantity > minimumQty.Value ? quantity : minimumQty.Value;
            }

            if (hasMinimum)
            {
                return minimumQty.Value;
            }

            return hasQuantity ? quantity : fallback;
        }
    }
}
