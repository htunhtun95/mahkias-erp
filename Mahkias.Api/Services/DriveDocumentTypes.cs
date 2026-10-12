namespace Mahkias.Api.Services
{
    public static class DriveDocumentTypes
    {
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

        public static bool TryDescribe(string originalName, out string fileName, out string contentType, out string fileType, out string error)
        {
            fileName = Path.GetFileName(originalName ?? string.Empty).Trim();
            contentType = null;
            fileType = null;
            error = null;
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(fileName) || !ContentTypes.ContainsKey(extension))
            {
                error = "Upload an Excel, Word, PDF, or image file.";
                return false;
            }

            if (fileName.Length > 260)
            {
                fileName = fileName.Substring(0, 260);
            }

            contentType = ContentTypes[extension];
            fileType = FileTypes[extension];
            return true;
        }
    }
}
