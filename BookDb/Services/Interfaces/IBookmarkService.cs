using BookDb.Models;

namespace BookDb.Services.Interfaces
{
    public interface IBookmarkService
    {
        Task<List<Bookmark>> GetBookmarksAsync(string? q);
        Task<Bookmark?> GetBookmarkByIdAsync(int id);
        Task<(bool Success, string? ErrorMessage, int? BookmarkId)> CreateBookmarkAsync(int documentPageId, string? title, string url);
        Task<bool> DeleteBookmarkAsync(int id);
        Task<DocumentPage?> GetDocumentPageForBookmarkCreation(int documentPageId); 
    }
}