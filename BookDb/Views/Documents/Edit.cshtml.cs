using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookDb.Models;

namespace BookDb.Views.Documents
{
    public class EditModel
    {
        public Document? Document { get; set; }
        public string Title => "Sửa tài liệu";
        
        public EditModel()
        {
        }

        public void Initialize(Document document)
        {
            Document = document;
        }

        public bool HasFile()
        {
            return !string.IsNullOrEmpty(Document?.FilePath);
        }

        public string GetFileUrl()
        {
            return Document?.FilePath ?? string.Empty;
        }

        public int GetDocumentId()
        {
            return Document?.Id ?? 0;
        }

        public string GetDocumentTitle()
        {
            return Document?.Title ?? string.Empty;
        }

        public string GetSafeTitle()
        {
            // Escape single quotes for JavaScript
            return GetDocumentTitle().Replace("'", "\\'");
        }

        public string GenerateUserName()
        {
            // TODO: Get from authentication
            return $"User_{new Random().Next(1000)}";
        }
    }
}
