using BookDb.Models;

namespace BookDb.Repositories.Interfaces
{
    public interface IBookmarkRepository : IGenericRepository<Bookmark>
    {
        Task<List<Bookmark>> GetAllWithDetailsAsync();
        Task<List<Bookmark>> GetFilteredBookmarksAsync(string? q);
        Task<bool> ExistsAsync(int documentPageId);
    }
}