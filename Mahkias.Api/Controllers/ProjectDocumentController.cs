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
    [Route("api/projects/{projectId:int}")]
    public class ProjectDocumentController : Controller
    {
        private const long MaxFileBytes = 52_428_800;

        private static readonly Dictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".xlsm"] = "application/vnd.ms-excel.sheet.macroEnabled.12",
            [".csv"] = "text/csv",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
            [".tif"] = "image/tiff",
            [".tiff"] = "image/tiff",
        };

        private static readonly Dictionary<string, string> FileTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "PDF",
            [".doc"] = "Word",
            [".docx"] = "Word",
            [".xls"] = "Excel",
            [".xlsx"] = "Excel",
            [".xlsm"] = "Excel",
            [".csv"] = "Excel",
            [".png"] = "Image",
            [".jpg"] = "Image",
            [".jpeg"] = "Image",
            [".gif"] = "Image",
            [".webp"] = "Image",
            [".bmp"] = "Image",
            [".tif"] = "Image",
            [".tiff"] = "Image",
        };

        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;
        private readonly ProjectDriveFolders _folders;

        public ProjectDocumentController(IUow uow, IGoogleDriveService drive, ProjectDriveFolders folders)
        {
            _uow = uow;
            _drive = drive;
            _folders = folders;
        }

        [HttpPost("drive-folder")]
        public async Task<IActionResult> CreateFolder(int projectId, CancellationToken cancellationToken)
        {
            try
            {
                var folderId = await _folders.EnsureManagedFoldersAsync(projectId, cancellationToken);
                if (string.IsNullOrWhiteSpace(folderId))
                {
                    return NotFound(new { message = "Project was not found." });
                }

                return Ok(new { googleDriveFolderId = folderId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("documents")]
        public async Task<IActionResult> List(int projectId)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            return Ok(await _uow.ProjectDocuments.ListByProjectAsync(projectId));
        }

        [HttpPost("documents")]
        [RequestSizeLimit(MaxFileBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
        public async Task<IActionResult> Upload(int projectId, IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Choose a document to upload." });
            }

            if (file.Length > MaxFileBytes)
            {
                return BadRequest(new { message = "Documents must be 50 MB or smaller." });
            }

            var fileName = Path.GetFileName(file.FileName ?? string.Empty).Trim();
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(fileName) || !ContentTypes.ContainsKey(extension))
            {
                return BadRequest(new { message = "Upload an Excel, Word, PDF, or image file." });
            }

            if (fileName.Length > 260)
            {
                fileName = fileName.Substring(0, 260);
            }

            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return NotFound(new { message = "Project was not found." });
            }

            try
            {
                var folderId = await _folders.EnsureProjectFolderAsync(projectId, cancellationToken);

                if (string.IsNullOrWhiteSpace(folderId))
                {
                    return NotFound(new { message = "Project was not found." });
                }

                await using var stream = file.OpenReadStream();
                var uploaded = await _drive.UploadAsync(folderId, fileName, ContentTypes[extension], stream, cancellationToken);
                var document = await _uow.ProjectDocuments.AddAsync(new CreateProjectDocumentArgs
                {
                    ProjectId = projectId,
                    FileName = string.IsNullOrWhiteSpace(uploaded.FileName) ? fileName : uploaded.FileName,
                    FileType = FileTypes[extension],
                    GoogleDriveFileId = uploaded.FileId,
                    WebViewLink = uploaded.WebViewLink,
                });

                if (document == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The file was stored, but its record could not be saved." });
                }

                return Ok(document);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
