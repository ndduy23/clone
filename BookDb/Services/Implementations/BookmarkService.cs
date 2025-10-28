using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using BookDb.Services.Interfaces;

namespace BookDb.Services.Implementations
{
    public class BookmarkService : IBookmarkService
    {
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly IDocumentPageRepository _pageRepo;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<BookmarkService> _logger;

        public BookmarkService(
            IBookmarkRepository bookmarkRepo, 
            IDocumentPageRepository pageRepo, 
            AppDbContext context,
            INotificationService notificationService,
            ILogger<BookmarkService> logger)
        {
            _bookmarkRepo = bookmarkRepo;
            _pageRepo = pageRepo;
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        public Task<List<Bookmark>> GetBookmarksAsync(string? q)
        {
            return _bookmarkRepo.GetFilteredBookmarksAsync(q);
        }

        public async Task<(bool Success, string? ErrorMessage, int? BookmarkId)> CreateBookmarkAsync(int documentPageId, string? title, string url)
        {
            var page = await _pageRepo.GetByIdWithDocumentAsync(documentPageId);
            if (page == null)
            {
                return (false, "Trang tài liệu không tồn tại.", null);
            }

            bool exists = await _bookmarkRepo.ExistsAsync(documentPageId);
            if (exists)
            {
                return (false, "Không lưu được vì đã có bookmark trên trang này.", null);
            }

            var bookmarkTitle = title ?? $"{page.Document?.Title} - Trang {page.PageNumber}";
            var bookmark = new Bookmark
            {
                DocumentPageId = documentPageId,
                Url = url,
                Title = bookmarkTitle,
                CreatedAt = DateTime.UtcNow
            };

            await _bookmarkRepo.AddAsync(bookmark);
            await _context.SaveChangesAsync();

            // Send notification via SignalR
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyBookmarkCreatedAsync(
                        page.Document?.Title ?? "Tài liệu", 
                        page.PageNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send bookmark created notification");
                }
            }

            _logger.LogInformation("Bookmark created: {BookmarkTitle} (ID: {BookmarkId})", bookmarkTitle, bookmark.Id);

            return (true, null, bookmark.Id);
        }

        public async Task<bool> DeleteBookmarkAsync(int id)
        {
            var bookmark = await _bookmarkRepo.GetByIdAsync(id);
            if (bookmark == null) return false;

            var bookmarkTitle = bookmark.Title ?? "Bookmark";

            _bookmarkRepo.Delete(bookmark);
            await _context.SaveChangesAsync();

            // Send notification via SignalR
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyBookmarkDeletedAsync(bookmarkTitle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send bookmark deleted notification");
                }
            }

            _logger.LogInformation("Bookmark deleted: {BookmarkTitle} (ID: {BookmarkId})", bookmarkTitle, id);

            return true;
        }

        public Task<Bookmark?> GetBookmarkByIdAsync(int id)
        {
            return _bookmarkRepo.GetByIdAsync(id);
        }

        public Task<DocumentPage?> GetDocumentPageForBookmarkCreation(int documentPageId)
        {
            return _pageRepo.GetByIdWithDocumentAsync(documentPageId);
        }
    }
}