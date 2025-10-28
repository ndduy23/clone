using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookDb.Models;

namespace BookDb.Views.Documents
{
    public class ViewDocumentModel
    {
        public Document? Document { get; set; }
        public string Mode { get; set; } = "original";
        public DocumentPage? CurrentPage { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 0;
        
        public string Title => "Xem tài liệu";

        public ViewDocumentModel()
        {
        }

        public void Initialize(Document document, string mode, DocumentPage? currentPage, int page, int totalPages)
        {
            Document = document;
            Mode = mode;
            CurrentPage = currentPage;
            Page = page;
            TotalPages = totalPages;
        }

        public bool IsPagedMode()
        {
            return Mode == "paged";
        }

        public bool IsOriginalMode()
        {
            return Mode == "original";
        }

        public bool HasPages()
        {
            return Document?.Pages != null && Document.Pages.Any();
        }

        public string GetFormattedDate()
        {
            return Document?.CreatedAt.ToString("dd/MM/yyyy") ?? string.Empty;
        }

        public string GetPageInfo()
        {
            if (TotalPages == 0) return string.Empty;
            return $"Trang {Page} / {TotalPages}";
        }

        public bool HasPreviousPage()
        {
            return Page > 1;
        }

        public bool HasNextPage()
        {
            return Page < TotalPages;
        }

        public int GetPreviousPageNumber()
        {
            return Math.Max(1, Page - 1);
        }

        public int GetNextPageNumber()
        {
            return Math.Min(TotalPages, Page + 1);
        }

        public DocumentPage? GetCurrentPageDocument()
        {
            return CurrentPage ?? Document?.Pages?
                .OrderBy(p => p.PageNumber)
                .FirstOrDefault(p => p.PageNumber == Page);
        }

        public bool CurrentPageHasBookmark()
        {
            var currentPageDoc = GetCurrentPageDocument();
            return currentPageDoc?.Bookmark != null;
        }

        public bool IsHtmlContent()
        {
            var currentPageDoc = GetCurrentPageDocument();
            return !string.IsNullOrEmpty(currentPageDoc?.ContentType) && 
                   currentPageDoc.ContentType.Contains("text/html");
        }

        public string GetIFrameSource()
        {
            var currentPageDoc = GetCurrentPageDocument();
            return currentPageDoc?.FilePath ?? string.Empty;
        }

        public int GetCurrentPageId()
        {
            return CurrentPage?.Id ?? 0;
        }

        public int GetDocumentId()
        {
            return Document?.Id ?? 0;
        }

        public string GetDocumentTitle()
        {
            return Document?.Title ?? string.Empty;
        }

        public string GetDocumentAuthor()
        {
            return Document?.Author ?? string.Empty;
        }

        public string GetDocumentCategory()
        {
            return Document?.Category ?? string.Empty;
        }

        public string GetDocumentDescription()
        {
            return Document?.Description ?? string.Empty;
        }

        public bool HasFile()
        {
            return !string.IsNullOrEmpty(Document?.FilePath);
        }

        public string GetFilePath()
        {
            return Document?.FilePath ?? string.Empty;
        }
    }
}
