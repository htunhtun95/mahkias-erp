using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Api.Helpers
{
    public sealed class QuotationAllocation
    {
        public int ActivityId { get; set; }

        public string DsnNo { get; set; }

        public int Quantity { get; set; }

        public int Required { get; set; }

        public bool IsAlternativePart { get; set; }

        public int? AlternativePartId { get; set; }
    }

    public static class QuotationAllocator
    {
        public static QuotationMapActivity Direct(string dsnNo, string partNo, IReadOnlyList<QuotationMapActivity> activities)
        {
            var dsnHits = MatchingDsn(dsnNo, activities);
            if (dsnHits.Count == 1)
            {
                return dsnHits[0];
            }

            var part = Normalize(partNo);
            if (dsnHits.Count > 1)
            {
                var narrowed = dsnHits.Where(activity => PartMatches(activity, part)).ToList();
                return narrowed.Count == 1 ? narrowed[0] : null;
            }

            var partHits = MatchingPart(partNo, activities);
            return partHits.Count == 1 ? partHits[0] : null;
        }

        public static List<QuotationMapActivity> Candidates(string dsnNo, string partNo, IReadOnlyList<QuotationMapActivity> activities)
        {
            var dsnHits = MatchingDsn(dsnNo, activities);
            if (dsnHits.Count > 0)
            {
                return Sort(dsnHits);
            }

            return Sort(MatchingPart(partNo, activities));
        }

        public static List<QuotationMapActivity> Selected(IReadOnlyList<int> activityIds, IReadOnlyList<QuotationMapActivity> activities)
        {
            var ids = activityIds ?? Array.Empty<int>();
            return Sort(activities.Where(activity => ids.Contains(activity.Id)).ToList());
        }

        public static List<QuotationAllocation> Allocate(int quantity, IReadOnlyList<QuotationMapActivity> pool, string partNo)
        {
            var slices = new List<QuotationAllocation>();
            if (quantity < 1 || pool == null || pool.Count == 0)
            {
                return slices;
            }

            if (pool.Count == 1)
            {
                slices.Add(Slice(pool[0], quantity, partNo));
                return slices;
            }

            var remaining = quantity;
            for (var index = 0; index < pool.Count; index++)
            {
                var activity = pool[index];
                var required = Required(activity);
                var last = index == pool.Count - 1;
                var take = last ? remaining : Math.Min(required, remaining);
                if (take > 0)
                {
                    slices.Add(Slice(activity, take, partNo));
                }

                remaining -= take;
                if (remaining <= 0 && !last)
                {
                    break;
                }
            }

            return slices;
        }

        private static QuotationAllocation Slice(QuotationMapActivity activity, int quantity, string partNo)
        {
            var part = Normalize(partNo);
            var alternative = activity.Alternatives?.FirstOrDefault(item => Same(item.AlternativePartNo, part));
            return new QuotationAllocation
            {
                ActivityId = activity.Id,
                DsnNo = (activity.DsnNo ?? string.Empty).Trim(),
                Quantity = quantity,
                Required = Required(activity),
                IsAlternativePart = alternative != null,
                AlternativePartId = alternative?.Id,
            };
        }

        private static int Required(QuotationMapActivity activity)
        {
            if (!activity.Quantity.HasValue)
            {
                return 1;
            }

            var rounded = (int)Math.Round(activity.Quantity.Value, MidpointRounding.AwayFromZero);
            return rounded < 0 ? 0 : rounded;
        }

        private static List<QuotationMapActivity> MatchingDsn(string dsnNo, IReadOnlyList<QuotationMapActivity> activities)
        {
            var dsn = Normalize(dsnNo);
            if (string.IsNullOrEmpty(dsn))
            {
                return new List<QuotationMapActivity>();
            }

            return activities.Where(activity => Same(activity.DsnNo, dsn)).ToList();
        }

        private static List<QuotationMapActivity> MatchingPart(string partNo, IReadOnlyList<QuotationMapActivity> activities)
        {
            var part = Normalize(partNo);
            if (string.IsNullOrEmpty(part))
            {
                return new List<QuotationMapActivity>();
            }

            return activities.Where(activity => PartMatches(activity, part)).ToList();
        }

        private static bool PartMatches(QuotationMapActivity activity, string part)
        {
            if (Same(activity.PartNo, part))
            {
                return true;
            }

            return activity.Alternatives != null && activity.Alternatives.Any(alternative =>
                Same(alternative.MainPartNo, part) || Same(alternative.AlternativePartNo, part));
        }

        private static List<QuotationMapActivity> Sort(List<QuotationMapActivity> activities)
        {
            return activities
                .OrderBy(activity => DsnRank(activity.DsnNo))
                .ThenBy(activity => Normalize(activity.DsnNo), StringComparer.OrdinalIgnoreCase)
                .ThenBy(activity => activity.Id)
                .ToList();
        }

        private static int DsnRank(string dsnNo)
        {
            var text = Normalize(dsnNo);
            if (text.Length > 0 && text.All(char.IsDigit) && int.TryParse(text, out var number))
            {
                return number;
            }

            return int.MaxValue;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Normalize(right));
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
