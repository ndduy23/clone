using BookDb.Models;

namespace BookDb.Services.Interfaces
{
    public interface IDocumentPageService
    {
        Task<DocumentPage?> GetPageByIdAsync(int id);
        Task UpdatePageAsync(int id, string? newTextContent);
        Task<IEnumerable<DocumentPage>> GetPagesOfDocumentAsync(int documentId);
        Task CreatePageAsync(DocumentPage page);
        Task DeletePageAsync(int id);
        Task<IEnumerable<DocumentPage>> GetPagesWithBookmarksAsync(int documentId);
    }
}