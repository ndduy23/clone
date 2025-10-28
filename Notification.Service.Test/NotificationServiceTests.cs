using Xunit;
using Moq;
using System;
using System.Threading.Tasks;
using BookDb.Services.Implementations;
using BookDb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BookDb.Tests.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<IHubContext<NotificationHub>> _hubContextMock;
        private readonly Mock<ILogger<NotificationService>> _loggerMock;
        private readonly Mock<IHubClients> _clientsMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _hubContextMock = new Mock<IHubContext<NotificationHub>>();
            _loggerMock = new Mock<ILogger<NotificationService>>();
            _clientsMock = new Mock<IHubClients>();
            _clientProxyMock = new Mock<IClientProxy>();

            _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
            _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);
            _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _clientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);

            _service = new NotificationService(_hubContextMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task SendGlobalNotificationAsync_Should_Send_To_All_Clients()
        {
            // Arrange
            var message = "Test global notification";

            // Act
            await _service.SendGlobalNotificationAsync(message);

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == message),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SendGlobalNotificationAsync_Should_LogError_When_Fails()
        {
            // Arrange
            var message = "Test message";
            _clientProxyMock.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default
            )).ThrowsAsync(new Exception("SignalR error"));

            // Act
            await _service.SendGlobalNotificationAsync(message);

            // Assert - Should not throw, just log error
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SendDocumentNotificationAsync_Should_Send_To_Document_Group()
        {
            // Arrange
            var documentId = 5;
            var message = "Document notification";

            // Act
            await _service.SendDocumentNotificationAsync(documentId, message);

            // Assert
            _clientsMock.Verify(c => c.Group("doc-5"), Times.Once);
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == message),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task SendUserNotificationAsync_Should_Send_To_Specific_User()
        {
            // Arrange
            var userId = "user123";
            var message = "User notification";

            // Act
            await _service.SendUserNotificationAsync(userId, message);

            // Assert
            _clientsMock.Verify(c => c.User(userId), Times.Once);
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString() == message),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyDocumentUploadedAsync_Should_Send_TextAndStructuredEvent()
        {
            // Arrange
            var documentTitle = "New Document";

            // Act
            await _service.NotifyDocumentUploadedAsync(documentTitle);

            // Assert
            // Should send both ReceiveNotification and DocumentAdded
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString().Contains(documentTitle)),
                    default
                ),
                Times.Once
            );

            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "DocumentAdded",
                    It.Is<object[]>(o => o.Length == 1),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyDocumentDeletedAsync_Should_Send_TextAndStructuredEvent()
        {
            // Arrange
            var documentTitle = "Deleted Document";

            // Act
            await _service.NotifyDocumentDeletedAsync(documentTitle);

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString().Contains(documentTitle)),
                    default
                ),
                Times.Once
            );

            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "DocumentDeleted",
                    It.Is<object[]>(o => o.Length == 1),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyDocumentUpdatedAsync_Should_Send_Notification()
        {
            // Arrange
            var documentTitle = "Updated Document";

            // Act
            await _service.NotifyDocumentUpdatedAsync(documentTitle);

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString().Contains(documentTitle)),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyBookmarkCreatedAsync_Should_Send_TextAndStructuredEvent()
        {
            // Arrange
            var documentTitle = "Test Document";
            var pageNumber = 5;

            // Act
            await _service.NotifyBookmarkCreatedAsync(documentTitle, pageNumber);

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString().Contains("🔖")),
                    default
                ),
                Times.Once
            );

            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "BookmarkCreated",
                    It.Is<object[]>(o => o.Length == 1),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyBookmarkDeletedAsync_Should_Send_TextAndStructuredEvent()
        {
            // Arrange
            var bookmarkTitle = "Test Bookmark";

            // Act
            await _service.NotifyBookmarkDeletedAsync(bookmarkTitle);

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(o => o.Length == 1 && o[0].ToString().Contains(bookmarkTitle)),
                    default
                ),
                Times.Once
            );

            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "BookmarkDeleted",
                    It.Is<object[]>(o => o.Length == 1),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyPageEditedAsync_Should_Send_To_Document_Group()
        {
            // Arrange
            var documentId = 10;
            var pageId = 25;

            // Act
            await _service.NotifyPageEditedAsync(documentId, pageId);

            // Assert
            _clientsMock.Verify(c => c.Group("doc-10"), Times.Once);
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    "PageChanged",
                    It.Is<object[]>(o => o.Length == 1),
                    default
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task NotifyPageEditedAsync_Should_LogError_When_Fails()
        {
            // Arrange
            _clientProxyMock.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default
            )).ThrowsAsync(new Exception("Hub error"));

            // Act
            await _service.NotifyPageEditedAsync(1, 1);

            // Assert - Should not throw
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once
            );
        }

        [Theory]
        [InlineData("Document Title 1")]
        [InlineData("")]
        [InlineData(null)]
        public async Task NotifyDocumentUploadedAsync_Should_HandleVariousTitles(string? title)
        {
            // Act
            await _service.NotifyDocumentUploadedAsync(title ?? "");

            // Assert
            _clientProxyMock.Verify(
                c => c.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    default
                ),
                Times.AtLeastOnce
            );
        }
    }
}
