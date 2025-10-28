using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookDb.Views.Documents
{
    public class CreateModel
    {
        public string Title => "Thêm tài liệu mới";
        public long MaxFileSize => 50 * 1024 * 1024; // 50MB
        public string[] AcceptedFileTypes => new[] { ".pdf", ".docx", ".txt", ".xlsx" };
        
        public CreateModel()
        {
        }

        public string GetAcceptedFileTypesString()
        {
            return string.Join(", ", AcceptedFileTypes);
        }

        public string GetMaxFileSizeDisplay()
        {
            return "50MB";
        }

        public string GetFileInputAccept()
        {
            return string.Join(",", AcceptedFileTypes);
        }

        public bool IsValidFileSize(long fileSize)
        {
            return fileSize <= MaxFileSize;
        }

        public string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 Bytes";
            
            const int k = 1024;
            string[] sizes = { "Bytes", "KB", "MB", "GB" };
            int i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
            
            return Math.Round(bytes / Math.Pow(k, i), 2) + " " + sizes[i];
        }
    }
}
