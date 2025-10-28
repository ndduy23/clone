using BookDb.Models;

namespace BookDb.Repositories.Interfaces
{
    public interface IDocumentRepository : IGenericRepository<Document>
    {
        Task<List<Document>> GetPagedAndSearchedAsync(string? q, int page, int pageSize);
        Task<Document?> GetByIdWithPagesAsync(int id);
    }
}