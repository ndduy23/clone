using BookDb.Models;
using BookDb.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Repositories.Implementations
{
    public class BookmarkRepository : GenericRepository<Bookmark>, IBookmarkRepository
    {
        public BookmarkRepository(AppDbContext context) : base(context) { }

        public Task<List<Bookmark>> GetAllWithDetailsAsync()
        {
            return _context.Bookmarks
                .Include(b => b.DocumentPage)
                .ThenInclude(p => p.Document)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Bookmark>> GetFilteredBookmarksAsync(string? q)
        {
            var query = _context.Bookmarks
                .Include(b => b.DocumentPage)
                    .ThenInclude(dp => dp.Document)
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(b =>
                    EF.Functions.Like(b.Title, $"%{q}%") ||
                    (b.DocumentPage != null && b.DocumentPage.Document != null && EF.Functions.Like(b.DocumentPage.Document.Title, $"%{q}%")));
            }

            return await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int documentPageId)
        {
            return await _context.Bookmarks.AnyAsync(b => b.DocumentPageId == documentPageId);
        }
    }
}