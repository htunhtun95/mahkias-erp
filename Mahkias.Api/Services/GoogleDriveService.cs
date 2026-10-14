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

        public string[] FolderPath { get; set; }

        public string CredentialsFilePath { get; set; }

        public string CredentialsJson { get; set; }
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

        public async Task<string> EnsureProjectParentFolderAsync(CancellationToken cancellationToken = default)
        {
            var parentId = RootFolderId();
            foreach (var segment in _options.FolderPath ?? Array.Empty<string>())
            {
                var folderName = segment?.Trim();
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    continue;
                }

                parentId = await CreateFolderAsync(folderName, parentId, cancellationToken);
            }

            return parentId;
        }

        public async Task<string> CreateFolderAsync(string name, string parentFolderId, CancellationToken cancellationToken = default)
        {
            var folderName = string.IsNullOrWhiteSpace(name) ? "Project" : name.Trim();
            var parent = string.IsNullOrWhiteSpace(parentFolderId) ? RootFolderId() : parentFolderId.Trim();
            var existingId = await FindFolderIdAsync(folderName, parent, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }

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

        private async Task<string> FindFolderIdAsync(string folderName, string parentFolderId, CancellationToken cancellationToken)
        {
            var scoped = parentFolderId.StartsWith("0A", StringComparison.Ordinal);
            try
            {
                return await FindFolderPageAsync(folderName, parentFolderId, scoped, cancellationToken);
            }
            catch (Google.GoogleApiException ex) when (scoped && (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden || ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound))
            {
                try
                {
                    return await FindFolderPageAsync(folderName, parentFolderId, false, cancellationToken);
                }
                catch (Google.GoogleApiException fallback)
                {
                    throw new InvalidOperationException(fallback.Error?.Message ?? "Google Drive could not look up the folder.", fallback);
                }
            }
            catch (Google.GoogleApiException ex)
            {
                throw new InvalidOperationException(ex.Error?.Message ?? "Google Drive could not look up the folder.", ex);
            }
        }

        private async Task<string> FindFolderPageAsync(string folderName, string parentFolderId, bool sharedDrive, CancellationToken cancellationToken)
        {
            string pageToken = null;
            var name = EscapeQuery(folderName);
            var parent = EscapeQuery(parentFolderId);
            do
            {
                var list = Drive().Files.List();
                list.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{name}' and '{parent}' in parents and trashed = false";
                list.Fields = "nextPageToken, files(id, name, parents)";
                list.Spaces = "drive";
                list.PageSize = 10;
                list.SupportsAllDrives = true;
                list.IncludeItemsFromAllDrives = true;
                list.Corpora = sharedDrive ? "drive" : "allDrives";
                if (sharedDrive)
                {
                    list.DriveId = parentFolderId;
                }

                list.PageToken = pageToken;
                var page = await list.ExecuteAsync(cancellationToken);
                var match = page.Files?.FirstOrDefault(file =>
                    string.Equals(file.Name, folderName, StringComparison.Ordinal)
                    && (file.Parents == null || file.Parents.Count == 0 || file.Parents.Contains(parentFolderId)));
                if (!string.IsNullOrWhiteSpace(match?.Id))
                {
                    return match.Id;
                }

                pageToken = page.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return null;
        }

        private static string EscapeQuery(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
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

                var credential = LoadCredential();
                _drive = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Mahkias ERP",
                });
                return _drive;
            }
        }

        private GoogleCredential LoadCredential()
        {
            var json = FirstConfigured(
                _options.CredentialsJson,
                Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CREDENTIALS"),
                Environment.GetEnvironmentVariable("GoogleDrive__CredentialsJson"));
            if (!string.IsNullOrWhiteSpace(json))
            {
                return GoogleCredential.FromJson(json).CreateScoped(DriveService.Scope.Drive);
            }

            var account = CredentialFactory.FromFile<ServiceAccountCredential>(CredentialsPath());
            return account.ToGoogleCredential().CreateScoped(DriveService.Scope.Drive);
        }

        private string CredentialsPath()
        {
            var configured = FirstConfigured(
                _options.CredentialsFilePath,
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException("Google Drive credentials file is not configured.");
            }

            configured = Environment.ExpandEnvironmentVariables(configured);
            if (Path.IsPathRooted(configured) && File.Exists(configured))
            {
                return configured;
            }

            var roots = new[]
            {
                _environment.ContentRootPath,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var candidate = Path.GetFullPath(Path.Combine(root, configured));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Google Drive credentials file was not found.");
        }

        private static string FirstConfigured(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}
