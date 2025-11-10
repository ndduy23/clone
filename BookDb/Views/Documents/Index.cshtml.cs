using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookDb.Models;

namespace BookDb.Views.Documents
{
    public class IndexModel
    {
        public IEnumerable<Document> Documents { get; set; } = Enumerable.Empty<Document>();
        public string? SearchQuery { get; set; }
        public bool OnlyMine { get; set; } = false;
        public string Title => "Danh sách tài liệu";

        public IndexModel()
        {
        }

        public void Initialize(IEnumerable<Document> documents, string? searchQuery, bool onlyMine = false)
        {
            Documents = documents ?? Enumerable.Empty<Document>();
            SearchQuery = searchQuery;
            OnlyMine = onlyMine;
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
