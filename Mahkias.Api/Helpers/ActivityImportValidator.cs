using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using System.Globalization;

namespace Mahkias.Api.Helpers
{
    public class ActivityImportPlan
    {
        public List<ActivityImportIssue> Errors { get; } = new();
        public List<ActivityImportCandidate> Rows { get; } = new();
    }

    public static class ActivityImportValidator
    {
        public static ActivityImportPlan Plan(ActivitySheet sheet, ActivityExcelUploadForm form, IReadOnlyList<ActivityType> types)
        {
            var plan = new ActivityImportPlan();
            var defaultTypeId = ParsePositiveInt(form?.DefaultTypeId);
            var defaultQuantity = ParseOptionalInteger(form?.DefaultQuantity, out var quantityDefaultInvalid);
            var defaultBudget = ParseOptionalDecimal(form?.DefaultBudget, out var budgetDefaultInvalid);

            if (quantityDefaultInvalid)
            {
                plan.Errors.Add(Issue(0, "Qty", "Default quantity must be a numeric integer."));
            }

            if (budgetDefaultInvalid)
            {
                plan.Errors.Add(Issue(0, "Budget", "Default budget must be a numeric value."));
            }

            foreach (var row in sheet?.Rows ?? Array.Empty<ActivityDataRow>())
            {
                var dsnRaw = Cell(row.Cells, form?.DsnNoColumn);
                var partRaw = Cell(row.Cells, form?.PartNoColumn);
                var typeRaw = Cell(row.Cells, form?.TypeColumn);
                var quantityRaw = Cell(row.Cells, form?.QuantityColumn);
                var budgetRaw = Cell(row.Cells, form?.BudgetColumn);
                var rowErrors = new List<ActivityImportIssue>();

                var dsnNo = TextOrDefault(dsnRaw, form?.DefaultDsnNo);
                if (string.IsNullOrWhiteSpace(dsnNo))
                {
                    rowErrors.Add(Issue(row.RowNumber, "DSN No", "Field is missing."));
                }

                var partNo = TextOrDefault(partRaw, form?.DefaultPartNo);
                if (string.IsNullOrWhiteSpace(partNo))
                {
                    rowErrors.Add(Issue(row.RowNumber, "Part No", "Field is missing."));
                }

                var typeId = ResolveType(typeRaw, defaultTypeId, types, out var typeError);
                if (typeError != null)
                {
                    rowErrors.Add(Issue(row.RowNumber, "Type", typeError));
                }

                int? quantity = null;
                if (string.IsNullOrWhiteSpace(quantityRaw))
                {
                    quantity = quantityDefaultInvalid ? null : defaultQuantity ?? 1;
                }
                else if (TryParseInteger(quantityRaw, out var parsedQuantity))
                {
                    quantity = parsedQuantity;
                }
                else
                {
                    rowErrors.Add(Issue(row.RowNumber, "Qty", $"'{quantityRaw}' is not a valid number."));
                }

                decimal? budget = null;
                if (string.IsNullOrWhiteSpace(budgetRaw))
                {
                    budget = budgetDefaultInvalid ? null : defaultBudget;
                }
                else if (TryParseDecimal(budgetRaw, out var parsedBudget))
                {
                    budget = parsedBudget;
                }
                else
                {
                    rowErrors.Add(Issue(row.RowNumber, "Budget", $"'{budgetRaw}' is not a valid number."));
                }

                plan.Errors.AddRange(rowErrors);
                if (rowErrors.Count > 0)
                {
                    continue;
                }

                plan.Rows.Add(new ActivityImportCandidate
                {
                    RowNumber = row.RowNumber,
                    PartNo = partNo,
                    DSNNo = dsnNo,
                    Description = Cell(row.Cells, form?.DescriptionColumn),
                    Budget = budget,
                    Quantity = quantity,
                    TypeId = typeId
                });
            }

            return plan;
        }

        private static ActivityImportIssue Issue(int row, string field, string message)
        {
            return new ActivityImportIssue
            {
                Row = row,
                Field = field,
                Message = message
            };
        }

        private static string Cell(IReadOnlyDictionary<string, string> row, string columnLetter)
        {
            if (row == null || string.IsNullOrWhiteSpace(columnLetter))
            {
                return null;
            }

            return row.TryGetValue(columnLetter.Trim(), out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
        }

        private static string TextOrDefault(string raw, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
        }

        private static int? ResolveType(string raw, int? defaultTypeId, IReadOnlyList<ActivityType> types, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (defaultTypeId == null)
                {
                    error = "Field is missing.";
                }

                return defaultTypeId;
            }

            var match = MatchType(raw, types);
            if (match != null)
            {
                return match;
            }

            if (defaultTypeId != null)
            {
                return defaultTypeId;
            }

            error = $"'{raw}' is not a valid type.";
            return null;
        }

        private static int? MatchType(string raw, IReadOnlyList<ActivityType> types)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                var byId = types?.FirstOrDefault(type => type.Id == id);
                if (byId != null)
                {
                    return byId.Id;
                }
            }

            var match = types?.FirstOrDefault(type =>
                string.Equals(type.Name, raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type.Slug, raw, StringComparison.OrdinalIgnoreCase));
            return match?.Id;
        }

        private static int? ParsePositiveInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : null;
        }

        private static int? ParseOptionalInteger(string value, out bool invalid)
        {
            invalid = false;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (TryParseInteger(value, out var parsed))
            {
                return parsed;
            }

            invalid = true;
            return null;
        }

        private static decimal? ParseOptionalDecimal(string value, out bool invalid)
        {
            invalid = false;
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (TryParseDecimal(value, out var parsed))
            {
                return parsed;
            }

            invalid = true;
            return null;
        }

        private static bool TryParseInteger(string raw, out int value)
        {
            value = 0;
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value >= 0;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                && number == decimal.Truncate(number)
                && number >= 0
                && number <= int.MaxValue)
            {
                value = (int)number;
                return true;
            }

            return false;
        }

        private static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0;
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.StartsWith("$", StringComparison.Ordinal))
            {
                text = text.Substring(1).Trim();
            }

            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
