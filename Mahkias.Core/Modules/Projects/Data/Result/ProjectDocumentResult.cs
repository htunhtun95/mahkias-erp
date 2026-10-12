namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class ProjectDocumentResult
    {
        public int DocumentId { get; set; }

        public int ProjectId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }

        public DateTime UploadedAt { get; set; }
    }

    public class ProjectDriveFolderResult
    {
        public bool Exists { get; set; }

        public string Name { get; set; }

        public string Reference { get; set; }

        public string GoogleDriveFolderId { get; set; }

        public string ProcurementFolderId { get; set; }
    }

    public class QuotationDocumentResult
    {
        public int DocumentId { get; set; }

        public int QuotationId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }

        public DateTime UploadedAt { get; set; }
    }
}
