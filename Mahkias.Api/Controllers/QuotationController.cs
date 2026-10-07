using Mahkias.Api.Helpers;
using Mahkias.Api.Modules.Projects;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    public class QuotationController : Controller
    {
        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;

        public QuotationController(MahkiasDbContext context, IUow uow)
        {
            _context = context;
            _uow = uow;
        }

        [HttpGet("/api/projects/{projectId}/quotations")]
        public async Task<IActionResult> ListForProject(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.Quotations.GetByProjectAsync(projectId));
        }

        [HttpGet("/api/quotations/next-code")]
        public async Task<IActionResult> NextCode()
        {
            return Ok(new { code = await _uow.Quotations.NextCodeAsync() });
        }

        [HttpGet("/api/quotations/prices")]
        public async Task<IActionResult> PriceHistory([FromQuery] string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return BadRequest(new { message = "Part number is required." });
            }

            return Ok(await _uow.Quotations.GetPriceHistoryAsync(partNo.Trim()));
        }

        [HttpGet("/api/quotations/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var detail = await _uow.Quotations.GetDetailAsync(id);
            return detail == null ? NotFound() : Ok(detail);
        }

        [HttpGet("/api/projects/{projectId}/quotation-links")]
        public async Task<IActionResult> ActivityLinks(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.Quotations.GetActivityLinksAsync(projectId));
        }

        [HttpPut("/api/quotations/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateQuotationRequest request)
        {
            if (await _uow.Quotations.GetDetailAsync(id) == null)
            {
                return NotFound();
            }

            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            var updated = await _uow.Quotations.UpdateAsync(id, prepared.Args);
            if (!updated)
            {
                return NotFound();
            }

            return Ok(await _uow.Quotations.GetDetailAsync(id));
        }

        [HttpDelete("/api/quotations/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _uow.Quotations.DeleteAsync(id) ? NoContent() : NotFound();
        }

        [HttpPost("/api/quotations")]
        public async Task<IActionResult> Create([FromBody] CreateQuotationRequest request)
        {
            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            var id = await _uow.Quotations.CreateAsync(prepared.Args);
            var projectId = prepared.Args.ProjectIds.First();
            var saved = (await _uow.Quotations.GetByProjectAsync(projectId)).FirstOrDefault(item => item.Id == id);
            return Ok(saved);
        }

        [HttpPost("/api/projects/{projectId}/quotations/excel/mapping")]
        public async Task<IActionResult> ExcelMapping(int projectId, IFormFile file)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Choose an Excel or CSV file." });
            }

            using var stream = file.OpenReadStream();
            var sheet = ActivityExcelReader.Read(stream, file.FileName);
            return Ok(new
            {
                columns = sheet.Columns.Select(column => new { column.Letter, column.Header }),
                rowCount = sheet.Rows.Count,
                suggested = new
                {
                    dsnNo = Suggest(sheet.Columns, "dsnno", "dsn", "dsnnumber"),
                    partNo = Suggest(sheet.Columns, "partno", "partnumber", "part"),
                    description = Suggest(sheet.Columns, "description", "desc"),
                    unitPrice = Suggest(sheet.Columns, "unitprice", "price", "unitcost"),
                    quantity = Suggest(sheet.Columns, "quantity", "qty"),
                    totalPrice = Suggest(sheet.Columns, "totalprice", "total", "linetotal"),
                    supplierReference = Suggest(sheet.Columns, "supplierquotationref", "supplierquoteref", "supplierreference", "quotationref", "quotationreference", "quoteref"),
                }
            });
        }

        [HttpPost("/api/projects/{projectId}/quotations/excel/validate")]
        public async Task<IActionResult> ExcelValidate(int projectId, [FromForm] QuotationExcelUploadForm form)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            if (form?.File == null || form.File.Length == 0)
            {
                return BadRequest(new { message = "Choose an Excel or CSV file." });
            }

            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(form.DsnNoColumn) && string.IsNullOrWhiteSpace(form.DefaultDsnNo))
            {
                missingFields.Add("DSN No");
            }

            if (string.IsNullOrWhiteSpace(form.PartNoColumn) && string.IsNullOrWhiteSpace(form.DefaultPartNo))
            {
                missingFields.Add("Part No");
            }

            if (missingFields.Count > 0)
            {
                return BadRequest(new { message = "Please select a Column Index or enter a Default Value for this required field.", fields = missingFields });
            }

            ActivitySheet sheet;
            using (var stream = form.File.OpenReadStream())
            {
                sheet = ActivityExcelReader.Read(stream, form.File.FileName);
            }

            var plan = QuotationExcelPlanner.Plan(sheet, form);
            var activities = await _uow.Quotations.GetActivitiesForMappingAsync(new[] { projectId });
            var errors = plan.Errors.ToList();
            var items = new List<object>();
            var choices = new List<object>();
            if (errors.Count == 0)
            {
                foreach (var row in plan.Rows)
                {
                    var quantity = row.Quantity < 1 ? 1 : row.Quantity;
                    TryPrice(row.UnitPrice, row.TotalPrice, quantity, out var unit, out var total);
                    var match = QuotationAllocator.Direct(row.DsnNo, row.PartNo, activities);
                    items.Add(new
                    {
                        row = row.Row,
                        dsnNo = row.DsnNo,
                        partNo = row.PartNo,
                        description = row.Description,
                        unitPrice = unit,
                        quantity,
                        totalPrice = total,
                        priceWarning = row.PriceWarning,
                        unitExplicit = row.UnitExplicit,
                        totalExplicit = row.TotalExplicit,
                        activityId = match?.Id,
                        activityGroupId = (int?)null,
                        allocations = Array.Empty<object>(),
                    });
                }
            }

            return Ok(new
            {
                valid = errors.Count == 0,
                ready = errors.Count == 0 && choices.Count == 0 ? items.Count : 0,
                rowCount = sheet.Rows.Count,
                errorCount = errors.Count,
                errors,
                items,
                choices,
            });
        }

        private async Task<(IActionResult Error, CreateQuotationArgs Args)> PrepareAsync(CreateQuotationRequest request)
        {
            if (request == null)
            {
                return (BadRequest(new { message = "Quotation details are required." }), null);
            }

            if (request.SupplierId <= 0)
            {
                return (BadRequest(new { message = "Supplier is required." }), null);
            }

            var projectIds = (request.ProjectIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
            if (projectIds.Count == 0)
            {
                return (BadRequest(new { message = "At least one project is required." }), null);
            }

            var existingProjects = await _context.Projects.AsNoTracking().Where(project => projectIds.Contains(project.Id)).Select(project => project.Id).ToListAsync();
            if (existingProjects.Count != projectIds.Count)
            {
                return (BadRequest(new { message = "One of the selected projects could not be found." }), null);
            }

            var filled = (request.Items ?? new List<QuotationItemRequest>())
                .Select((item, index) => new { item, row = index + 1 })
                .Where(entry => !IsBlank(entry.item))
                .ToList();
            if (filled.Count == 0)
            {
                return (BadRequest(new { message = "Enter at least one quotation item." }), null);
            }

            var activities = await _uow.Quotations.GetActivitiesForMappingAsync(projectIds);
            var groups = await GroupsForAsync(projectIds);
            var handling = QuotationItemMapper.NormalizeHandling(request.DuplicateHandling);
            var errors = new List<ActivityImportIssue>();
            var writes = new List<QuotationItemWrite>();
            foreach (var entry in filled)
            {
                if (string.IsNullOrWhiteSpace(entry.item.DsnNo))
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "DSN No", Message = "DSN number is required." });
                }

                if (string.IsNullOrWhiteSpace(entry.item.PartNo))
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Part No", Message = "Part number is required." });
                }

                if (string.IsNullOrWhiteSpace(entry.item.DsnNo) || string.IsNullOrWhiteSpace(entry.item.PartNo))
                {
                    continue;
                }

                var quantity = entry.item.Quantity ?? 1;
                if (quantity < 1)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Qty", Message = "Quantity must be a whole number of at least 1." });
                    continue;
                }

                if (!TryPrice(entry.item.UnitPrice, entry.item.TotalPrice, quantity, out var unit, out var total))
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Unit Price", Message = "Price must be a valid number." });
                    continue;
                }

                var mapError = AppendWrites(writes, entry.item, activities, groups, quantity, unit, total);
                if (mapError != null)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Part No", Message = mapError });
                }
            }

            if (errors.Count == 0 && handling == "merge")
            {
                writes = MergeWrites(writes);
            }

            if (errors.Count > 0)
            {
                return (BadRequest(new
                {
                    message = "Some quotation items need attention before they can be saved.",
                    errors,
                }), null);
            }

            return (null, new CreateQuotationArgs
            {
                SupplierId = request.SupplierId,
                SupplierReference = TrimTo(request.SupplierReference, 100),
                DriveFileLink = request.DriveFileLink?.Trim(),
                ProjectIds = projectIds,
                Items = writes,
            });
        }

        private async Task<IReadOnlyList<QuotationMapGroup>> GroupsForAsync(IReadOnlyList<int> projectIds)
        {
            var groups = new List<QuotationMapGroup>();
            foreach (var projectId in projectIds.Distinct())
            {
                foreach (var group in await _uow.ActivityGroups.ListAsync(projectId))
                {
                    groups.Add(new QuotationMapGroup
                    {
                        Id = group.Id,
                        ProjectId = group.ProjectId,
                        Name = group.Name,
                    });
                }
            }

            return groups;
        }

        private static string AppendWrites(
            List<QuotationItemWrite> writes,
            QuotationItemRequest item,
            IReadOnlyList<QuotationMapActivity> activities,
            IReadOnlyList<QuotationMapGroup> groups,
            int quantity,
            decimal? unit,
            decimal? total)
        {
            QuotationMapActivity match = null;
            if (item.ActivityId.HasValue)
            {
                match = activities.FirstOrDefault(activity => activity.Id == item.ActivityId.Value);
                if (match == null)
                {
                    return "The selected activity is not on this project.";
                }
            }
            else
            {
                match = QuotationAllocator.Direct(item.DsnNo, item.PartNo, activities);
            }

            if (match == null)
            {
                writes.Add(WriteItem(item, null, null, false, null, item.DsnNo, quantity, unit, total));
                return null;
            }

            var slice = QuotationAllocator.Allocate(quantity, new[] { match }, item.PartNo)[0];
            writes.Add(WriteItem(item, slice.ActivityId, null, slice.IsAlternativePart, slice.AlternativePartId, item.DsnNo, quantity, unit, total));
            return null;
        }

        private static QuotationItemWrite WriteItem(
            QuotationItemRequest item,
            int? activityId,
            int? activityGroupId,
            bool isAlternativePart,
            int? alternativePartId,
            string dsnNo,
            int quantity,
            decimal? unit,
            decimal? total)
        {
            return new QuotationItemWrite
            {
                ActivityId = activityId,
                ActivityGroupId = activityGroupId,
                IsAlternativePart = isAlternativePart,
                AlternativePartId = alternativePartId,
                DsnNo = string.IsNullOrWhiteSpace(dsnNo) ? item.DsnNo?.Trim() : dsnNo.Trim(),
                PartNo = item.PartNo.Trim(),
                PartDescription = item.Description?.Trim(),
                UnitPrice = unit,
                Quantity = quantity,
                TotalPrice = total,
            };
        }

        private static List<QuotationMapLink> ResolveLinks(
            QuotationItemRequest item,
            IReadOnlyList<QuotationMapActivity> activities,
            IReadOnlyList<QuotationMapGroup> groups,
            string handling,
            out string error)
        {
            error = null;
            if (item.ActivityId.HasValue || item.ActivityGroupId.HasValue)
            {
                if (item.ActivityId.HasValue && activities.All(activity => activity.Id != item.ActivityId.Value))
                {
                    error = "The selected activity is not on this project.";
                    return new List<QuotationMapLink>();
                }

                if (item.ActivityGroupId.HasValue && groups.All(group => group.Id != item.ActivityGroupId.Value))
                {
                    error = "The selected group is not on this project.";
                    return new List<QuotationMapLink>();
                }

                var activity = item.ActivityId.HasValue ? activities.First(match => match.Id == item.ActivityId.Value) : null;
                var alternative = activity?.Alternatives.FirstOrDefault(match => string.Equals(match.AlternativePartNo?.Trim(), item.PartNo?.Trim(), StringComparison.OrdinalIgnoreCase));
                return new List<QuotationMapLink>
                {
                    new QuotationMapLink
                    {
                        ActivityId = item.ActivityId,
                        ActivityGroupId = item.ActivityGroupId,
                        IsAlternativePart = alternative != null,
                        AlternativePartId = alternative?.Id,
                    },
                };
            }

            var outcome = QuotationItemMapper.Map(item.DsnNo, item.PartNo, activities, groups, handling);
            if (outcome.Status == QuotationMapStatus.Ambiguous)
            {
                error = outcome.Message;
                return new List<QuotationMapLink>();
            }

            return outcome.Links;
        }

        private static List<QuotationItemWrite> MergeWrites(List<QuotationItemWrite> writes)
        {
            return writes
                .GroupBy(item => (item.ActivityId, item.ActivityGroupId, (item.PartNo ?? string.Empty).Trim().ToUpperInvariant()))
                .Select(group =>
                {
                    var first = group.First();
                    var quantity = group.Sum(item => item.Quantity);
                    decimal? total = group.All(item => item.TotalPrice == null && item.UnitPrice == null)
                        ? null
                        : group.Sum(item => item.TotalPrice ?? (item.UnitPrice.HasValue ? decimal.Round(item.UnitPrice.Value * item.Quantity, 4, MidpointRounding.AwayFromZero) : 0));
                    decimal? unit = total.HasValue && quantity > 0
                        ? decimal.Round(total.Value / quantity, 4, MidpointRounding.AwayFromZero)
                        : null;
                    return new QuotationItemWrite
                    {
                        ActivityId = first.ActivityId,
                        ActivityGroupId = first.ActivityGroupId,
                        IsAlternativePart = first.IsAlternativePart,
                        AlternativePartId = first.AlternativePartId,
                        DsnNo = first.DsnNo,
                        PartNo = first.PartNo,
                        PartDescription = first.PartDescription,
                        UnitPrice = unit,
                        Quantity = quantity,
                        TotalPrice = total,
                    };
                })
                .ToList();
        }

        private async Task<bool> ProjectExists(int projectId)
        {
            return await _context.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId);
        }

        private static bool IsBlank(QuotationItemRequest item)
        {
            if (item == null)
            {
                return true;
            }

            return !item.UnitPrice.HasValue && !item.TotalPrice.HasValue;
        }

        private static bool TryPrice(decimal? unitPrice, decimal? totalPrice, int quantity, out decimal? unit, out decimal? total)
        {
            unit = null;
            total = null;
            if (!unitPrice.HasValue && !totalPrice.HasValue)
            {
                return true;
            }

            if (unitPrice.HasValue && unitPrice.Value < 0)
            {
                return false;
            }

            if (totalPrice.HasValue && totalPrice.Value < 0)
            {
                return false;
            }

            if (unitPrice.HasValue && totalPrice.HasValue)
            {
                unit = decimal.Round(unitPrice.Value, 4, MidpointRounding.AwayFromZero);
                total = decimal.Round(totalPrice.Value, 4, MidpointRounding.AwayFromZero);
                return true;
            }

            if (unitPrice.HasValue)
            {
                unit = decimal.Round(unitPrice.Value, 4, MidpointRounding.AwayFromZero);
                total = decimal.Round(unit.Value * quantity, 4, MidpointRounding.AwayFromZero);
                return true;
            }

            if (totalPrice.HasValue && quantity > 0)
            {
                if (totalPrice.Value < 0)
                {
                    return false;
                }

                total = decimal.Round(totalPrice.Value, 4, MidpointRounding.AwayFromZero);
                unit = decimal.Round(total.Value / quantity, 4, MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }

        private static object PricedItem(string dsnNo, string partNo, string description, decimal? unitPrice, decimal? totalPrice, int quantity)
        {
            TryPrice(unitPrice, totalPrice, quantity < 1 ? 1 : quantity, out var unit, out var total);
            return new
            {
                dsnNo,
                partNo,
                description,
                unitPrice = unit,
                quantity = quantity < 1 ? 1 : quantity,
                totalPrice = total,
            };
        }

        private static string TrimTo(string value, int length)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Length <= length ? text : text.Substring(0, length);
        }

        private static string Suggest(IReadOnlyList<ActivityColumn> columns, params string[] names)
        {
            return columns.FirstOrDefault(column => names.Contains(Normalize(column.Header)))?.Letter;
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }
    }
}
