using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;

namespace Mahkias.Api.Services
{
    public class GoogleDriveOptions
    {
        public const string SectionName = "GoogleDrive";

        public string RootFolderId { get; set; }

        public string CredentialsFilePath { get; set; }
    }

    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly GoogleDriveOptions _options;
        private readonly object _gate = new object();
        private DriveService _drive;

        public GoogleDriveService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _options = configuration.GetSection(GoogleDriveOptions.SectionName).Get<GoogleDriveOptions>() ?? new GoogleDriveOptions();
        }

        public async Task<string> CreateFolderAsync(string name, string parentFolderId, CancellationToken cancellationToken = default)
        {
            var folderName = string.IsNullOrWhiteSpace(name) ? "Project" : name.Trim();
            var parent = string.IsNullOrWhiteSpace(parentFolderId) ? RootFolderId() : parentFolderId.Trim();
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { parent },
            };

            var request = Drive().Files.Create(metadata);
            request.Fields = "id";
            request.SupportsAllDrives = true;
            try
            {
                var created = await request.ExecuteAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(created?.Id))
                {
                    throw new InvalidOperationException("Google Drive did not return a folder id.");
                }

                return created.Id;
            }
            catch (Google.GoogleApiException ex)
            {
                throw new InvalidOperationException(ex.Error?.Message ?? "Google Drive could not create the project folder.", ex);
            }
        }

        public async Task EnsureFolderNameAsync(string folderId, string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                return;
            }

            var folderName = string.IsNullOrWhiteSpace(name) ? "Project" : name.Trim();
            try
            {
                var lookup = Drive().Files.Get(folderId);
                lookup.Fields = "name";
                lookup.SupportsAllDrives = true;
                var current = await lookup.ExecuteAsync(cancellationToken);
                if (string.Equals(current?.Name, folderName, StringComparison.Ordinal))
                {
                    return;
                }

                var update = Drive().Files.Update(new Google.Apis.Drive.v3.Data.File { Name = folderName }, folderId);
                update.SupportsAllDrives = true;
                await update.ExecuteAsync(cancellationToken);
            }
            catch (Google.GoogleApiException ex)
            {
                throw new InvalidOperationException(ex.Error?.Message ?? "Google Drive could not update the project folder name.", ex);
            }
        }

        public async Task<GoogleDriveUploadResult> UploadAsync(string folderId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(folderId))
            {
                throw new InvalidOperationException("A project Google Drive folder is required before uploading.");
            }

            if (content == null)
            {
                throw new InvalidOperationException("A file is required.");
            }

            var parentId = folderId.Trim();
            var storedName = UniqueFileName(fileName, await FolderFileNamesAsync(parentId, cancellationToken));
            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = storedName,
                Parents = new List<string> { parentId },
            };

            var request = Drive().Files.Create(metadata, content, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            request.Fields = "id, webViewLink";
            request.SupportsAllDrives = true;
            try
            {
                var progress = await request.UploadAsync(cancellationToken);
                if (progress.Status != UploadStatus.Completed || string.IsNullOrWhiteSpace(request.ResponseBody?.Id))
                {
                    var detail = progress.Exception?.Message;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                        ? "Google Drive could not store this file."
                        : detail);
                }
            }
            catch (Google.GoogleApiException ex)
            {
                throw new InvalidOperationException(ex.Error?.Message ?? "Google Drive could not store this file.", ex);
            }

            var fileId = request.ResponseBody.Id;
            var link = request.ResponseBody.WebViewLink;
            if (string.IsNullOrWhiteSpace(link))
            {
                link = "https://drive.google.com/file/d/" + fileId + "/view";
            }

            return new GoogleDriveUploadResult
            {
                FileId = fileId,
                FileName = storedName,
                WebViewLink = link,
            };
        }

        private async Task<HashSet<string>> FolderFileNamesAsync(string folderId, CancellationToken cancellationToken)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string pageToken = null;
            var parent = folderId.Replace("'", "\\'");
            do
            {
                var list = Drive().Files.List();
                list.Q = $"'{parent}' in parents and trashed = false";
                list.Fields = "nextPageToken, files(id, name)";
                list.Spaces = "drive";
                list.Corpora = "allDrives";
                list.PageSize = 1000;
                list.SupportsAllDrives = true;
                list.IncludeItemsFromAllDrives = true;
                list.PageToken = pageToken;
                var page = await list.ExecuteAsync(cancellationToken);
                if (page.Files != null)
                {
                    foreach (var file in page.Files)
                    {
                        if (!string.IsNullOrWhiteSpace(file.Name))
                        {
                            names.Add(file.Name);
                        }
                    }
                }

                pageToken = page.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return names;
        }

        private static string UniqueFileName(string fileName, ISet<string> existing)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "document" : fileName.Trim();
            var extension = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrEmpty(stem))
            {
                stem = "document";
            }

            var candidate = SizedName(stem, string.Empty, extension);
            if (!existing.Contains(candidate))
            {
                return candidate;
            }

            for (var index = 1; index < 10000; index++)
            {
                candidate = SizedName(stem, $" ({index})", extension);
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("A unique file name could not be chosen.");
        }

        private static string SizedName(string stem, string suffix, string extension)
        {
            var room = 260 - suffix.Length - extension.Length;
            if (room < 1)
            {
                room = 1;
                extension = string.Empty;
            }

            if (stem.Length > room)
            {
                stem = stem.Substring(0, room).TrimEnd();
            }

            if (string.IsNullOrEmpty(stem))
            {
                stem = "file";
            }

            return (stem + extension).AddFilenameSuffix(suffix);
        }

        private string RootFolderId()
        {
            var folderId = _options.RootFolderId?.Trim();
            if (string.IsNullOrWhiteSpace(folderId))
            {
                throw new InvalidOperationException("Google Drive root folder is not configured.");
            }

            return folderId;
        }

        private DriveService Drive()
        {
            if (_drive != null)
            {
                return _drive;
            }

            lock (_gate)
            {
                if (_drive != null)
                {
                    return _drive;
                }

                var account = CredentialFactory.FromFile<ServiceAccountCredential>(CredentialsPath());
                var credential = account.ToGoogleCredential().CreateScoped(DriveService.Scope.Drive);
                _drive = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Mahkias ERP",
                });
                return _drive;
            }
        }

        private string CredentialsPath()
        {
            var configured = _options.CredentialsFilePath?.Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException("Google Drive credentials file is not configured.");
            }

            if (Path.IsPathRooted(configured) && File.Exists(configured))
            {
                return configured;
            }

            var candidates = new[]
            {
                Path.Combine(_environment.ContentRootPath, configured),
                Path.Combine(AppContext.BaseDirectory, configured),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Google Drive credentials file was not found.");
        }
    }
}
