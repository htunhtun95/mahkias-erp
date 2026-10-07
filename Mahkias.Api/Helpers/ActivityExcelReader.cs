using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.Text;

namespace Mahkias.Api.Helpers
{
    public static class ActivityExcelReader
    {
        public static ActivitySheet Read(Stream stream, string fileName)
        {
            if (IsCsv(fileName))
            {
                return ReadCsv(stream);
            }

            return ReadWorkbook(stream);
        }

        public static string ToColumnLetter(int zeroBasedIndex)
        {
            var dividend = zeroBasedIndex + 1;
            var column = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                column = (char)('A' + modulo) + column;
                dividend = (dividend - modulo) / 26;
            }

            return column;
        }

        private static bool IsCsv(string fileName)
        {
            return fileName != null && fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        private static ActivitySheet ReadCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var lines = new List<string[]>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    continue;
                }

                lines.Add(SplitCsv(line));
            }

            if (lines.Count == 0)
            {
                return ActivitySheet.Empty;
            }

            var columnCount = lines.Max(line => line.Length);
            var columns = BuildColumns(columnCount, index => index < lines[0].Length ? lines[0][index] : null);
            var rows = new List<ActivityDataRow>();
            for (var i = 1; i < lines.Count; i++)
            {
                var row = ReadCells(columnCount, index => index < lines[i].Length ? lines[i][index] : null);
                if (HasValue(row))
                {
                    rows.Add(new ActivityDataRow(i + 1, row));
                }
            }

            return new ActivitySheet(columns, rows);
        }

        private static ActivitySheet ReadWorkbook(Stream stream)
        {
            using IWorkbook workbook = new XSSFWorkbook(stream);
            ISheet sheet = workbook.NumberOfSheets == 0 ? null : workbook.GetSheetAt(0);
            if (sheet == null || sheet.LastRowNum < 0 && sheet.PhysicalNumberOfRows == 0)
            {
                return ActivitySheet.Empty;
            }

            var lastRow = Math.Max(sheet.LastRowNum, 0);
            var columnCount = 0;
            for (var rowIndex = 0; rowIndex <= lastRow; rowIndex++)
            {
                var sheetRow = sheet.GetRow(rowIndex);
                if (sheetRow != null && sheetRow.LastCellNum > columnCount)
                {
                    columnCount = sheetRow.LastCellNum;
                }
            }

            if (columnCount <= 0)
            {
                return ActivitySheet.Empty;
            }

            var headerRow = sheet.GetRow(0);
            var columns = BuildColumns(columnCount, index => CellText(headerRow?.GetCell(index)));
            var rows = new List<ActivityDataRow>();
            for (var rowIndex = 1; rowIndex <= lastRow; rowIndex++)
            {
                var sheetRow = sheet.GetRow(rowIndex);
                var row = ReadCells(columnCount, index => CellText(sheetRow?.GetCell(index)));
                if (HasValue(row))
                {
                    rows.Add(new ActivityDataRow(rowIndex + 1, row));
                }
            }

            return new ActivitySheet(columns, rows);
        }

        private static IReadOnlyList<ActivityColumn> BuildColumns(int columnCount, Func<int, string> headerAt)
        {
            var columns = new List<ActivityColumn>(columnCount);
            for (var index = 0; index < columnCount; index++)
            {
                var header = headerAt(index)?.Trim();
                columns.Add(new ActivityColumn(ToColumnLetter(index), index, string.IsNullOrWhiteSpace(header) ? null : header));
            }

            return columns;
        }

        private static Dictionary<string, string> ReadCells(int columnCount, Func<int, string> valueAt)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columnCount; index++)
            {
                row[ToColumnLetter(index)] = Clean(valueAt(index));
            }

            return row;
        }

        private static bool HasValue(IReadOnlyDictionary<string, string> row)
        {
            return row.Values.Any(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string CellText(ICell cell)
        {
            if (cell == null || cell.CellType == CellType.Blank)
            {
                return null;
            }

            if (cell.CellType == CellType.Formula)
            {
                return cell.CachedFormulaResultType switch
                {
                    CellType.Numeric => FormatNumber(cell.NumericCellValue),
                    CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
                    CellType.Blank => null,
                    _ => cell.ToString()
                };
            }

            if (cell.CellType == CellType.Numeric)
            {
                return FormatNumber(cell.NumericCellValue);
            }

            if (cell.CellType == CellType.Boolean)
            {
                return cell.BooleanCellValue ? "true" : "false";
            }

            return cell.ToString();
        }

        private static string FormatNumber(double number)
        {
            var rounded = Math.Round(number, MidpointRounding.AwayFromZero);
            if (Math.Abs(number - rounded) < 0.0000001d)
            {
                return rounded.ToString(CultureInfo.InvariantCulture);
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static string[] SplitCsv(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];
                if (character == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }
    }

    public record ActivityColumn(string Letter, int Index, string Header);

    public record ActivityDataRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);

    public record ActivitySheet(IReadOnlyList<ActivityColumn> Columns, IReadOnlyList<ActivityDataRow> Rows)
    {
        public static ActivitySheet Empty { get; } = new(Array.Empty<ActivityColumn>(), Array.Empty<ActivityDataRow>());
    }
}
