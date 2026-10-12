using Mahkias.Api.Services;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Microsoft.AspNetCore.Mvc;

namespace Mahkias.Api.Controllers
{
    [ApiController]
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
                var folderId = await _folders.EnsureProcurementFolderAsync(projectId, cancellationToken);
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    return NotFound(new { message = "Project was not found." });
                }

                var saved = new List<object>();
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
                    });
                    if (document == null)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The file was stored, but its record could not be saved." });
                    }

                    saved.Add(document);
                }

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
            if (!existing.Any(item => item.DocumentId == documentId))
            {
                return NotFound(new { message = "Quotation file was not found on this project." });
            }

            try
            {
                var folderId = await _folders.EnsureProcurementFolderAsync(projectId, cancellationToken);
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
                });
                if (document == null)
                {
                    return NotFound(new { message = "Quotation file was not found on this project." });
                }

                return Ok(document);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{documentId:int}")]
        public async Task<IActionResult> Remove(int projectId, int quotationId, int documentId)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            var removed = await _uow.ProjectDocuments.RemoveQuotationDocumentAsync(projectId, quotationId, documentId);
            if (!removed)
            {
                return NotFound(new { message = "Quotation file was not found on this project." });
            }

            return NoContent();
        }
    }
}
