using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Services.Implementations
{
    public class BookmarkService : IBookmarkService
    {
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly IDocumentRepository _docRepo;
        private readonly IDocumentPageRepository _pageRepo;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<BookmarkService> _logger;

        public BookmarkService(
            IBookmarkRepository bookmarkRepo,
            IDocumentRepository docRepo,
            IDocumentPageRepository pageRepo,
            AppDbContext context,
            INotificationService notificationService,
            ILogger<BookmarkService> logger)
        {
            _bookmarkRepo = bookmarkRepo;
            _docRepo = docRepo;
            _pageRepo = pageRepo;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<List<Bookmark>> GetBookmarksAsync(string? q, string? userId = null, bool onlyMine = false)
        {
            var query = _context.Bookmarks
                .Include(b => b.Document)
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(b =>
                    EF.Functions.Like(b.Title ?? "", $"%{q}%") ||
                    (b.Document != null && EF.Functions.Like(b.Document.Title, $"%{q}%")));
            }

            if (onlyMine && !string.IsNullOrEmpty(userId))
            {
                query = query.Where(b => b.UserId == userId);
            }

            return await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        // Create by document page id (MVC controller path)
        public async Task<(bool Success, string? ErrorMessage, int? BookmarkId)> CreateBookmarkAsync(
            int documentPageId,
            string? title,
            string url,
            string? userId = null)
        {
            var page = await GetDocumentPageForBookmarkCreation(documentPageId);
            if (page == null)
            {
                return (false, "Trang không tồn tại.", null);
            }

            // Check existence for this user+page
            var exists = await _context.Bookmarks.AnyAsync(b => b.DocumentPageId == documentPageId && b.UserId == userId);
            if (exists)
            {
                return (false, "Bookmark đã tồn tại cho trang này.", null);
            }

            var bookmarkTitle = title ?? $"{page.Document?.Title ?? "Tài liệu"} - Trang {page.PageNumber}";

            var bookmark = new Bookmark
            {
                DocumentPageId = documentPageId,
                DocumentId = page.DocumentId,
                PageNumber = page.PageNumber,
                Url = url,
                Title = bookmarkTitle,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            await _bookmarkRepo.AddAsync(bookmark);
            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.NotifyBookmarkCreatedAsync(page.Document?.Title ?? "Tài liệu", page.PageNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send bookmark created notification");
            }

            return (true, null, bookmark.Id);
        }

        // Create by documentId + pageNumber (API path)
        public async Task<(bool Success, string? ErrorMessage, int? BookmarkId)> CreateBookmarkAsync(
            int documentId,
            int pageNumber,
            string? title,
            string url,
            string? userId = null)
        {
            // Check if document exists
            var doc = await _docRepo.GetByIdAsync(documentId);
            if (doc == null) return (false, "Tài liệu không tồn tại.", null);

            // Check existence for this user
            var exists = await _context.Bookmarks.AnyAsync(b => b.DocumentId == documentId && b.PageNumber == pageNumber && b.UserId == userId);
            if (exists) return (false, "Đã có bookmark cho trang này.", null);

            var bookmarkTitle = title ?? $"{doc.Title} - Trang {pageNumber}";

            var bookmark = new Bookmark
            {
                DocumentId = documentId,
                PageNumber = pageNumber,
                Url = url,
                Title = bookmarkTitle,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            await _bookmarkRepo.AddAsync(bookmark);
            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.NotifyBookmarkCreatedAsync(doc.Title, pageNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send bookmark created notification");
            }

            return (true, null, bookmark.Id);
        }

        public async Task<bool> DeleteBookmarkAsync(int id, string? userId = null, bool isAdmin = false)
        {
            var bookmark = await _bookmarkRepo.GetByIdAsync(id);
            if (bookmark == null) return false;

            // Only owner or admin may delete
            if (!isAdmin && !string.Equals(bookmark.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unauthorized delete attempt for bookmark {BookmarkId} by user {UserId}", id, userId);
                return false;
            }

            var bookmarkTitle = bookmark.Title ?? "Bookmark";

            _bookmarkRepo.Delete(bookmark);
            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.NotifyBookmarkDeletedAsync(bookmarkTitle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send bookmark deleted notification");
            }

            _logger.LogInformation("Bookmark deleted: {BookmarkTitle} (ID: {BookmarkId})", bookmarkTitle, id);

            return true;
        }

        public Task<Bookmark?> GetBookmarkByIdAsync(int id)
        {
            return _context.Bookmarks
                .Include(b => b.Document)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Bookmark?> GetBookmarkForPageAsync(int documentId, int pageNumber, string? userId = null)
        {
            return await _context.Bookmarks
                .FirstOrDefaultAsync(b => b.DocumentId == documentId && b.PageNumber == pageNumber && b.UserId == userId);
        }

        public async Task<DocumentPage?> GetDocumentPageForBookmarkCreation(int documentPageId)
        {
            return await _context.DocumentPages
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == documentPageId);
        }
    }
}