using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Api.Helpers
{
    public static class LookupText
    {
        public static string Fold(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool Equals(string left, string right)
        {
            var expected = Fold(left);
            var actual = Fold(right);
            return expected.Length > 0 && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        public static string Canonical(string value, string fallback, IReadOnlyList<LookupValueResult> lookups)
        {
            var text = Fold(value);
            if (text.Length == 0)
            {
                text = Fold(fallback);
            }

            if (text.Length == 0)
            {
                return null;
            }

            var match = lookups?.FirstOrDefault(item => Equals(item.Name, text) || Equals(item.Code, text));
            return string.IsNullOrWhiteSpace(match?.Name) ? text : match.Name.Trim();
        }

        public static CurrencyResult MatchCurrency(string value, IReadOnlyList<CurrencyResult> currencies)
        {
            var text = Fold(value);
            if (text.Length == 0 || currencies == null)
            {
                return null;
            }

            return currencies.FirstOrDefault(currency => Equals(currency.Code, text) || Equals(currency.Name, text));
        }
    }
}
