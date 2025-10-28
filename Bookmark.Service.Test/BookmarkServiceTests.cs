using Xunit;
using Moq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using BookDb.Models;
using BookDb.Services.Implementations;
using BookDb.Services.Interfaces;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookDb.Tests.Services
{
    public class BookmarkServiceTests
    {
        private readonly Mock<IBookmarkRepository> _bookmarkRepoMock;
        private readonly Mock<IDocumentPageRepository> _pageRepoMock;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<ILogger<BookmarkService>> _loggerMock;
        private readonly BookmarkService _service;

        public BookmarkServiceTests()
        {
            _bookmarkRepoMock = new Mock<IBookmarkRepository>();
            _pageRepoMock = new Mock<IDocumentPageRepository>();
            _notificationServiceMock = new Mock<INotificationService>();
            _loggerMock = new Mock<ILogger<BookmarkService>>();

            // Use InMemory database for testing
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _service = new BookmarkService(
                _bookmarkRepoMock.Object,
                _pageRepoMock.Object,
                _context,
                _notificationServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetBookmarksAsync_Should_Call_Repository()
        {
            // Arrange
            var expectedBookmarks = new List<Bookmark>
            {
                new Bookmark { Id = 1, Title = "Bookmark 1" },
                new Bookmark { Id = 2, Title = "Bookmark 2" }
            };
            _bookmarkRepoMock.Setup(r => r.GetFilteredBookmarksAsync("test"))
                             .ReturnsAsync(expectedBookmarks);

            // Act
            var result = await _service.GetBookmarksAsync("test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            _bookmarkRepoMock.Verify(r => r.GetFilteredBookmarksAsync("test"), Times.Once);
        }

        [Fact]
        public async Task GetBookmarksAsync_WithNullQuery_Should_Return_All()
        {
            // Arrange
            _bookmarkRepoMock.Setup(r => r.GetFilteredBookmarksAsync(null))
                             .ReturnsAsync(new List<Bookmark>());

            // Act
            var result = await _service.GetBookmarksAsync(null);

            // Assert
            Assert.NotNull(result);
            _bookmarkRepoMock.Verify(r => r.GetFilteredBookmarksAsync(null), Times.Once);
        }

        [Fact]
        public async Task CreateBookmarkAsync_Should_Succeed_When_Valid()
        {
            // Arrange
            var page = new DocumentPage
            {
                Id = 1,
                PageNumber = 2,
                DocumentId = 5,
                Document = new Document { Id = 5, Title = "Book Title" }
            };

            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(page);
            _bookmarkRepoMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(false);
            _bookmarkRepoMock.Setup(r => r.AddAsync(It.IsAny<Bookmark>())).Returns(Task.CompletedTask);
            _notificationServiceMock.Setup(n => n.NotifyBookmarkCreatedAsync(It.IsAny<string>(), It.IsAny<int>()))
                                   .Returns(Task.CompletedTask);

            // Act
            var (success, error, bookmarkId) = await _service.CreateBookmarkAsync(1, "Custom Title", "/url");

            // Assert
            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(bookmarkId);
            _bookmarkRepoMock.Verify(r => r.AddAsync(It.Is<Bookmark>(b => 
                b.DocumentPageId == 1 && 
                b.Title == "Custom Title" && 
                b.Url == "/url"
            )), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyBookmarkCreatedAsync("Book Title", 2), Times.Once);
        }

        [Fact]
        public async Task CreateBookmarkAsync_Should_UseDefaultTitle_When_TitleIsNull()
        {
            // Arrange
            var page = new DocumentPage
            {
                Id = 1,
                PageNumber = 3,
                DocumentId = 5,
                Document = new Document { Id = 5, Title = "Test Document" }
            };

            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(page);
            _bookmarkRepoMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(false);
            _bookmarkRepoMock.Setup(r => r.AddAsync(It.IsAny<Bookmark>())).Returns(Task.CompletedTask);

            // Act
            var (success, error, bookmarkId) = await _service.CreateBookmarkAsync(1, null, "/url");

            // Assert
            Assert.True(success);
            _bookmarkRepoMock.Verify(r => r.AddAsync(It.Is<Bookmark>(b => 
                b.Title == "Test Document - Trang 3"
            )), Times.Once);
        }

        [Fact]
        public async Task CreateBookmarkAsync_Should_Fail_When_Page_NotFound()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(999))
                         .ReturnsAsync((DocumentPage?)null);

            // Act
            var (success, error, bookmarkId) = await _service.CreateBookmarkAsync(999, "title", "/url");

            // Assert
            Assert.False(success);
            Assert.Equal("Trang tài liệu không tồn tại.", error);
            Assert.Null(bookmarkId);
            _bookmarkRepoMock.Verify(r => r.AddAsync(It.IsAny<Bookmark>()), Times.Never);
        }

        [Fact]
        public async Task CreateBookmarkAsync_Should_Fail_When_AlreadyExists()
        {
            // Arrange
            var page = new DocumentPage 
            { 
                Id = 1,
                Document = new Document { Title = "Test" }
            };
            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(page);
            _bookmarkRepoMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(true);

            // Act
            var (success, error, bookmarkId) = await _service.CreateBookmarkAsync(1, "title", "/url");

            // Assert
            Assert.False(success);
            Assert.Equal("Không lưu được vì đã có bookmark trên trang này.", error);
            Assert.Null(bookmarkId);
            _bookmarkRepoMock.Verify(r => r.AddAsync(It.IsAny<Bookmark>()), Times.Never);
        }

        [Fact]
        public async Task CreateBookmarkAsync_Should_Continue_When_NotificationFails()
        {
            // Arrange
            var page = new DocumentPage
            {
                Id = 1,
                PageNumber = 2,
                Document = new Document { Title = "Book" }
            };

            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(page);
            _bookmarkRepoMock.Setup(r => r.ExistsAsync(1)).ReturnsAsync(false);
            _bookmarkRepoMock.Setup(r => r.AddAsync(It.IsAny<Bookmark>())).Returns(Task.CompletedTask);
            _notificationServiceMock.Setup(n => n.NotifyBookmarkCreatedAsync(It.IsAny<string>(), It.IsAny<int>()))
                                   .ThrowsAsync(new Exception("SignalR error"));

            // Act
            var (success, error, bookmarkId) = await _service.CreateBookmarkAsync(1, "title", "/url");

            // Assert - Should still succeed even if notification fails
            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(bookmarkId);
        }

        [Fact]
        public async Task DeleteBookmarkAsync_Should_Succeed_When_Found()
        {
            // Arrange
            var bookmark = new Bookmark { Id = 1, Title = "Test Bookmark" };
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(bookmark);
            _notificationServiceMock.Setup(n => n.NotifyBookmarkDeletedAsync(It.IsAny<string>()))
                                   .Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteBookmarkAsync(1);

            // Assert
            Assert.True(result);
            _bookmarkRepoMock.Verify(r => r.Delete(bookmark), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyBookmarkDeletedAsync("Test Bookmark"), Times.Once);
        }

        [Fact]
        public async Task DeleteBookmarkAsync_Should_UseDefaultTitle_When_TitleIsNull()
        {
            // Arrange
            var bookmark = new Bookmark { Id = 1, Title = null };
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(bookmark);

            // Act
            var result = await _service.DeleteBookmarkAsync(1);

            // Assert
            Assert.True(result);
            _notificationServiceMock.Verify(n => n.NotifyBookmarkDeletedAsync("Bookmark"), Times.Once);
        }

        [Fact]
        public async Task DeleteBookmarkAsync_Should_Fail_When_NotFound()
        {
            // Arrange
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Bookmark?)null);

            // Act
            var result = await _service.DeleteBookmarkAsync(999);

            // Assert
            Assert.False(result);
            _bookmarkRepoMock.Verify(r => r.Delete(It.IsAny<Bookmark>()), Times.Never);
            _notificationServiceMock.Verify(n => n.NotifyBookmarkDeletedAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteBookmarkAsync_Should_Continue_When_NotificationFails()
        {
            // Arrange
            var bookmark = new Bookmark { Id = 1, Title = "Test" };
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(bookmark);
            _notificationServiceMock.Setup(n => n.NotifyBookmarkDeletedAsync(It.IsAny<string>()))
                                   .ThrowsAsync(new Exception("SignalR error"));

            // Act
            var result = await _service.DeleteBookmarkAsync(1);

            // Assert - Should still succeed
            Assert.True(result);
        }

        [Fact]
        public async Task GetBookmarkByIdAsync_Should_Return_Bookmark()
        {
            // Arrange
            var expectedBookmark = new Bookmark { Id = 1, Title = "Test" };
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(expectedBookmark);

            // Act
            var result = await _service.GetBookmarkByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test", result.Title);
            _bookmarkRepoMock.Verify(r => r.GetByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetBookmarkByIdAsync_Should_Return_Null_When_NotFound()
        {
            // Arrange
            _bookmarkRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Bookmark?)null);

            // Act
            var result = await _service.GetBookmarkByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDocumentPageForBookmarkCreation_Should_Return_Page()
        {
            // Arrange
            var expectedPage = new DocumentPage { Id = 1, PageNumber = 5 };
            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(expectedPage);

            // Act
            var result = await _service.GetDocumentPageForBookmarkCreation(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(5, result.PageNumber);
            _pageRepoMock.Verify(r => r.GetByIdWithDocumentAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetDocumentPageForBookmarkCreation_Should_Return_Null_When_NotFound()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(999)).ReturnsAsync((DocumentPage?)null);

            // Act
            var result = await _service.GetDocumentPageForBookmarkCreation(999);

            // Assert
            Assert.Null(result);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
