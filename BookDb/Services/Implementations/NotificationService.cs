using Microsoft.AspNetCore.SignalR;
using BookDb.Hubs;

namespace BookDb.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IHubContext<NotificationHub> hubContext, ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendGlobalNotificationAsync(string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
                _logger.LogInformation("Global notification sent: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending global notification: {Message}", message);
            }
        }

        public async Task SendDocumentNotificationAsync(int documentId, string message)
        {
            try
            {
                var groupName = $"doc-{documentId}";
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNotification", message);
                _logger.LogInformation("Document notification sent to group {Group}: {Message}", groupName, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document notification for document {DocumentId}", documentId);
            }
        }

        public async Task SendUserNotificationAsync(string userId, string message)
        {
            try
            {
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", message);
                _logger.LogInformation("User notification sent to {UserId}: {Message}", userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user notification to {UserId}", userId);
            }
        }

        public async Task NotifyDocumentUploadedAsync(string documentTitle)
        {
            try
            {
                var message = $"📄 Tài liệu mới đã được thêm: {documentTitle}";
                
                // Send global notification (will be received once by all users)
                await SendGlobalNotificationAsync(message);
                
                // Send structured DocumentAdded event for AJAX table reload
                await _hubContext.Clients.All.SendAsync("DocumentAdded", new { Title = documentTitle });
                
                _logger.LogInformation("Document uploaded notification sent: {DocumentTitle}", documentTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document uploaded notification");
            }
        }

        public async Task NotifyDocumentDeletedAsync(string documentTitle)
        {
            try
            {
                var message = $"🗑️ Tài liệu đã bị xóa: {documentTitle}";
                
                // Send both text notification and structured event
                await SendGlobalNotificationAsync(message);
                
                // Send structured DocumentDeleted event for AJAX table reload
                await _hubContext.Clients.All.SendAsync("DocumentDeleted", new { Title = documentTitle });
                
                _logger.LogInformation("Document deleted notification sent: {DocumentTitle}", documentTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document deleted notification");
            }
        }

        public async Task NotifyDocumentUpdatedAsync(string documentTitle)
        {
            try
            {
                var message = $"✏️ Tài liệu đã được cập nhật: {documentTitle}";
                await SendGlobalNotificationAsync(message);
                
                _logger.LogInformation("Document updated notification sent: {DocumentTitle}", documentTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document updated notification");
            }
        }

        public async Task NotifyBookmarkCreatedAsync(string documentTitle, int pageNumber)
        {
            try
            {
                var message = $"🔖 Bookmark mới: {documentTitle} - Trang {pageNumber}";
                
                // Send global notification
                await SendGlobalNotificationAsync(message);
                
                // Send structured BookmarkCreated event for AJAX table reload
                await _hubContext.Clients.All.SendAsync("BookmarkCreated", new 
                { 
                    DocumentTitle = documentTitle, 
                    PageNumber = pageNumber 
                });
                
                _logger.LogInformation("Bookmark created notification sent: {DocumentTitle} - Page {PageNumber}", documentTitle, pageNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bookmark created notification");
            }
        }

        public async Task NotifyBookmarkDeletedAsync(string bookmarkTitle)
        {
            try
            {
                var message = $"🗑️ Bookmark đã bị xóa: {bookmarkTitle}";
                
                // Send global notification
                await SendGlobalNotificationAsync(message);
                
                // Send structured BookmarkDeleted event for AJAX table reload
                await _hubContext.Clients.All.SendAsync("BookmarkDeleted", new 
                { 
                    Title = bookmarkTitle 
                });
                
                _logger.LogInformation("Bookmark deleted notification sent: {BookmarkTitle}", bookmarkTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bookmark deleted notification");
            }
        }

        public async Task NotifyPageEditedAsync(int documentId, int pageId)
        {
            try
            {
                var groupName = $"doc-{documentId}";
                await _hubContext.Clients.Group(groupName).SendAsync("PageChanged", new { PageId = pageId, DocumentId = documentId });
                _logger.LogInformation("Page edited notification sent for document {DocumentId}, page {PageId}", documentId, pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending page edited notification for document {DocumentId}", documentId);
            }
        }
    }
}