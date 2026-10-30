using Mahkias.Api.Helpers;
using Mahkias.Api.Security;
using Mahkias.Api.Modules.Projects;
using Mahkias.Api.Services;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class QuotationController : Controller
    {
        private const long MaxImportFileBytes = 52_428_800;
        private const long MaxImportRequestBytes = 104_857_600;

        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;
        private readonly ProjectDriveFolders _folders;

        public QuotationController(MahkiasDbContext context, IUow uow, IGoogleDriveService drive, ProjectDriveFolders folders)
        {
            _context = context;
            _uow = uow;
            _drive = drive;
            _folders = folders;
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

            var projectId = prepared.Args.ProjectIds.First();
            var id = await _uow.Quotations.ImportAsync(prepared.Args, null, HttpContext.RequestAborted);
            var saved = (await _uow.Quotations.GetByProjectAsync(projectId)).FirstOrDefault(item => item.Id == id);
            return Ok(saved);
        }

        [HttpPost("/api/projects/{projectId:int}/quotations/import")]
        [RequestSizeLimit(MaxImportRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxImportRequestBytes)]
        public async Task<IActionResult> Import(int projectId, [FromForm] QuotationImportForm form, CancellationToken cancellationToken)
        {
            if (form == null)
            {
                return BadRequest(new { message = "Quotation details are required." });
            }

            List<QuotationItemRequest> items;
            try
            {
                items = string.IsNullOrWhiteSpace(form.Items)
                    ? new List<QuotationItemRequest>()
                    : JsonSerializer.Deserialize<List<QuotationItemRequest>>(form.Items, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<QuotationItemRequest>();
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Quotation items could not be read." });
            }

            var request = new CreateQuotationRequest
            {
                SupplierId = form.SupplierId,
                SupplierReference = form.SupplierReference,
                CurrencyId = form.CurrencyId,
                ProjectIds = new List<int> { projectId },
                Items = items,
            };
            var dateError = ApplyFormDates(form, request);
            if (dateError != null)
            {
                return dateError;
            }

            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            MemoryStream buffer = null;
            string uploadName = null;
            string contentType = null;
            string fileType = null;
            var file = form.File;
            if (file != null && file.Length > 0)
            {
                if (file.Length > MaxImportFileBytes)
                {
                    return BadRequest(new { message = "Each document must be 50 MB or smaller." });
                }

                if (!DriveDocumentTypes.TryDescribe(file.FileName, out uploadName, out contentType, out fileType, out var fileError))
                {
                    return BadRequest(new { message = fileError });
                }

                buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
            }

            try
            {
                var id = await _uow.Quotations.ImportAsync(
                    prepared.Args,
                    buffer == null ? null : (quotationId, token) => UploadQuotationFileAsync(projectId, quotationId, 0, uploadName, contentType, fileType, buffer, token),
                    cancellationToken);
                var saved = (await _uow.Quotations.GetByProjectAsync(projectId)).FirstOrDefault(item => item.Id == id);
                return Ok(saved);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            finally
            {
                if (buffer != null)
                {
                    await buffer.DisposeAsync();
                }
            }
        }

        [HttpPost("/api/projects/{projectId:int}/quotations/{quotationId:int}/save")]
        [RequestSizeLimit(MaxImportRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxImportRequestBytes)]
        public async Task<IActionResult> Save(int projectId, int quotationId, [FromForm] QuotationImportForm form, CancellationToken cancellationToken)
        {
            if (form == null)
            {
                return BadRequest(new { message = "Quotation details are required." });
            }

            if (form.File == null || form.File.Length == 0)
            {
                return BadRequest(new { message = "Choose a document to upload." });
            }

            List<QuotationItemRequest> items;
            try
            {
                items = string.IsNullOrWhiteSpace(form.Items)
                    ? new List<QuotationItemRequest>()
                    : JsonSerializer.Deserialize<List<QuotationItemRequest>>(form.Items, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<QuotationItemRequest>();
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Quotation items could not be read." });
            }

            var request = new CreateQuotationRequest
            {
                SupplierId = form.SupplierId,
                SupplierReference = form.SupplierReference,
                CurrencyId = form.CurrencyId,
                ProjectIds = new List<int> { projectId },
                Items = items,
            };
            var dateError = ApplyFormDates(form, request);
            if (dateError != null)
            {
                return dateError;
            }

            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            if (form.File.Length > MaxImportFileBytes)
            {
                return BadRequest(new { message = "Each document must be 50 MB or smaller." });
            }

            if (!DriveDocumentTypes.TryDescribe(form.File.FileName, out var uploadName, out var contentType, out var fileType, out var fileError))
            {
                return BadRequest(new { message = fileError });
            }

            var buffer = new MemoryStream();
            await form.File.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            try
            {
                await _uow.Quotations.SaveExistingAsync(
                    quotationId,
                    prepared.Args,
                    (id, token) => UploadQuotationFileAsync(projectId, id, form.DocumentId, uploadName, contentType, fileType, buffer, token),
                    cancellationToken);
                var saved = await _uow.Quotations.GetDetailAsync(quotationId);
                return saved == null ? NotFound() : Ok(saved);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            finally
            {
                await buffer.DisposeAsync();
            }
        }

        private async Task<CreateQuotationDocumentArgs> UploadQuotationFileAsync(int projectId, int quotationId, int documentId, string uploadName, string contentType, string fileType, MemoryStream buffer, CancellationToken cancellationToken)
        {
            var folderId = await _folders.EnsureQuotationsFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(folderId))
            {
                throw new InvalidOperationException("The quotation folder could not be prepared.");
            }

            buffer.Position = 0;
            var uploaded = await _drive.UploadAsync(folderId, uploadName, contentType, buffer, cancellationToken);
            if (documentId > 0)
            {
                return new UpdateQuotationDocumentArgs
                {
                    ProjectId = projectId,
                    QuotationId = quotationId,
                    DocumentId = documentId,
                    FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? uploadName : uploaded.FileName,
                    FileType = fileType,
                    GoogleDriveFileId = uploaded.FileId,
                    WebViewLink = uploaded.WebViewLink ?? string.Empty,
                };
            }

            return new CreateQuotationDocumentArgs
            {
                ProjectId = projectId,
                QuotationId = quotationId,
                FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? uploadName : uploaded.FileName,
                FileType = fileType,
                GoogleDriveFileId = uploaded.FileId,
                WebViewLink = uploaded.WebViewLink ?? string.Empty,
            };
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
                    condition = Suggest(sheet.Columns, "condition", "itemcondition", "cond"),
                    leadTime = Suggest(sheet.Columns, "leadtime", "lead", "deliveryleadtime"),
                    minimumQty = Suggest(sheet.Columns, "minimumqty", "moq", "minqty", "minimumorderqty", "minimumquantity"),
                    quotedDate = Suggest(sheet.Columns, "quoteddate", "quotedate", "quotationdate", "quotedon"),
                    validUntil = Suggest(sheet.Columns, "validuntil", "validto", "expirydate", "expiry", "validthru"),
                    unitPrice = Suggest(sheet.Columns, "unitprice", "price", "unitcost"),
                    quantity = Suggest(sheet.Columns, "quantity", "qty"),
                    totalPrice = Suggest(sheet.Columns, "totalprice", "total", "linetotal"),
                    currency = Suggest(sheet.Columns, "currency", "currencycode", "ccy"),
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
            var currencies = await _uow.Currencies.ListActiveAsync();
            var errors = plan.Errors.ToList();
            var items = new List<object>();
            var choices = new List<object>();
            DateTime? quotedDate = null;
            DateTime? validUntil = null;
            if (errors.Count == 0)
            {
                foreach (var row in plan.Rows)
                {
                    if (!SameDate(ref quotedDate, row.QuotedDate))
                    {
                        errors.Add(new ActivityImportIssue { Row = row.Row, Field = "Quoted Date", Message = "Quoted date does not match the other rows." });
                    }

                    if (!SameDate(ref validUntil, row.ValidUntil))
                    {
                        errors.Add(new ActivityImportIssue { Row = row.Row, Field = "Valid Until", Message = "Valid until does not match the other rows." });
                    }

                    var quantity = row.Quantity < 1 ? 1 : row.Quantity;
                    TryPrice(row.UnitPrice, row.TotalPrice, EffectiveQuantity(row.MinimumQty, quantity), out var unit, out var total);
                    var match = QuotationAllocator.Direct(row.DsnNo, row.PartNo, activities);
                    items.Add(new
                    {
                        row = row.Row,
                        dsnNo = row.DsnNo,
                        partNo = row.PartNo,
                        description = row.Description,
                        condition = row.Condition,
                        leadTime = row.LeadTime,
                        minimumQty = row.MinimumQty,
                        unitPrice = unit,
                        quantity,
                        totalPrice = total,
                        currencyId = ResolveImportedCurrency(row.CurrencyCode, form.CurrencyId, currencies),
                        currencyExplicit = IsKnownCurrency(row.CurrencyCode, currencies),
                        priceWarning = row.PriceWarning,
                        unitExplicit = row.UnitExplicit,
                        totalExplicit = row.TotalExplicit,
                        activityId = match?.Id,
                        activityGroupId = (int?)null,
                        allocations = Array.Empty<object>(),
                    });
                }
            }

            if (errors.Count == 0 && quotedDate.HasValue && validUntil.HasValue && validUntil.Value.Date < quotedDate.Value.Date)
            {
                errors.Add(new ActivityImportIssue { Row = 0, Field = "Valid Until", Message = "Valid until cannot be earlier than the quoted date." });
            }

            return Ok(new
            {
                valid = errors.Count == 0,
                ready = errors.Count == 0 && choices.Count == 0 ? items.Count : 0,
                rowCount = sheet.Rows.Count,
                errorCount = errors.Count,
                errors,
                items = errors.Count == 0 ? items : new List<object>(),
                choices = errors.Count == 0 ? choices : new List<object>(),
                quotedDate = errors.Count == 0 ? quotedDate : null,
                validUntil = errors.Count == 0 ? validUntil : null,
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

            if (request.QuotedDate.HasValue && request.ValidUntil.HasValue && request.ValidUntil.Value.Date < request.QuotedDate.Value.Date)
            {
                return (BadRequest(new { message = "Valid until cannot be earlier than the quoted date." }), null);
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
            var currencies = await _uow.Currencies.ListActiveAsync();
            var headerCurrencyId = ResolveCurrencyId(request.CurrencyId, null, currencies);
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

                if (!string.IsNullOrWhiteSpace(entry.item.Condition) && entry.item.Condition.Trim().Length > 100)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Condition", Message = "Condition must be 100 characters or fewer." });
                }

                if (!string.IsNullOrWhiteSpace(entry.item.LeadTime) && entry.item.LeadTime.Trim().Length > 100)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Lead Time", Message = "Lead time must be 100 characters or fewer." });
                }

                if (entry.item.MinimumQty is < 1)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "MOQ", Message = "Minimum quantity must be a positive number." });
                }

                if (string.IsNullOrWhiteSpace(entry.item.DsnNo) || string.IsNullOrWhiteSpace(entry.item.PartNo)
                    || (!string.IsNullOrWhiteSpace(entry.item.Condition) && entry.item.Condition.Trim().Length > 100)
                    || (!string.IsNullOrWhiteSpace(entry.item.LeadTime) && entry.item.LeadTime.Trim().Length > 100)
                    || entry.item.MinimumQty is < 1)
                {
                    continue;
                }

                var quantity = entry.item.Quantity ?? 1;
                if (quantity < 1)
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Qty", Message = "Quantity must be a whole number of at least 1." });
                    continue;
                }

                if (!TryPrice(entry.item.UnitPrice, entry.item.TotalPrice, EffectiveQuantity(entry.item.MinimumQty, quantity), out var unit, out var total))
                {
                    errors.Add(new ActivityImportIssue { Row = entry.row, Field = "Unit Price", Message = "Price must be a valid number." });
                    continue;
                }

                var mapError = AppendWrites(writes, entry.item, activities, groups, quantity, unit, total, ResolveCurrencyId(entry.item.CurrencyId, headerCurrencyId, currencies));
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

            int? currencyId = headerCurrencyId;

            return (null, new CreateQuotationArgs
            {
                SupplierId = request.SupplierId,
                SupplierReference = TrimTo(request.SupplierReference, 100),
                DriveFileLink = request.DriveFileLink?.Trim(),
                CurrencyId = currencyId,
                QuotedDate = request.QuotedDate?.Date,
                ValidUntil = request.ValidUntil?.Date,
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
            decimal? total,
            int? currencyId)
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
                writes.Add(WriteItem(item, null, null, false, null, item.DsnNo, quantity, unit, total, currencyId));
                return null;
            }

            var slice = QuotationAllocator.Allocate(quantity, new[] { match }, item.PartNo)[0];
            writes.Add(WriteItem(item, slice.ActivityId, null, slice.IsAlternativePart, slice.AlternativePartId, item.DsnNo, quantity, unit, total, currencyId));
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
            decimal? total,
            int? currencyId)
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
                Condition = TrimOptional(item.Condition),
                LeadTime = TrimOptional(item.LeadTime),
                MinimumQty = item.MinimumQty is > 0 ? item.MinimumQty : null,
                UnitPrice = unit,
                Quantity = quantity,
                TotalPrice = total,
                CurrencyId = currencyId,
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
                        : group.Sum(item => item.TotalPrice ?? (item.UnitPrice.HasValue ? decimal.Round(item.UnitPrice.Value * EffectiveQuantity(item.MinimumQty, item.Quantity), 4, MidpointRounding.AwayFromZero) : 0));
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
                        Condition = first.Condition,
                        LeadTime = first.LeadTime,
                        MinimumQty = first.MinimumQty,
                        UnitPrice = unit,
                        Quantity = quantity,
                        TotalPrice = total,
                        CurrencyId = first.CurrencyId,
                    };
                })
                .ToList();
        }

        private async Task<bool> ProjectExists(int projectId)
        {
            return await _context.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId);
        }

        private static bool SameDate(ref DateTime? current, DateTime? next)
        {
            if (!next.HasValue)
            {
                return true;
            }

            if (!current.HasValue)
            {
                current = next.Value.Date;
                return true;
            }

            return current.Value.Date == next.Value.Date;
        }

        private IActionResult ApplyFormDates(QuotationImportForm form, CreateQuotationRequest request)
        {
            if (!TryFormDate(form?.QuotedDate, out var quotedDate, out var quotedError))
            {
                return BadRequest(new { message = "Quoted date " + quotedError });
            }

            if (!TryFormDate(form?.ValidUntil, out var validUntil, out var validError))
            {
                return BadRequest(new { message = "Valid until " + validError });
            }

            request.QuotedDate = quotedDate;
            request.ValidUntil = validUntil;
            return null;
        }

        private static bool TryFormDate(string value, out DateTime? date, out string error)
        {
            date = null;
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                || DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                date = parsed.Date;
                return true;
            }

            error = "'" + value.Trim() + "' is not a valid date.";
            return false;
        }

        private static string TrimOptional(string value)
        {
            var text = value?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool IsBlank(QuotationItemRequest item)
        {
            if (item == null)
            {
                return true;
            }

            return !item.UnitPrice.HasValue && !item.TotalPrice.HasValue;
        }

        private static int? ResolveCurrencyId(int? preferred, int? fallback, IReadOnlyList<CurrencyResult> currencies)
        {
            if (preferred is > 0 && currencies.Any(currency => currency.Id == preferred.Value))
            {
                return preferred;
            }

            if (fallback is > 0 && currencies.Any(currency => currency.Id == fallback.Value))
            {
                return fallback;
            }

            return currencies.FirstOrDefault(currency => currency.IsDefault)?.Id
                ?? currencies.FirstOrDefault(currency => string.Equals(currency.Code, "USD", StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private static int? ResolveImportedCurrency(string code, int? headerCurrencyId, IReadOnlyList<CurrencyResult> currencies)
        {
            var matched = MatchCurrency(code, currencies);
            return matched?.Id ?? ResolveCurrencyId(headerCurrencyId, null, currencies);
        }

        private static bool IsKnownCurrency(string code, IReadOnlyList<CurrencyResult> currencies)
        {
            return MatchCurrency(code, currencies) != null;
        }

        private static CurrencyResult MatchCurrency(string code, IReadOnlyList<CurrencyResult> currencies)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var token = code.Trim();
            return currencies.FirstOrDefault(currency => string.Equals(currency.Code, token, StringComparison.OrdinalIgnoreCase));
        }

        private static int EffectiveQuantity(int? minimumQty, int quantity)
        {
            if (minimumQty is > 0)
            {
                return minimumQty.Value;
            }

            return quantity > 0 ? quantity : 1;
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
