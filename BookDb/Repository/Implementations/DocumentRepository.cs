using BookDb.Models;
using BookDb.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Repositories.Implementations
{
    public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
    {
        public DocumentRepository(AppDbContext context) : base(context) { }

        public async Task<Document?> GetByIdWithPagesAsync(int id)
        {
            return await _context.Documents
                .Include(d => d.Pages)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Document>> GetPagedAndSearchedAsync(string? q, int page, int pageSize)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(d => EF.Functions.Like(d.Title, $"%{q}%") ||
                                          EF.Functions.Like(d.Author, $"%{q}%") ||
                                          EF.Functions.Like(d.Category, $"%{q}%"));
            }

            return await query.OrderByDescending(d => d.CreatedAt)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();
        }
    }
}