using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Result;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mahkias.Api.Helpers
{
    public sealed class QuotationPlannedRow
    {
        public int Row { get; set; }

        public string DsnNo { get; set; }

        public string PartNo { get; set; }

        public string Description { get; set; }

        public string Condition { get; set; }

        public string LeadTime { get; set; }

        public int? MinimumQty { get; set; }

        public DateTime? QuotedDate { get; set; }

        public DateTime? ValidUntil { get; set; }

        public decimal? UnitPrice { get; set; }

        public decimal? TotalPrice { get; set; }

        public int Quantity { get; set; }

        public bool PriceWarning { get; set; }

        public bool UnitExplicit { get; set; }

        public bool TotalExplicit { get; set; }

        public string CurrencyCode { get; set; }
    }

    public static class QuotationExcelPlanner
    {
        public static (List<QuotationPlannedRow> Rows, List<ActivityImportIssue> Errors) Plan(
            ActivitySheet sheet,
            QuotationExcelUploadForm form)
        {
            var rows = new List<QuotationPlannedRow>();
            var errors = new List<ActivityImportIssue>();
            foreach (var source in sheet.Rows)
            {
                var dsn = CellOrDefault(source, form.DsnNoColumn, form.DefaultDsnNo);
                var part = CellOrDefault(source, form.PartNoColumn, form.DefaultPartNo);
                var description = CellOrDefault(source, form.DescriptionColumn, form.DefaultDescription);
                var condition = CellOrDefault(source, form.ConditionColumn, form.DefaultCondition);
                var leadTime = CellOrDefault(source, form.LeadTimeColumn, form.DefaultLeadTime);
                var minimumText = Cell(source, form.MinimumQtyColumn);
                var quotedDateText = Cell(source, form.QuotedDateColumn);
                var validUntilText = Cell(source, form.ValidUntilColumn);
                var quantityText = Cell(source, form.QuantityColumn);
                var unitText = Cell(source, form.UnitPriceColumn);
                var totalText = Cell(source, form.TotalPriceColumn);
                var currencyText = Cell(source, form.CurrencyColumn);
                if (string.IsNullOrWhiteSpace(dsn)
                    && string.IsNullOrWhiteSpace(part)
                    && string.IsNullOrWhiteSpace(description)
                    && string.IsNullOrWhiteSpace(quantityText)
                    && string.IsNullOrWhiteSpace(unitText)
                    && string.IsNullOrWhiteSpace(totalText)
                    && string.IsNullOrWhiteSpace(form.DefaultPartNo)
                    && string.IsNullOrWhiteSpace(form.DefaultUnitPrice)
                    && string.IsNullOrWhiteSpace(form.DefaultTotalPrice))
                {
                    continue;
                }

                var pricesBlank = string.IsNullOrWhiteSpace(unitText)
                    && string.IsNullOrWhiteSpace(totalText)
                    && string.IsNullOrWhiteSpace(form.DefaultUnitPrice)
                    && string.IsNullOrWhiteSpace(form.DefaultTotalPrice);
                if (pricesBlank)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dsn))
                {
                    errors.Add(Issue(source.RowNumber, "DSN No", "Field is missing."));
                }

                if (string.IsNullOrWhiteSpace(part))
                {
                    errors.Add(Issue(source.RowNumber, "Part No", "Field is missing."));
                }

                if (!string.IsNullOrWhiteSpace(condition) && condition.Trim().Length > 100)
                {
                    errors.Add(Issue(source.RowNumber, "Condition", "Condition must be 100 characters or fewer."));
                }

                if (!string.IsNullOrWhiteSpace(leadTime) && leadTime.Trim().Length > 100)
                {
                    errors.Add(Issue(source.RowNumber, "Lead Time", "Lead time must be 100 characters or fewer."));
                }

                int quantity;
                if (string.IsNullOrWhiteSpace(quantityText))
                {
                    quantity = ParseQuantity(form.DefaultQuantity) ?? 1;
                }
                else if (!TryQuantity(quantityText, out quantity))
                {
                    errors.Add(Issue(source.RowNumber, "Qty", "'" + quantityText + "' is not a valid number."));
                    quantity = 0;
                }

                int? minimumQty = null;
                var minimumSource = string.IsNullOrWhiteSpace(minimumText) ? form.DefaultMinimumQty : minimumText;
                if (!string.IsNullOrWhiteSpace(minimumSource))
                {
                    if (!TryQuantity(minimumSource, out var parsedMinimum))
                    {
                        errors.Add(Issue(source.RowNumber, "MOQ", "'" + minimumSource + "' is not a valid number."));
                    }
                    else
                    {
                        minimumQty = parsedMinimum;
                    }
                }

                if (!TryDate(string.IsNullOrWhiteSpace(quotedDateText) ? form.DefaultQuotedDate : quotedDateText, out var quotedDate))
                {
                    errors.Add(Issue(source.RowNumber, "Quoted Date", "'" + (string.IsNullOrWhiteSpace(quotedDateText) ? form.DefaultQuotedDate : quotedDateText) + "' is not a valid date."));
                }

                if (!TryDate(string.IsNullOrWhiteSpace(validUntilText) ? form.DefaultValidUntil : validUntilText, out var validUntil))
                {
                    errors.Add(Issue(source.RowNumber, "Valid Until", "'" + (string.IsNullOrWhiteSpace(validUntilText) ? form.DefaultValidUntil : validUntilText) + "' is not a valid date."));
                }

                decimal? unit = null;
                decimal? total = null;
                if (!string.IsNullOrWhiteSpace(unitText))
                {
                    if (!TryMoney(unitText, out var parsed))
                    {
                        errors.Add(Issue(source.RowNumber, "Unit Price", "'" + unitText + "' is not a valid number."));
                    }
                    else
                    {
                        unit = parsed;
                    }
                }
                else if (TryMoney(form.DefaultUnitPrice, out var defaultUnit))
                {
                    unit = defaultUnit;
                }

                if (!string.IsNullOrWhiteSpace(totalText))
                {
                    if (!TryMoney(totalText, out var parsed))
                    {
                        errors.Add(Issue(source.RowNumber, "Total Price", "'" + totalText + "' is not a valid number."));
                    }
                    else
                    {
                        total = parsed;
                    }
                }
                else if (TryMoney(form.DefaultTotalPrice, out var defaultTotal))
                {
                    total = defaultTotal;
                }

                var unitExplicit = unit.HasValue;
                var totalExplicit = total.HasValue;
                var priceWarning = false;
                var effectiveQuantity = minimumQty is > 0 ? minimumQty.Value : (quantity > 0 ? quantity : 0);
                if (!totalExplicit && unitExplicit && effectiveQuantity > 0)
                {
                    total = decimal.Round(unit.Value * effectiveQuantity, 4, MidpointRounding.AwayFromZero);
                }
                else if (!unitExplicit && totalExplicit && effectiveQuantity > 0)
                {
                    unit = decimal.Round(total.Value / effectiveQuantity, 4, MidpointRounding.AwayFromZero);
                }
                else if (unitExplicit && totalExplicit && effectiveQuantity > 0)
                {
                    var computed = decimal.Round(unit.Value * effectiveQuantity, 4, MidpointRounding.AwayFromZero);
                    var imported = decimal.Round(total.Value, 4, MidpointRounding.AwayFromZero);
                    priceWarning = Math.Abs(computed - imported) > 0.01m;
                    total = imported;
                }

                rows.Add(new QuotationPlannedRow
                {
                    Row = source.RowNumber,
                    DsnNo = dsn,
                    PartNo = part,
                    Description = description,
                    Condition = TrimTo(condition, 100),
                    LeadTime = TrimTo(leadTime, 100),
                    MinimumQty = minimumQty,
                    QuotedDate = quotedDate,
                    ValidUntil = validUntil,
                    UnitPrice = unit,
                    TotalPrice = total,
                    Quantity = quantity < 0 ? 0 : quantity,
                    PriceWarning = priceWarning,
                    UnitExplicit = unitExplicit,
                    TotalExplicit = totalExplicit,
                    CurrencyCode = currencyText,
                });
            }

            return (rows, errors);
        }

        private static ActivityImportIssue Issue(int row, string field, string message)
        {
            return new ActivityImportIssue { Row = row, Field = field, Message = message };
        }

        private static string TrimTo(string value, int maxLength)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            {
                return null;
            }

            return text;
        }

        private static string CellOrDefault(ActivityDataRow row, string column, string fallback)
        {
            var cell = Cell(row, column);
            return string.IsNullOrWhiteSpace(cell) ? fallback?.Trim() ?? string.Empty : cell.Trim();
        }

        private static string Cell(ActivityDataRow row, string column)
        {
            if (string.IsNullOrWhiteSpace(column) || row.Cells == null || !row.Cells.TryGetValue(column.Trim().ToUpperInvariant(), out var value))
            {
                return string.Empty;
            }

            return value?.Trim() ?? string.Empty;
        }

        private static int? ParseQuantity(string value)
        {
            return TryQuantity(value, out var quantity) ? quantity : null;
        }

        private static bool TryQuantity(string value, out int quantity)
        {
            quantity = 0;
            var text = SanitizeNumber(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                return false;
            }

            if (parsed < 0 || parsed != decimal.Truncate(parsed))
            {
                return false;
            }

            quantity = (int)parsed;
            return true;
        }

        private static bool TryDate(string value, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var text = value.Trim();
            if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial)
                && serial > 20000
                && serial < 80000)
            {
                date = DateTime.FromOADate(serial).Date;
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                date = parsed.Date;
                return true;
            }

            return false;
        }

        private static readonly Regex UnitToken = new Regex(@"\b(EA|PCS|PC|QTY|UNITS|UNIT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static string SanitizeNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim();
            text = text.Replace("$", string.Empty).Replace("€", string.Empty).Replace("£", string.Empty).Replace("¥", string.Empty);
            text = text.Replace(",", string.Empty);
            text = UnitToken.Replace(text, " ");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static bool TryMoney(string value, out decimal amount)
        {
            amount = 0;
            var text = SanitizeNumber(value);
            if (string.IsNullOrWhiteSpace(text) || text.Any(character => char.IsLetter(character)))
            {
                return false;
            }

            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            {
                return false;
            }

            return amount >= 0;
        }
    }
}
