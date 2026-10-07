using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Api.Helpers
{
    public enum QuotationMapStatus
    {
        Linked,
        Unmatched,
        Ambiguous,
    }

    public sealed class QuotationMapLink
    {
        public int? ActivityId { get; set; }

        public int? ActivityGroupId { get; set; }

        public bool IsAlternativePart { get; set; }

        public int? AlternativePartId { get; set; }
    }

    public sealed class QuotationMapCandidate
    {
        public string Kind { get; set; }

        public int Id { get; set; }

        public string Label { get; set; }
    }

    public sealed class QuotationMapOutcome
    {
        public QuotationMapStatus Status { get; set; }

        public string Message { get; set; }

        public List<QuotationMapLink> Links { get; set; } = new List<QuotationMapLink>();

        public List<QuotationMapCandidate> Candidates { get; set; } = new List<QuotationMapCandidate>();
    }

    public static class QuotationItemMapper
    {
        public static QuotationMapOutcome Map(
            string dsnNo,
            string partNo,
            IReadOnlyList<QuotationMapActivity> activities,
            IReadOnlyList<QuotationMapGroup> groups,
            string duplicateHandling)
        {
            var dsn = Normalize(dsnNo);
            var part = Normalize(partNo);
            var activityHits = new List<QuotationMapActivity>();
            if (!string.IsNullOrEmpty(dsn))
            {
                activityHits.AddRange(activities.Where(activity => Same(activity.DsnNo, dsn)));
            }
            else if (!string.IsNullOrEmpty(part))
            {
                activityHits.AddRange(activities.Where(activity => PartMatches(activity, part)));
            }

            var groupHits = new List<QuotationMapGroup>();
            _ = groups;

            var candidates = new List<QuotationMapCandidate>();
            foreach (var activity in activityHits)
            {
                candidates.Add(new QuotationMapCandidate
                {
                    Kind = "activity",
                    Id = activity.Id,
                    Label = ActivityLabel(activity),
                });
            }

            if (candidates.Count == 0)
            {
                return new QuotationMapOutcome
                {
                    Status = QuotationMapStatus.Unmatched,
                    Links = new List<QuotationMapLink> { new QuotationMapLink() },
                };
            }

            if (candidates.Count == 1)
            {
                return new QuotationMapOutcome
                {
                    Status = QuotationMapStatus.Linked,
                    Links = new List<QuotationMapLink> { LinkFor(candidates[0], activityHits, part) },
                    Candidates = candidates,
                };
            }

            var handling = NormalizeHandling(duplicateHandling);
            if (handling == "choose")
            {
                return new QuotationMapOutcome
                {
                    Status = QuotationMapStatus.Ambiguous,
                    Message = "This item matches more than one activity or group. Choose a target.",
                    Candidates = candidates,
                };
            }

            if (handling == "separate")
            {
                return new QuotationMapOutcome
                {
                    Status = QuotationMapStatus.Linked,
                    Links = candidates.Select(candidate => LinkFor(candidate, activityHits, part)).ToList(),
                    Candidates = candidates,
                };
            }

            return new QuotationMapOutcome
            {
                Status = QuotationMapStatus.Linked,
                Links = new List<QuotationMapLink> { MergeLink(activityHits, groupHits) },
                Candidates = candidates,
            };
        }

        public static string NormalizeHandling(string value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant();
            return text == "merge" || text == "separate" || text == "choose" ? text : "choose";
        }

        private static QuotationMapLink MergeLink(IReadOnlyList<QuotationMapActivity> activities, IReadOnlyList<QuotationMapGroup> groups)
        {
            var sharedGroup = activities
                .Select(activity => activity.ActivityGroupId)
                .Where(id => id.HasValue)
                .Distinct()
                .ToList();
            if (activities.Count > 0 && sharedGroup.Count == 1 && activities.All(activity => activity.ActivityGroupId == sharedGroup[0]))
            {
                return new QuotationMapLink { ActivityGroupId = sharedGroup[0] };
            }

            if (activities.Count == 0 && groups.Count == 1)
            {
                return new QuotationMapLink { ActivityGroupId = groups[0].Id };
            }

            if (activities.Count == 1 && groups.Count == 0)
            {
                return LinkActivity(activities[0], string.Empty);
            }

            return new QuotationMapLink();
        }

        private static QuotationMapLink LinkFor(QuotationMapCandidate candidate, IReadOnlyList<QuotationMapActivity> activities, string part)
        {
            if (candidate.Kind == "group")
            {
                return new QuotationMapLink { ActivityGroupId = candidate.Id };
            }

            var activity = activities.First(item => item.Id == candidate.Id);
            return LinkActivity(activity, part);
        }

        private static QuotationMapLink LinkActivity(QuotationMapActivity activity, string part)
        {
            var alternative = activity.Alternatives.FirstOrDefault(item => Same(item.AlternativePartNo, part));
            return new QuotationMapLink
            {
                ActivityId = activity.Id,
                IsAlternativePart = alternative != null,
                AlternativePartId = alternative?.Id,
            };
        }

        private static bool PartMatches(QuotationMapActivity activity, string part)
        {
            return Same(activity.PartNo, part)
                || activity.Alternatives.Any(alternative => Same(alternative.MainPartNo, part) || Same(alternative.AlternativePartNo, part));
        }

        private static string ActivityLabel(QuotationMapActivity activity)
        {
            var dsn = string.IsNullOrWhiteSpace(activity.DsnNo) ? "#" + activity.Id : activity.DsnNo.Trim();
            var part = string.IsNullOrWhiteSpace(activity.PartNo) ? string.Empty : activity.PartNo.Trim();
            return string.IsNullOrEmpty(part) ? dsn : dsn + " · " + part;
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
