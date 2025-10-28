using Microsoft.AspNetCore.SignalR;

namespace BookDb.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(string message)
        {
            await Clients.All.SendAsync("ReceiveNotification", message);
        }

        // Join a group representing a document so only clients viewing that document receive notifications
        public async Task JoinDocumentGroup(int documentId)
        {
            var groupName = GetGroupName(documentId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveDocumentGroup(int documentId)
        {
            var groupName = GetGroupName(documentId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        // Notify specific document group about page changes
        public async Task NotifyPageAdded(int documentId, int pageId, int pageNumber)
        {
            var groupName = GetGroupName(documentId);
            await Clients.Group(groupName).SendAsync("PageAdded", new
            {
                DocumentId = documentId,
                PageId = pageId,
                PageNumber = pageNumber
            });
        }

        public async Task NotifyPageUpdated(int documentId, int pageId, int pageNumber)
        {
            var groupName = GetGroupName(documentId);
            await Clients.Group(groupName).SendAsync("PageUpdated", new
            {
                DocumentId = documentId,
                PageId = pageId,
                PageNumber = pageNumber
            });
        }

        public async Task NotifyPageDeleted(int documentId, int pageId, int pageNumber)
        {
            var groupName = GetGroupName(documentId);
            await Clients.Group(groupName).SendAsync("PageDeleted", new
            {
                DocumentId = documentId,
                PageId = pageId,
                PageNumber = pageNumber
            });
        }

        // Document editing notifications - for real-time collaboration
        public async Task NotifyDocumentEditingStarted(int documentId, string documentTitle, string userName)
        {
            await Clients.Others.SendAsync("DocumentEditingStarted", new
            {
                DocumentId = documentId,
                DocumentTitle = documentTitle,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyDocumentEditingEnded(int documentId, string userName)
        {
            await Clients.Others.SendAsync("DocumentEditingEnded", new
            {
                DocumentId = documentId,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyDocumentFieldChanged(int documentId, string fieldName, string newValue, string userName)
        {
            await Clients.Others.SendAsync("DocumentFieldChanged", new
            {
                DocumentId = documentId,
                FieldName = fieldName,
                NewValue = newValue,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            });
        }

        private static string GetGroupName(int documentId) => $"doc-{documentId}";
    }
}