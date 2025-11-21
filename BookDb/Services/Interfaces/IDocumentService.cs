using BookDb.Models;

namespace BookDb.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<List<Document>> GetDocumentsAsync(string? q, int page, int pageSize);
        Task<List<Document>> GetDocumentsAsync(string? q, string? userId, bool onlyMine, int page, int pageSize);
        Task<Document?> GetDocumentForViewingAsync(int id);
        Task CreateDocumentAsync(IFormFile file, string title, string category, string? authorName, string description, int? authorId = null, string? ownerId = null);
        Task<bool> DeleteDocumentAsync(int id);
        Task<Document?> GetDocumentByIdAsync(int id);
        Task<DocumentPage?> GetDocumentPageByIdAsync(int id);
        Task<bool> UpdateDocumentAsync(int id, IFormFile? file, string title, string category, int? authorId, string description);
    }
}