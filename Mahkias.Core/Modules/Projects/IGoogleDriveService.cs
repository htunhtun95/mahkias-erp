namespace Mahkias.Core.Modules.Projects
{
    public class GoogleDriveUploadResult
    {
        public string FileId { get; set; }

        public string FileName { get; set; }

        public string WebViewLink { get; set; }
    }

    public interface IGoogleDriveService
    {
        Task<string> EnsureProjectParentFolderAsync(CancellationToken cancellationToken = default);

        Task<string> CreateFolderAsync(string name, string parentFolderId, CancellationToken cancellationToken = default);

        Task<string> FindFolderAsync(string name, string parentFolderId, CancellationToken cancellationToken = default);

        Task RenameFolderAsync(string folderId, string name, CancellationToken cancellationToken = default);

        Task<GoogleDriveUploadResult> UploadAsync(string folderId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);
    }
}
