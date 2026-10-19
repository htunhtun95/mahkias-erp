using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;

namespace Mahkias.Api.Services
{
    public class ProjectDriveFolders
    {
        private readonly IUow _uow;
        private readonly IGoogleDriveService _drive;

        public ProjectDriveFolders(IUow uow, IGoogleDriveService drive)
        {
            _uow = uow;
            _drive = drive;
        }

        public async Task<string> EnsureProjectFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists)
            {
                return null;
            }

            var folderName = ProjectFolderName(project.Reference, project.Name);
            var parentFolderId = await _drive.EnsureProjectParentFolderAsync(cancellationToken);
            var folderId = await _drive.CreateFolderAsync(folderName, parentFolderId, cancellationToken);
            if (!string.Equals(folderId, project.GoogleDriveFolderId, StringComparison.Ordinal))
            {
                folderId = await _uow.ProjectDocuments.SetDriveFolderAsync(projectId, folderId);
            }

            return folderId;
        }

        public async Task RenameProjectFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            if (!project.Exists || string.IsNullOrWhiteSpace(project.GoogleDriveFolderId))
            {
                return;
            }

            await _drive.RenameFolderAsync(project.GoogleDriveFolderId, ProjectFolderName(project.Reference, project.Name), cancellationToken);
        }

        public async Task<string> EnsureProcurementFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var projectFolderId = await EnsureProjectFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(projectFolderId))
            {
                return null;
            }

            var project = await _uow.ProjectDocuments.GetDriveFolderAsync(projectId);
            var folderId = await _drive.CreateFolderAsync("Procurement", projectFolderId, cancellationToken);
            if (!string.Equals(folderId, project.ProcurementFolderId, StringComparison.Ordinal))
            {
                folderId = await _uow.ProjectDocuments.SetProcurementFolderAsync(projectId, folderId);
            }

            await _drive.CreateFolderAsync("Quotations", folderId, cancellationToken);
            await _drive.CreateFolderAsync("Purchases", folderId, cancellationToken);
            return folderId;
        }

        public async Task<string> EnsureQuotationsFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var procurementFolderId = await EnsureProcurementFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(procurementFolderId))
            {
                return null;
            }

            return await _drive.CreateFolderAsync("Quotations", procurementFolderId, cancellationToken);
        }

        public async Task<string> EnsurePurchasesFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var procurementFolderId = await EnsureProcurementFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(procurementFolderId))
            {
                return null;
            }

            return await _drive.CreateFolderAsync("Purchases", procurementFolderId, cancellationToken);
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
