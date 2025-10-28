using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BookDb.Models;

namespace BookDb.Views.Bookmarks
{
    public class IndexModel
    {
        public IEnumerable<Bookmark> Bookmarks { get; set; } = Enumerable.Empty<Bookmark>();
        public string? SearchQuery { get; set; }
        public string Title => "Danh sách Bookmark";

        public IndexModel()
        {
        }

        public void Initialize(IEnumerable<Bookmark> bookmarks, string? searchQuery)
        {
            Bookmarks = bookmarks ?? Enumerable.Empty<Bookmark>();
            SearchQuery = searchQuery;
        }

        public bool HasBookmarks()
        {
            return Bookmarks?.Any() ?? false;
        }

        public int GetBookmarkCount()
        {
            return Bookmarks?.Count() ?? 0;
        }

        public string GetFormattedDateTime(DateTime dateTime)
        {
            return dateTime.ToString("dd/MM/yyyy");
        }

        public string GetDocumentTitle(Bookmark bookmark)
        {
            return bookmark?.DocumentPage?.Document?.Title ?? "N/A";
        }

        public int? GetPageNumber(Bookmark bookmark)
        {
            return bookmark?.DocumentPage?.PageNumber;
        }

        public int? GetDocumentId(Bookmark bookmark)
        {
            return bookmark?.DocumentPage?.DocumentId;
        }

        public string GetEmptyMessage()
        {
            return "Chưa có bookmark nào. Hãy thêm bookmark khi xem tài liệu!";
        }
    }
}
