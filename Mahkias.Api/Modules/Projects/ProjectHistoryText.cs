using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Api.Modules.Projects
{
    public static class ProjectHistoryText
    {
        public static string Created(string name)
        {
            return Fit("Created " + Clean(name));
        }

        public static string Updated(IReadOnlyList<string> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                return null;
            }

            return Fit("Updated " + Join(fields));
        }

        public static string Deleted(string name)
        {
            return Fit("Deleted " + Clean(name));
        }

        public static string Added(IEnumerable<CreateActivityArgs> activities)
        {
            var lines = (activities ?? Array.Empty<CreateActivityArgs>())
                .Where(activity => activity != null && !string.IsNullOrWhiteSpace(activity.PartNo))
                .Select(activity => Line(activity.DSNNo, activity.PartNo))
                .ToList();
            return AddedLines(lines);
        }

        public static string Added(CreateActivityRequest activity)
        {
            if (activity == null || string.IsNullOrWhiteSpace(activity.PartNo))
            {
                return null;
            }

            return Fit("Added " + Line(activity.DSNNo, activity.PartNo));
        }

        public static string QuotationCreated(string code, string supplier)
        {
            var quotation = QuotationName(code);
            var name = Clean(supplier);
            return Fit(name.Length > 0 ? "Created " + quotation + " for " + name : "Created " + quotation);
        }

        public static string QuotationUpdated(string code)
        {
            return Fit("Updated " + QuotationName(code));
        }

        public static string QuotationDeleted(string code)
        {
            return Fit("Deleted " + QuotationName(code));
        }

        public static string DocumentsUploaded(IEnumerable<string> names, string quotationCode)
        {
            return DocumentNames("Uploaded", names, quotationCode);
        }

        public static string DocumentReplaced(string previousName, string fileName, string quotationCode)
        {
            var next = Clean(fileName);
            var previous = Clean(previousName);
            var file = next.Length > 0 ? next : "a document";
            var quotation = Clean(quotationCode);
            if (previous.Length > 0 && !string.Equals(previous, next, StringComparison.Ordinal))
            {
                file = previous + " with " + file;
            }
            var summary = "Replaced " + file;
            if (quotation.Length > 0)
            {
                summary += " on quotation " + quotation;
            }
            return Fit(summary);
        }

        public static string DocumentRemoved(string fileName, string quotationCode)
        {
            var file = Clean(fileName);
            var summary = "Removed " + (file.Length > 0 ? file : "a document");
            var quotation = Clean(quotationCode);
            if (quotation.Length > 0)
            {
                summary += " from quotation " + quotation;
            }
            return Fit(summary);
        }

        public static string DocumentsReordered(string quotationCode)
        {
            var quotation = Clean(quotationCode);
            return Fit(quotation.Length > 0 ? "Reordered documents on quotation " + quotation : "Reordered project documents");
        }

        public static bool KeptOrderChanged<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, Func<T, int> id, Func<T, int> sort, Func<T, bool> original)
        {
            before ??= Array.Empty<T>();
            after ??= Array.Empty<T>();
            var beforeIds = new HashSet<int>(before.Select(id));
            var afterIds = new HashSet<int>(after.Select(id));
            var keptBefore = before.Where(item => afterIds.Contains(id(item))).OrderBy(sort).ThenBy(id).Select(id).ToList();
            var keptAfter = after.Where(item => beforeIds.Contains(id(item))).OrderBy(sort).ThenBy(id).Select(id).ToList();
            if (!keptBefore.SequenceEqual(keptAfter))
            {
                return true;
            }

            var beforeOriginal = before.Where(item => original(item) && afterIds.Contains(id(item))).Select(id).Cast<int?>().FirstOrDefault();
            var afterOriginal = after.Where(item => original(item) && beforeIds.Contains(id(item))).Select(id).Cast<int?>().FirstOrDefault();
            return beforeOriginal != afterOriginal;
        }

        public static string Removed(ActivityIdentity activity)
        {
            if (activity == null)
            {
                return null;
            }

            return Fit("Removed " + Line(activity.DsnNo, activity.PartNo));
        }

        public static string ActivityUpdate(ProjectActivityResult before, CreateActivityArgs after)
        {
            if (before == null || after == null)
            {
                return null;
            }

            var fields = new List<string>();
            Compare(fields, "DSN", before.DSNNo, after.DSNNo);
            Compare(fields, "part number", before.PartNo, after.PartNo);
            Compare(fields, "description", before.Description, after.Description);
            decimal? quantity = after.Quantity;
            if (before.Quantity != quantity)
            {
                fields.Add("quantity");
            }
            Compare(fields, "type", before.TypeId?.ToString(), after.TypeId?.ToString());
            Compare(fields, "unit", before.Unit, after.Unit);
            Compare(fields, "condition", before.Condition, after.Condition);
            if (before.Budget != after.Budget)
            {
                fields.Add("budget");
            }
            Compare(fields, "SerialNo", before.SerialNo, after.SerialNo);
            Compare(fields, "TSN", before.TSN, after.TSN);
            Compare(fields, "TSO", before.TSO, after.TSO);
            Compare(fields, "CSN", before.CSN, after.CSN);
            Compare(fields, "CSO", before.CSO, after.CSO);
            if (fields.Count == 0)
            {
                return null;
            }

            return Fit("Updated " + Line(after.DSNNo, after.PartNo) + " (" + Join(fields) + ")");
        }

        private static string AddedLines(List<string> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }

            if (lines.Count == 1)
            {
                return Fit("Added " + lines[0]);
            }

            var shown = string.Join(", ", lines.Take(5));
            var extra = lines.Count - Math.Min(lines.Count, 5);
            var summary = "Added " + lines.Count + " activities: " + shown;
            if (extra > 0)
            {
                summary += " and " + extra + " more";
            }

            return Fit(summary);
        }

        private static void Compare(List<string> fields, string label, string before, string after)
        {
            if (!string.Equals(Clean(before), Clean(after), StringComparison.Ordinal))
            {
                fields.Add(label);
            }
        }

        private static string QuotationName(string code)
        {
            var quotation = Clean(code);
            return quotation.Length > 0 ? "quotation " + quotation : "a quotation";
        }

        private static string DocumentNames(string verb, IEnumerable<string> names, string quotationCode)
        {
            var files = (names ?? Array.Empty<string>()).Select(Clean).Where(name => name.Length > 0).ToList();
            if (files.Count == 0)
            {
                return null;
            }

            string summary;
            if (files.Count == 1)
            {
                summary = verb + " " + files[0];
            }
            else
            {
                var shown = string.Join(", ", files.Take(5));
                var extra = files.Count - Math.Min(files.Count, 5);
                summary = verb + " " + files.Count + " documents: " + shown;
                if (extra > 0)
                {
                    summary += " and " + extra + " more";
                }
            }

            var quotation = Clean(quotationCode);
            if (quotation.Length > 0)
            {
                summary += " on quotation " + quotation;
            }

            return Fit(summary);
        }

        private static string Line(string dsn, string part)
        {
            var number = Clean(dsn);
            var partNo = Clean(part);
            if (number.Length > 0 && partNo.Length > 0)
            {
                return "DSN " + number + " · " + partNo;
            }

            if (partNo.Length > 0)
            {
                return partNo;
            }

            return number.Length > 0 ? "DSN " + number : "activity";
        }

        private static string Join(IReadOnlyList<string> fields)
        {
            if (fields.Count == 1)
            {
                return fields[0];
            }

            if (fields.Count == 2)
            {
                return fields[0] + " and " + fields[1];
            }

            return string.Join(", ", fields.Take(fields.Count - 1)) + ", and " + fields[fields.Count - 1];
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string Fit(string value)
        {
            var text = Clean(value);
            return text.Length <= 400 ? text : text.Substring(0, 400);
        }
    }
}
