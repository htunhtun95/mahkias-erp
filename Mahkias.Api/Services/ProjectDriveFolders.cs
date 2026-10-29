using System.Text.RegularExpressions;
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

        public async Task<string> EnsureManagedFoldersAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var projectFolderId = await EnsureProjectFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(projectFolderId))
            {
                return null;
            }

            await EnsureProcurementFolderAsync(projectId, cancellationToken);
            return projectFolderId;
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
            var folderId = await EnsureChildFolderAsync(projectFolderId, cancellationToken, "Procurement");
            if (!string.Equals(folderId, project.ProcurementFolderId, StringComparison.Ordinal))
            {
                folderId = await _uow.ProjectDocuments.SetProcurementFolderAsync(projectId, folderId);
            }

            await EnsureChildFolderAsync(folderId, cancellationToken, "Quotations", "Quotation");
            await EnsureChildFolderAsync(folderId, cancellationToken, "Purchase", "Purchases");
            return folderId;
        }

        public async Task<string> EnsureQuotationsFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var procurementFolderId = await EnsureProcurementFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(procurementFolderId))
            {
                return null;
            }

            return await EnsureChildFolderAsync(procurementFolderId, cancellationToken, "Quotations", "Quotation");
        }

        public async Task<string> EnsurePurchasesFolderAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var procurementFolderId = await EnsureProcurementFolderAsync(projectId, cancellationToken);
            if (string.IsNullOrWhiteSpace(procurementFolderId))
            {
                return null;
            }

            return await EnsureChildFolderAsync(procurementFolderId, cancellationToken, "Purchase", "Purchases");
        }

        private async Task<string> EnsureChildFolderAsync(string parentFolderId, CancellationToken cancellationToken, string canonicalName, params string[] alternateNames)
        {
            foreach (var name in new[] { canonicalName }.Concat(alternateNames ?? Array.Empty<string>()))
            {
                var existingId = await _drive.FindFolderAsync(name, parentFolderId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(existingId))
                {
                    return existingId;
                }
            }

            return await _drive.CreateFolderAsync(canonicalName, parentFolderId, cancellationToken);
        }

        public static string ProjectFolderName(string reference, string name)
        {
            var referenceText = NormalizeFolderName(reference);
            var nameText = NormalizeFolderName(name);
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

            label = NormalizeFolderName(label);
            if (label.Length > 180)
            {
                label = label.Substring(0, 180).Trim();
            }

            return label;
        }

        public static string NormalizeFolderName(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return string.Empty;
            }

            return Regex.Replace(text, @"\s+", " ");
        }
    }
}
