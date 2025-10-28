using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookDb.Models;

namespace BookDb.Views.Documents
{
    public class IndexModel
    {
        public IEnumerable<Document> Documents { get; set; } = Enumerable.Empty<Document>();
        public string? SearchQuery { get; set; }
        public string Title => "Danh sách tài liệu";

        public IndexModel()
        {
        }

        public void Initialize(IEnumerable<Document> documents, string? searchQuery)
        {
            Documents = documents ?? Enumerable.Empty<Document>();
            SearchQuery = searchQuery;
        }

        public string GetFormattedDate(DateTime date)
        {
            return date.ToString("dd/MM/yyyy");
        }

        public bool HasDocuments()
        {
            return Documents?.Any() ?? false;
        }

        public int GetDocumentCount()
        {
            return Documents?.Count() ?? 0;
        }
    }
}
