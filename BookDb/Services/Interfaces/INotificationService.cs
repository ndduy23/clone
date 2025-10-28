using Microsoft.AspNetCore.SignalR;
using BookDb.Hubs;

namespace BookDb.Services.Implementations
{
    public interface INotificationService
    {
        Task SendGlobalNotificationAsync(string message);
        Task SendDocumentNotificationAsync(int documentId, string message);
        Task SendUserNotificationAsync(string userId, string message);
        Task NotifyDocumentUploadedAsync(string documentTitle);
        Task NotifyDocumentDeletedAsync(string documentTitle);
        Task NotifyDocumentUpdatedAsync(string documentTitle);
        Task NotifyBookmarkCreatedAsync(string documentTitle, int pageNumber);
        Task NotifyBookmarkDeletedAsync(string bookmarkTitle);
        Task NotifyPageEditedAsync(int documentId, int pageId);
    }
}