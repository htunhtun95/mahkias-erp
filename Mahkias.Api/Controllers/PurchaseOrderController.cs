using Mahkias.Api.Helpers;
using Mahkias.Api.Modules.Projects;
using Mahkias.Api.Security;
using Mahkias.Api.Services;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Mahkias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    public class PurchaseOrderController : Controller
    {
        private const long MaxFileBytes = 52_428_800;
        private const long MaxRequestBytes = 104_857_600;

        private readonly MahkiasDbContext _context;
        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;
        private readonly ProjectDriveFolders _folders;

        public PurchaseOrderController(MahkiasDbContext context, IUow uow, IGoogleDriveService drive, ProjectDriveFolders folders)
        {
            _context = context;
            _uow = uow;
            _drive = drive;
            _folders = folders;
        }

        [HttpGet("/api/purchase-order-statuses")]
        public async Task<IActionResult> Statuses()
        {
            return Ok(await _uow.PurchaseOrders.GetStatusesAsync());
        }

        [HttpGet("/api/projects/{projectId:int}/purchase-orders")]
        public async Task<IActionResult> List(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.PurchaseOrders.GetByProjectAsync(projectId));
        }

        [HttpGet("/api/projects/{projectId:int}/purchase-orders/lines")]
        public async Task<IActionResult> Lines(int projectId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.PurchaseOrders.GetLinesByProjectAsync(projectId));
        }

        [HttpGet("/api/purchase-orders/next-code")]
        public async Task<IActionResult> NextCode()
        {
            return Ok(new { code = await _uow.PurchaseOrders.NextCodeAsync() });
        }

        [HttpGet("/api/purchase-orders/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var detail = await _uow.PurchaseOrders.GetDetailAsync(id);
            return detail == null ? NotFound() : Ok(detail);
        }

        [HttpPost("/api/purchase-orders")]
        public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request)
        {
            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            try
            {
                var id = await _uow.PurchaseOrders.CreateAsync(prepared.Args);
                return Ok(await _uow.PurchaseOrders.GetDetailAsync(id));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("/api/purchase-orders/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreatePurchaseOrderRequest request)
        {
            if (await _uow.PurchaseOrders.GetDetailAsync(id) == null)
            {
                return NotFound();
            }

            var prepared = await PrepareAsync(request);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            try
            {
                await _uow.PurchaseOrders.SaveExistingAsync(id, prepared.Args, null);
                return Ok(await _uow.PurchaseOrders.GetDetailAsync(id));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("/api/purchase-orders/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _uow.PurchaseOrders.DeleteAsync(id, ActorName(), ActorEmail(), null) ? NoContent() : NotFound();
        }

        [HttpPost("/api/projects/{projectId:int}/purchase-orders/import")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Import(int projectId, [FromForm] PurchaseOrderImportForm form, CancellationToken cancellationToken)
        {
            var prepared = await PrepareFormAsync(projectId, form);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            var activityError = await MatchImportedActivities(projectId, prepared.Args);
            if (activityError != null)
            {
                return activityError;
            }

            var upload = await ReadUploadAsync(form?.File, cancellationToken);
            if (upload.Error != null)
            {
                return upload.Error;
            }

            try
            {
                var id = await _uow.PurchaseOrders.ImportAsync(
                    prepared.Args,
                    upload.Buffer == null ? null : (purchaseOrderId, token) => UploadFileAsync(projectId, purchaseOrderId, upload.Buffer, upload.FileName, upload.ContentType, upload.FileType, token),
                    cancellationToken);
                return Ok(await _uow.PurchaseOrders.GetDetailAsync(id));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                return BadRequest(new { message = ex.Message });
            }
            finally
            {
                if (upload.Buffer != null)
                {
                    await upload.Buffer.DisposeAsync();
                }
            }
        }

        [HttpPost("/api/projects/{projectId:int}/purchase-orders/{purchaseOrderId:int}/save")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Save(int projectId, int purchaseOrderId, [FromForm] PurchaseOrderImportForm form, CancellationToken cancellationToken)
        {
            if (await _uow.PurchaseOrders.GetDetailAsync(purchaseOrderId) == null)
            {
                return NotFound();
            }

            var prepared = await PrepareFormAsync(projectId, form);
            if (prepared.Error != null)
            {
                return prepared.Error;
            }

            var upload = await ReadUploadAsync(form?.File, cancellationToken);
            if (upload.Error != null)
            {
                return upload.Error;
            }

            try
            {
                await _uow.PurchaseOrders.SaveExistingAsync(
                    purchaseOrderId,
                    prepared.Args,
                    upload.Buffer == null ? null : (_, token) => UploadFileAsync(projectId, purchaseOrderId, upload.Buffer, upload.FileName, upload.ContentType, upload.FileType, token),
                    cancellationToken);
                return Ok(await _uow.PurchaseOrders.GetDetailAsync(purchaseOrderId));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                return BadRequest(new { message = ex.Message });
            }
            finally
            {
                if (upload.Buffer != null)
                {
                    await upload.Buffer.DisposeAsync();
                }
            }
        }

        [HttpPost("/api/projects/{projectId:int}/purchase-orders/excel/validate")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> ValidateExcel(int projectId, [FromForm] QuotationExcelUploadForm form, CancellationToken cancellationToken)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            if (form?.File == null || form.File.Length == 0)
            {
                return BadRequest(new { message = "Choose a spreadsheet to check." });
            }

            ActivitySheet sheet;
            try
            {
                await using var stream = form.File.OpenReadStream();
                sheet = ActivityExcelReader.Read(stream, form.File.FileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var planned = QuotationExcelPlanner.Plan(sheet, form);
            var activities = await _uow.PurchaseOrders.GetProjectActivitiesAsync(projectId);
            var errors = new List<ActivityImportIssue>(planned.Errors);
            var items = new List<object>();
            foreach (var row in planned.Rows)
            {
                var matches = MatchActivities(activities, row.DsnNo, row.PartNo);
                int? activityId = null;
                if (matches.Count == 0)
                {
                    errors.Add(new ActivityImportIssue { Row = row.Row, Field = "Activity", Message = "No activity on this project matches this DSN and part." });
                }
                else if (matches.Count > 1)
                {
                    errors.Add(new ActivityImportIssue { Row = row.Row, Field = "Activity", Message = "More than one activity matches this DSN and part." });
                }
                else
                {
                    activityId = matches[0].Id;
                }

                if (row.Quantity <= 0)
                {
                    errors.Add(new ActivityImportIssue { Row = row.Row, Field = "Quantity", Message = "Each purchase order line needs a quantity greater than zero." });
                }

                items.Add(new
                {
                    row = row.Row,
                    activityId,
                    dsnNo = row.DsnNo,
                    partNo = row.PartNo,
                    description = row.Description,
                    condition = row.Condition,
                    unit = row.UnitName,
                    leadTime = row.LeadTime,
                    currencyCode = row.CurrencyCode,
                    quantity = row.Quantity,
                    unitPrice = row.UnitPrice,
                    totalPrice = row.TotalPrice,
                });
            }

            return Ok(new
            {
                valid = errors.Count == 0 && items.Count > 0,
                ready = items.Count,
                rowCount = planned.Rows.Count,
                errorCount = errors.Count,
                errors,
                items,
            });
        }

        [HttpGet("/api/projects/{projectId:int}/purchase-orders/{purchaseOrderId:int}/documents")]
        public async Task<IActionResult> Documents(int projectId, int purchaseOrderId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return Ok(await _uow.PurchaseOrders.ListDocumentsAsync(projectId, purchaseOrderId));
        }

        [HttpPut("/api/projects/{projectId:int}/purchase-orders/{purchaseOrderId:int}/documents/order")]
        public async Task<IActionResult> OrderDocuments(int projectId, int purchaseOrderId, [FromBody] DocumentOrderRequest request)
        {
            if (request?.Documents == null)
            {
                return BadRequest(new { message = "Document order is required." });
            }

            await _uow.PurchaseOrders.OrderDocumentsAsync(projectId, purchaseOrderId, request.Documents, request.OriginalDocumentId);
            return Ok(await _uow.PurchaseOrders.ListDocumentsAsync(projectId, purchaseOrderId));
        }

        [HttpDelete("/api/projects/{projectId:int}/purchase-orders/{purchaseOrderId:int}/documents/{documentId:int}")]
        public async Task<IActionResult> RemoveDocument(int projectId, int purchaseOrderId, int documentId)
        {
            if (!await ProjectExists(projectId))
            {
                return NotFound();
            }

            return await _uow.PurchaseOrders.RemoveDocumentAsync(projectId, purchaseOrderId, documentId) ? NoContent() : NotFound();
        }

        [HttpPost("/api/projects/{projectId:int}/purchase-orders/{purchaseOrderId:int}/documents")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> UploadDocument(int projectId, int purchaseOrderId, IFormFile file, [FromForm] bool isOriginal, [FromForm] int? sortOrder, CancellationToken cancellationToken)
        {
            if (await _uow.PurchaseOrders.GetDetailAsync(purchaseOrderId) == null)
            {
                return NotFound();
            }

            var upload = await ReadUploadAsync(file, cancellationToken);
            if (upload.Error != null)
            {
                return upload.Error;
            }

            if (upload.Buffer == null)
            {
                return BadRequest(new { message = "Choose a document to upload." });
            }

            try
            {
                var saved = await UploadFileAsync(projectId, purchaseOrderId, upload.Buffer, upload.FileName, upload.ContentType, upload.FileType, cancellationToken);
                saved.FileSize = upload.Buffer.Length;
                saved.IsOriginal = isOriginal;
                saved.SortOrder = sortOrder;
                return Ok(await _uow.PurchaseOrders.AddDocumentAsync(saved));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                return BadRequest(new { message = ex.Message });
            }
            finally
            {
                await upload.Buffer.DisposeAsync();
            }
        }

        private async Task<CreatePurchaseOrderDocumentArgs> UploadFileAsync(int projectId, int purchaseOrderId, MemoryStream buffer, string fileName, string contentType, string fileType, CancellationToken cancellationToken)
        {
            var folderId = await _folders.EnsurePurchasesFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(folderId))
            {
                throw new InvalidOperationException("The purchases folder could not be prepared.");
            }

            buffer.Position = 0;
            var uploaded = await _drive.UploadAsync(folderId, fileName, contentType, buffer, cancellationToken);
            return new CreatePurchaseOrderDocumentArgs
            {
                ProjectId = projectId,
                PurchaseOrderId = purchaseOrderId,
                FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? fileName : uploaded.FileName,
                FileType = fileType,
                GoogleDriveFileId = uploaded.FileId,
                WebViewLink = uploaded.WebViewLink ?? string.Empty,
                FileSize = buffer.Length,
            };
        }

        private async Task<(IActionResult Error, CreatePurchaseOrderArgs Args)> PrepareFormAsync(int projectId, PurchaseOrderImportForm form)
        {
            if (form == null)
            {
                return (BadRequest(new { message = "Purchase order details are required." }), null);
            }

            if (!await ProjectExists(projectId))
            {
                return (NotFound(), null);
            }

            List<PurchaseOrderItemRequest> items;
            try
            {
                items = string.IsNullOrWhiteSpace(form.Items)
                    ? new List<PurchaseOrderItemRequest>()
                    : JsonSerializer.Deserialize<List<PurchaseOrderItemRequest>>(form.Items, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<PurchaseOrderItemRequest>();
            }
            catch (JsonException)
            {
                return (BadRequest(new { message = "Purchase order lines could not be read." }), null);
            }

            var charges = ReadFormCharges(form.Charges);
            if (charges.Error != null)
            {
                return (BadRequest(new { message = charges.Error }), null);
            }

            DateTime? poDate = null;
            if (!string.IsNullOrWhiteSpace(form.PoDate))
            {
                if (!DateTime.TryParse(form.PoDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                    && !DateTime.TryParse(form.PoDate, out parsed))
                {
                    return (BadRequest(new { message = "PO date could not be read." }), null);
                }

                poDate = parsed.Date;
            }

            return await PrepareAsync(new CreatePurchaseOrderRequest
            {
                Code = form.Code,
                SupplierId = form.SupplierId,
                PoDate = poDate,
                ShipToAddress = form.ShipToAddress,
                CurrencyId = form.CurrencyId,
                Status = form.Status,
                ProjectIds = new List<int> { projectId },
                Items = items,
                Charges = charges.Charges,
            });
        }

        private async Task<(IActionResult Error, CreatePurchaseOrderArgs Args)> PrepareAsync(CreatePurchaseOrderRequest request)
        {
            if (request == null)
            {
                return (BadRequest(new { message = "Purchase order details are required." }), null);
            }

            var code = (request.Code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code))
            {
                return (BadRequest(new { message = "Enter a PO number." }), null);
            }

            if (code.Length > 50)
            {
                return (BadRequest(new { message = "PO number must be 50 characters or fewer." }), null);
            }

            if (request.SupplierId <= 0)
            {
                return (BadRequest(new { message = "Choose a supplier." }), null);
            }

            var projectIds = (request.ProjectIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
            if (projectIds.Count != 1 || !await ProjectExists(projectIds[0]))
            {
                return (BadRequest(new { message = "The purchase order needs a project." }), null);
            }

            var shipTo = (request.ShipToAddress ?? string.Empty).Trim();
            if (shipTo.Length > 500)
            {
                return (BadRequest(new { message = "Ship-to address must be 500 characters or fewer." }), null);
            }

            var writes = new List<PurchaseOrderItemWrite>();
            foreach (var item in request.Items ?? new List<PurchaseOrderItemRequest>())
            {
                var error = AppendItem(writes, item);
                if (error != null)
                {
                    return (BadRequest(new { message = error }), null);
                }
            }

            if (writes.Count == 0)
            {
                return (BadRequest(new { message = "Add at least one purchase order line with a unit price." }), null);
            }

            var charges = NormalizeCharges(request.Charges);
            if (charges.Error != null)
            {
                return (BadRequest(new { message = charges.Error }), null);
            }

            var status = (request.Status ?? string.Empty).Trim();
            if (status.Length > 50)
            {
                return (BadRequest(new { message = "Choose a purchase order status." }), null);
            }

            return (null, new CreatePurchaseOrderArgs
            {
                Code = code,
                SupplierId = request.SupplierId,
                PoDate = request.PoDate?.Date,
                ShipToAddress = shipTo,
                CurrencyId = request.CurrencyId,
                StatusCode = status,
                ActorName = ActorName(),
                ActorEmail = ActorEmail(),
                ProjectIds = projectIds,
                Items = writes,
                Charges = charges.Charges,
            });
        }

        private string ActorName()
        {
            return ActorHeader("X-User-Name", 200);
        }

        private string ActorEmail()
        {
            return ActorHeader("X-User-Email", 320);
        }

        private string ActorHeader(string name, int maxLength)
        {
            if (!Request.Headers.TryGetValue(name, out var values))
            {
                return null;
            }

            var raw = values.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(raw.Trim());
            }
            catch (UriFormatException)
            {
                decoded = raw.Trim();
            }

            if (string.IsNullOrWhiteSpace(decoded))
            {
                return null;
            }

            return decoded.Length <= maxLength ? decoded : decoded.Substring(0, maxLength);
        }

        private static string AppendItem(List<PurchaseOrderItemWrite> writes, PurchaseOrderItemRequest item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.UnitPrice == null)
            {
                return null;
            }

            if (item.UnitPrice < 0)
            {
                return "Purchase order amounts must be zero or more.";
            }

            var dsn = TrimTo(item.DsnNo, 100);
            var part = TrimTo(item.PartNo, 200);
            var activityId = item.ActivityId > 0 ? item.ActivityId : null;
            if (activityId == null && (string.IsNullOrWhiteSpace(dsn) || string.IsNullOrWhiteSpace(part)))
            {
                return "Each line that is not linked to an activity needs a DSN No and a Part No.";
            }

            if (item.Quantity == null || item.Quantity <= 0)
            {
                return "Each purchase order line needs a quantity greater than zero.";
            }

            if (item.LeadTime is < 0)
            {
                return LeadTimeDays.Message;
            }

            var quantity = item.Quantity.Value;
            var unitPrice = item.UnitPrice.Value;
            var totalPrice = item.TotalPrice ?? decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
            if (totalPrice < 0)
            {
                return "Purchase order amounts must be zero or more.";
            }

            writes.Add(new PurchaseOrderItemWrite
            {
                ActivityId = activityId,
                QuotationId = activityId.HasValue && item.QuotationId > 0 ? item.QuotationId : null,
                DsnNo = dsn,
                PartNo = part,
                PartDescription = TrimTo(item.Description, 500),
                Condition = TrimTo(item.Condition, 100),
                Unit = TrimTo(item.Unit, 100),
                LeadTime = item.LeadTime,
                Quantity = decimal.Round(quantity, 2, MidpointRounding.AwayFromZero),
                UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
                TotalPrice = decimal.Round(totalPrice, 2, MidpointRounding.AwayFromZero),
                CurrencyId = item.CurrencyId,
            });
            return null;
        }

        private static (string Error, List<QuotationChargeRequest> Charges) ReadFormCharges(string json)
        {
            if (json == null)
            {
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return (null, new List<QuotationChargeRequest>());
            }

            try
            {
                var charges = JsonSerializer.Deserialize<List<QuotationChargeRequest>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<QuotationChargeRequest>();
                return (null, charges);
            }
            catch (JsonException)
            {
                return ("Additional charges could not be read.", null);
            }
        }

        private static (string Error, List<PurchaseOrderChargeWrite> Charges) NormalizeCharges(IEnumerable<QuotationChargeRequest> charges)
        {
            if (charges == null)
            {
                return (null, null);
            }

            var writes = new List<PurchaseOrderChargeWrite>();
            var sort = 0;
            foreach (var charge in charges)
            {
                var name = charge?.Name?.Trim() ?? string.Empty;
                var hasAmount = charge?.Amount != null;
                if (name.Length == 0 && !hasAmount)
                {
                    continue;
                }

                if (name.Length == 0)
                {
                    return ("Each additional charge needs a name.", null);
                }

                if (name.Length > 200)
                {
                    return ("Additional charge names must be 200 characters or fewer.", null);
                }

                if (!hasAmount || charge.Amount < 0)
                {
                    return ("Each additional charge needs an amount of zero or more.", null);
                }

                writes.Add(new PurchaseOrderChargeWrite
                {
                    Name = name,
                    Amount = decimal.Round(charge.Amount.Value, 2, MidpointRounding.AwayFromZero),
                    SortOrder = sort++,
                });
            }

            return (null, writes);
        }

        private async Task<(IActionResult Error, MemoryStream Buffer, string FileName, string ContentType, string FileType)> ReadUploadAsync(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return (null, null, null, null, null);
            }

            if (file.Length > MaxFileBytes)
            {
                return (BadRequest(new { message = "Each document must be 50 MB or smaller." }), null, null, null, null);
            }

            if (!DriveDocumentTypes.TryDescribe(file.FileName, out var uploadName, out var contentType, out var fileType, out var fileError))
            {
                return (BadRequest(new { message = fileError }), null, null, null, null);
            }

            var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return (null, buffer, uploadName, contentType, fileType);
        }

        private async Task<IActionResult> MatchImportedActivities(int projectId, CreatePurchaseOrderArgs args)
        {
            var activities = await _uow.PurchaseOrders.GetProjectActivitiesAsync(projectId);
            var index = 0;
            foreach (var item in args.Items ?? Array.Empty<PurchaseOrderItemWrite>())
            {
                index++;
                var matches = MatchActivities(activities, item.DsnNo, item.PartNo);
                var label = string.IsNullOrWhiteSpace(item.DsnNo) && string.IsNullOrWhiteSpace(item.PartNo)
                    ? "Line " + index
                    : "Line " + index + " (" + item.DsnNo + " / " + item.PartNo + ")";
                if (matches.Count == 0)
                {
                    return BadRequest(new { message = label + " does not match an activity on this project." });
                }

                if (matches.Count > 1)
                {
                    return BadRequest(new { message = label + " matches more than one activity on this project." });
                }

                item.ActivityId = matches[0].Id;
            }

            return null;
        }

        private static List<PurchaseOrderActivityMatch> MatchActivities(IReadOnlyList<PurchaseOrderActivityMatch> activities, string dsn, string part)
        {
            var matches = string.IsNullOrWhiteSpace(dsn)
                ? new List<PurchaseOrderActivityMatch>()
                : activities.Where(activity => Same(activity.DsnNo, dsn)).ToList();
            if (matches.Count > 1 && !string.IsNullOrWhiteSpace(part))
            {
                var narrowed = matches.Where(activity => PartMatches(activity.PartNo, part)).ToList();
                if (narrowed.Count > 0)
                {
                    matches = narrowed;
                }
            }

            if (matches.Count == 0 && !string.IsNullOrWhiteSpace(part))
            {
                matches = activities.Where(activity => PartMatches(activity.PartNo, part)).ToList();
            }

            return matches;
        }

        private static bool PartMatches(string activityPart, string importedPart)
        {
            if (Same(activityPart, importedPart))
            {
                return true;
            }

            var wanted = (importedPart ?? string.Empty).Trim();
            if (wanted.Length < 3)
            {
                return false;
            }

            return (activityPart ?? string.Empty)
                .Split(new[] { " Or ", " or ", " OR ", ",", ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Any(token => Same(token, wanted));
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimTo(string value, int length)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= length ? text : text.Substring(0, length);
        }

        private Task<bool> ProjectExists(int projectId)
        {
            return _context.Projects.AnyAsync(project => project.Id == projectId);
        }
    }
}
