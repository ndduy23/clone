using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Repositories.Implementations
{
    public class DocumentPageRepository : GenericRepository<DocumentPage>, IDocumentPageRepository
    {
        public DocumentPageRepository(AppDbContext context) : base(context) { }

        public async Task<DocumentPage?> GetByIdWithDocumentAsync(int id)
        {
            return await _context.DocumentPages
                .Include(p => p.Document)
                .Include(p => p.Bookmark)  // Include bookmark to check if exists
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<DocumentPage>> GetPagesByDocumentIdAsync(int documentId)
        {
            return await _context.DocumentPages
                .Where(p => p.DocumentId == documentId)
                .OrderBy(p => p.PageNumber)
                .ToListAsync();
        }

        // New method to get page with bookmark for checking existence
        public async Task<DocumentPage?> GetByIdWithBookmarkAsync(int id)
        {
            return await _context.DocumentPages
                .Include(p => p.Bookmark)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<DocumentPage>> GetPagesWithBookmarksAsync(int documentId)
        {
            return await _context.DocumentPages
                .Include(p => p.Bookmark)
                .Where(p => p.DocumentId == documentId)
                .OrderBy(p => p.PageNumber)
                .ToListAsync();
        }
    }
}