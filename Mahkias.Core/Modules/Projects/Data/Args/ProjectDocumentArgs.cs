namespace Mahkias.Core.Modules.Projects.Data.Args
{
    public class CreateProjectDocumentArgs
    {
        public int ProjectId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }
    }

    public class CreateQuotationDocumentArgs
    {
        public int ProjectId { get; set; }

        public int QuotationId { get; set; }

        public string FileName { get; set; }

        public string FileType { get; set; }

        public string GoogleDriveFileId { get; set; }

        public string WebViewLink { get; set; }
    }

    public class UpdateQuotationDocumentArgs : CreateQuotationDocumentArgs
    {
        public int DocumentId { get; set; }
    }
}
