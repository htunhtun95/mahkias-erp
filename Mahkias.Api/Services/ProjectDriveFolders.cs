using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace Mahkias.Api.Services
{
    public class ProjectDriveFolders
    {
        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;
        private readonly ILogger<ProjectDriveFolders> _logger;

        public ProjectDriveFolders(IUow uow, IGoogleDriveService drive, ILogger<ProjectDriveFolders> logger)
        {
            _uow = uow;
            _drive = drive;
            _logger = logger;
        }

        public async Task<string> EnsureProjectFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return null;
            }

            var folderName = ProjectFolderName(project.Reference, project.Name);
            if (!string.IsNullOrWhiteSpace(project.GoogleDriveFolderId))
            {
                try
                {
                    await _drive.EnsureFolderNameAsync(project.GoogleDriveFolderId, folderName, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Google Drive folder {FolderId} for project {ProjectId} could not be renamed to {FolderName}.", project.GoogleDriveFolderId, projectId, folderName);
                }

                return project.GoogleDriveFolderId;
            }

            var folderId = await _drive.CreateFolderAsync(folderName, null, cancellationToken);
            return await _uow.ProjectDocuments.SetDriveFolderAsync(projectId, folderId);
        }

        public async Task<string> EnsureProcurementFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var projectFolderId = await EnsureProjectFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(projectFolderId))
            {
                return null;
            }

            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!string.IsNullOrWhiteSpace(project.ProcurementFolderId))
            {
                return project.ProcurementFolderId;
            }

            var folderId = await _drive.CreateFolderAsync("Procurement", projectFolderId, cancellationToken);
            return await _uow.ProjectDocuments.SetProcurementFolderAsync(projectId, folderId);
        }

        public static string ProjectFolderName(string reference, string name)
        {
            var referenceText = (reference ?? string.Empty).Trim();
            var nameText = (name ?? string.Empty).Trim();
            var label = string.IsNullOrEmpty(referenceText)
                ? nameText
                : string.IsNullOrEmpty(nameText) ? referenceText : referenceText + " " + nameText;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = "Project";
            }

            foreach (var character in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            {
                label = label.Replace(character, ' ');
            }

            label = string.Join(" ", label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (label.Length > 180)
            {
                label = label.Substring(0, 180).Trim();
            }

            return label;
        }
    }
}
