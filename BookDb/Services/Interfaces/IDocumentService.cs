using BookDb.Models;

namespace BookDb.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<List<Document>> GetDocumentsAsync(string? q, int page, int pageSize);
        Task<Document?> GetDocumentForViewingAsync(int id);
        Task CreateDocumentAsync(IFormFile file, string title, string category, string author, string description);
        Task<bool> DeleteDocumentAsync(int id);
        Task<Document?> GetDocumentByIdAsync(int id);
        Task<DocumentPage?> GetDocumentPageByIdAsync(int id);
        Task<bool> UpdateDocumentAsync(int id, IFormFile? file, string title, string category, string author, string description);
        Task<List<Bookmark>> GetBookmarksAsync();
    }
}