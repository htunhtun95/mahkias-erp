using System.Globalization;
using System.Text.RegularExpressions;

namespace Mahkias.Api.Helpers
{
    public static class LeadTimeDays
    {
        private static readonly Regex DayText = new(@"^(?<days>\d+)(?:\.0+)?(?:\s*(?:days?|d))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public const string Message = "Lead time must be a whole number of days, or left blank.";

        public static bool TryParse(string value, out int? days)
        {
            days = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var match = DayText.Match(value.Trim());
            if (!match.Success || !int.TryParse(match.Groups["days"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            days = parsed;
            return true;
        }
    }
}
