using System.Globalization;
using System.Text;

namespace Mahkias.Core.Modules.Projects
{
    public static class SupplierReferenceRules
    {
        public const int MaxLength = 100;

        public static string Slug(string name)
        {
            var builder = new StringBuilder();
            var pendingHyphen = false;
            foreach (var character in (name ?? string.Empty).Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingHyphen && builder.Length > 0)
                    {
                        builder.Append('-');
                    }
                    builder.Append(character);
                    pendingHyphen = false;
                    continue;
                }
                pendingHyphen = builder.Length > 0;
            }

            var slug = builder.ToString().Trim('-');
            if (slug.Length > MaxLength)
            {
                slug = slug.Substring(0, MaxLength).Trim('-');
            }
            return slug.Length == 0 ? "supplier" : slug;
        }

        public static string NextAvailable(string slug, IEnumerable<string> taken)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in taken)
            {
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    used.Add(reference.Trim());
                }
            }
            if (!used.Contains(slug))
            {
                return slug;
            }

            for (var number = 2; number < 100000; number++)
            {
                var suffix = "-" + number.ToString(CultureInfo.InvariantCulture);
                var stem = slug;
                var room = MaxLength - suffix.Length;
                if (stem.Length > room)
                {
                    stem = stem.Substring(0, Math.Max(room, 1)).TrimEnd('-');
                }
                var candidate = stem + suffix;
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }

            return slug;
        }
    }

    public class DuplicateSupplierReferenceException : Exception
    {
        public DuplicateSupplierReferenceException(string reference)
            : base(MessageFor(reference))
        {
            Reference = reference;
        }

        public string Reference { get; }

        public static string MessageFor(string reference)
        {
            return $"Supplier reference \"{reference}\" is already in use.";
        }
    }
}
