using Mahkias.Api.Helpers;
using Mahkias.Api.Modules.Projects;
using Mahkias.Api.Security;
using Mahkias.Api.Services;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
    [RequireApiKey]
    [Route("api/projects/{projectId:int}/quotations/{quotationId:int}/documents")]
    public class QuotationDocumentController : Controller
    {
        private const long MaxFileBytes = 52_428_800;
        private const long MaxRequestBytes = 104_857_600;

        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;
        private readonly ProjectDriveFolders _folders;

        public QuotationDocumentController(IUow uow, IGoogleDriveService drive, ProjectDriveFolders folders)
        {
            _uow = uow;
            _drive = drive;
            _folders = folders;
        }

        [HttpGet]
        public async Task<IActionResult> List(int projectId, int quotationId)
        {
            var documents = await _uow.ProjectDocuments.ListByQuotationAsync(projectId, quotationId);
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            return Ok(documents);
        }

        [HttpPost]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Upload(int projectId, int quotationId, List<IFormFile> files, CancellationToken cancellationToken)
        {
            if (files == null || files.Count == 0 || files.All(file => file == null || file.Length == 0))
            {
                return BadRequest(new { message = "Choose at least one document to upload." });
            }

            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            try
            {
                var folderId = await _folders.EnsureQuotationsFolderAsync(projectId, cancellationToken);
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    return NotFound(new { message = "Project was not found." });
                }

                var saved = new List<object>();
                var uploadedNames = new List<string>();
                foreach (var file in files.Where(item => item != null && item.Length > 0))
                {
                    if (file.Length > MaxFileBytes)
                    {
                        return BadRequest(new { message = "Each document must be 50 MB or smaller." });
                    }

                    if (!DriveDocumentTypes.TryDescribe(file.FileName, out var fileName, out var contentType, out var fileType, out var error))
                    {
                        return BadRequest(new { message = error });
                    }

                    await using var stream = file.OpenReadStream();
                    var uploaded = await _drive.UploadAsync(folderId, fileName, contentType, stream, cancellationToken);
                    var document = await _uow.ProjectDocuments.AddQuotationDocumentAsync(new CreateQuotationDocumentArgs
                    {
                        ProjectId = projectId,
                        QuotationId = quotationId,
                        FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? fileName : uploaded.FileName,
                        FileType = fileType,
                        GoogleDriveFileId = uploaded.FileId,
                        WebViewLink = uploaded.WebViewLink,
                        FileSize = file.Length,
                        IsOriginal = IsOriginalRequested(),
                        SortOrder = RequestedSortOrder(),
                    });
                    if (document == null)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The file was stored, but its record could not be saved." });
                    }

                    saved.Add(document);
                    uploadedNames.Add(document.FileName);
                }

                await this.Write(_uow, projectId, "DocumentUploaded", ProjectHistoryText.DocumentsUploaded(uploadedNames, await QuotationCodeAsync(quotationId)));
                return Ok(saved);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{documentId:int}")]
        [RequestSizeLimit(MaxFileBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
        public async Task<IActionResult> Replace(int projectId, int quotationId, int documentId, IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Choose a document to upload." });
            }

            if (file.Length > MaxFileBytes)
            {
                return BadRequest(new { message = "Each document must be 50 MB or smaller." });
            }

            if (!DriveDocumentTypes.TryDescribe(file.FileName, out var fileName, out var contentType, out var fileType, out var error))
            {
                return BadRequest(new { message = error });
            }

            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            var existing = await _uow.ProjectDocuments.ListByQuotationAsync(projectId, quotationId);
            var previous = existing.FirstOrDefault(item => item.DocumentId == documentId);
            if (previous == null)
            {
                return NotFound(new { message = "Quotation file was not found on this project." });
            }

            try
            {
                var folderId = await _folders.EnsureQuotationsFolderAsync(projectId, cancellationToken);
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    return NotFound(new { message = "Project was not found." });
                }

                await using var stream = file.OpenReadStream();
                var uploaded = await _drive.UploadAsync(folderId, fileName, contentType, stream, cancellationToken);
                var document = await _uow.ProjectDocuments.UpdateQuotationDocumentAsync(new UpdateQuotationDocumentArgs
                {
                    ProjectId = projectId,
                    QuotationId = quotationId,
                    DocumentId = documentId,
                    FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? fileName : uploaded.FileName,
                    FileType = fileType,
                    GoogleDriveFileId = uploaded.FileId,
                    WebViewLink = uploaded.WebViewLink,
                    FileSize = file.Length,
                    IsOriginal = IsOriginalRequested(),
                    SortOrder = RequestedSortOrder(),
                });
                if (document == null)
                {
                    return NotFound(new { message = "Quotation file was not found on this project." });
                }

                await this.Write(_uow, projectId, "DocumentUpdated", ProjectHistoryText.DocumentReplaced(previous.FileName, document.FileName, await QuotationCodeAsync(quotationId)));
                return Ok(document);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("order")]
        public async Task<IActionResult> Order(int projectId, int quotationId, [FromBody] DocumentOrderRequest request)
        {
            if (request?.Documents == null)
            {
                return BadRequest(new { message = "Document order is required." });
            }

            var before = await _uow.ProjectDocuments.ListByQuotationAsync(projectId, quotationId);
            await _uow.ProjectDocuments.OrderQuotationDocumentsAsync(projectId, quotationId, request.Documents, request.OriginalDocumentId);
            var after = await _uow.ProjectDocuments.ListByQuotationAsync(projectId, quotationId);
            if (ProjectHistoryText.KeptOrderChanged(before, after, item => item.DocumentId, item => item.SortOrder, item => item.IsOriginal))
            {
                await this.Write(_uow, projectId, "DocumentReordered", ProjectHistoryText.DocumentsReordered(await QuotationCodeAsync(quotationId)));
            }
            return Ok(after);
        }

        [HttpDelete("{documentId:int}")]
        public async Task<IActionResult> Remove(int projectId, int quotationId, int documentId)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            var existing = await _uow.ProjectDocuments.ListByQuotationAsync(projectId, quotationId);
            var fileName = existing.FirstOrDefault(item => item.DocumentId == documentId)?.FileName;
            var removed = await _uow.ProjectDocuments.RemoveQuotationDocumentAsync(projectId, quotationId, documentId);
            if (!removed)
            {
                return NotFound(new { message = "Quotation file was not found on this project." });
            }

            await this.Write(_uow, projectId, "DocumentRemoved", ProjectHistoryText.DocumentRemoved(fileName, await QuotationCodeAsync(quotationId)));
            return NoContent();
        }

        private async Task<string> QuotationCodeAsync(int quotationId)
        {
            return (await _uow.Quotations.GetDetailAsync(quotationId))?.Code;
        }

        private bool IsOriginalRequested()
        {
            return string.Equals(Request.Form["isOriginal"], "true", StringComparison.OrdinalIgnoreCase);
        }

        private int? RequestedSortOrder()
        {
            return int.TryParse(Request.Form["sortOrder"], out var sort) ? sort : null;
        }
    }

    public class DocumentOrderRequest
    {
        public List<DocumentOrderItem> Documents { get; set; } = new();

        public int? OriginalDocumentId { get; set; }
    }
}
