using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using BookDb.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BookDb.Services.Implementations
{
    public class DocumentPageService : IDocumentPageService
    {
        private readonly IDocumentPageRepository _pageRepo;
        private readonly AppDbContext _context; 
        private readonly INotificationService? _notificationService;
        private readonly ILogger<DocumentPageService>? _logger;

        public DocumentPageService(
            IDocumentPageRepository pageRepo, 
            AppDbContext context, 
            INotificationService? notificationService = null,
            ILogger<DocumentPageService>? logger = null)
        {
            _pageRepo = pageRepo;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public Task<DocumentPage?> GetPageByIdAsync(int id)
        {
            return _pageRepo.GetByIdAsync(id);
        }

        public Task<IEnumerable<DocumentPage>> GetPagesOfDocumentAsync(int documentId)
        {
            return _pageRepo.GetPagesByDocumentIdAsync(documentId);
        }

        public async Task UpdatePageAsync(int id, string? newTextContent)
        {
            var pageToUpdate = await _pageRepo.GetByIdAsync(id);

            if (pageToUpdate == null)
            {
                throw new KeyNotFoundException("Không tìm thấy trang tài liệu.");
            }

            pageToUpdate.TextContent = newTextContent;

            _pageRepo.Update(pageToUpdate);
            await _context.SaveChangesAsync();

            // Notify viewers of the document that a page was updated
            if (_notificationService != null && pageToUpdate.DocumentId > 0)
            {
                try
                {
                    await _notificationService.NotifyPageEditedAsync(pageToUpdate.DocumentId, pageToUpdate.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to send page edited notification");
                }
            }
        }

        public async Task CreatePageAsync(DocumentPage page)
        {
            await _pageRepo.AddAsync(page);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePageAsync(int id)
        {
            var page = await _pageRepo.GetByIdAsync(id);
            if (page == null)
            {
                throw new KeyNotFoundException("Không tìm thấy trang tài liệu.");
            }

            _pageRepo.Delete(page);
            await _context.SaveChangesAsync();
        }

        public Task<IEnumerable<DocumentPage>> GetPagesWithBookmarksAsync(int documentId)
        {
            return _pageRepo.GetPagesWithBookmarksAsync(documentId);
        }
    }
}