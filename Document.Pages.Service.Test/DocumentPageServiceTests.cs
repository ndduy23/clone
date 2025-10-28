using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using BookDb.Services.Implementations;
using BookDb.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace BookDb.Tests.Services
{
    public class DocumentPageServiceTests : IDisposable
    {
        private readonly Mock<IDocumentPageRepository> _pageRepoMock;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<ILogger<DocumentPageService>> _loggerMock;
        private readonly DocumentPageService _service;

        public DocumentPageServiceTests()
        {
            _pageRepoMock = new Mock<IDocumentPageRepository>();
            _notificationServiceMock = new Mock<INotificationService>();
            _loggerMock = new Mock<ILogger<DocumentPageService>>();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _service = new DocumentPageService(
                _pageRepoMock.Object,
                _context,
                _notificationServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetPageByIdAsync_Should_Return_Page()
        {
            // Arrange
            var expectedPage = new DocumentPage 
            { 
                Id = 1, 
                PageNumber = 5,
                TextContent = "Test content"
            };
            _pageRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(expectedPage);

            // Act
            var result = await _service.GetPageByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(5, result.PageNumber);
            Assert.Equal("Test content", result.TextContent);
            _pageRepoMock.Verify(r => r.GetByIdAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetPageByIdAsync_Should_Return_Null_When_NotFound()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((DocumentPage?)null);

            // Act
            var result = await _service.GetPageByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetPagesOfDocumentAsync_Should_Return_All_Pages()
        {
            // Arrange
            var expectedPages = new List<DocumentPage>
            {
                new DocumentPage { Id = 1, PageNumber = 1, DocumentId = 5 },
                new DocumentPage { Id = 2, PageNumber = 2, DocumentId = 5 },
                new DocumentPage { Id = 3, PageNumber = 3, DocumentId = 5 }
            };
            _pageRepoMock.Setup(r => r.GetPagesByDocumentIdAsync(5))
                         .ReturnsAsync(expectedPages);

            // Act
            var result = await _service.GetPagesOfDocumentAsync(5);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
            _pageRepoMock.Verify(r => r.GetPagesByDocumentIdAsync(5), Times.Once);
        }

        [Fact]
        public async Task GetPagesOfDocumentAsync_Should_Return_Empty_When_NoPages()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetPagesByDocumentIdAsync(999))
                         .ReturnsAsync(new List<DocumentPage>());

            // Act
            var result = await _service.GetPagesOfDocumentAsync(999);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task UpdatePageAsync_Should_Update_Content_Successfully()
        {
            // Arrange
            var page = new DocumentPage 
            { 
                Id = 1, 
                PageNumber = 2,
                DocumentId = 10,
                TextContent = "Old content" 
            };
            _pageRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(page);
            _pageRepoMock.Setup(r => r.Update(It.IsAny<DocumentPage>()));
            _notificationServiceMock.Setup(n => n.NotifyPageEditedAsync(It.IsAny<int>(), It.IsAny<int>()))
                                   .Returns(Task.CompletedTask);

            // Act
            await _service.UpdatePageAsync(1, "New content");

            // Assert
            Assert.Equal("New content", page.TextContent);
            _pageRepoMock.Verify(r => r.Update(page), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyPageEditedAsync(10, 1), Times.Once);
        }

        [Fact]
        public async Task UpdatePageAsync_Should_Throw_When_PageNotFound()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetByIdAsync(999))
                         .ReturnsAsync((DocumentPage?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.UpdatePageAsync(999, "New content")
            );

            Assert.Equal("Không tìm thấy trang tài liệu.", ex.Message);
            _pageRepoMock.Verify(r => r.Update(It.IsAny<DocumentPage>()), Times.Never);
        }

        [Fact]
        public async Task UpdatePageAsync_Should_Continue_When_NotificationFails()
        {
            // Arrange
            var page = new DocumentPage 
            { 
                Id = 1, 
                DocumentId = 5,
                TextContent = "Old" 
            };
            _pageRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(page);
            _notificationServiceMock.Setup(n => n.NotifyPageEditedAsync(It.IsAny<int>(), It.IsAny<int>()))
                                   .ThrowsAsync(new Exception("SignalR error"));

            // Act - Should not throw
            await _service.UpdatePageAsync(1, "New");

            // Assert
            Assert.Equal("New", page.TextContent);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Short")]
        [InlineData("Very long content that spans multiple lines and contains a lot of text to test the service")]
        public async Task UpdatePageAsync_Should_HandleVariousContentLengths(string content)
        {
            // Arrange
            var page = new DocumentPage 
            { 
                Id = 1, 
                DocumentId = 5,
                TextContent = "Old" 
            };
            _pageRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(page);

            // Act
            await _service.UpdatePageAsync(1, content);

            // Assert
            Assert.Equal(content, page.TextContent);
        }

        [Fact]
        public async Task CreatePageAsync_Should_Create_New_Page()
        {
            // Arrange
            var newPage = new DocumentPage
            {
                DocumentId = 5,
                PageNumber = 1,
                TextContent = "Page content",
                FilePath = "/uploads/page1.html"
            };
            _pageRepoMock.Setup(r => r.AddAsync(It.IsAny<DocumentPage>())).Returns(Task.CompletedTask);

            // Act
            await _service.CreatePageAsync(newPage);

            // Assert
            _pageRepoMock.Verify(r => r.AddAsync(It.Is<DocumentPage>(p =>
                p.DocumentId == 5 &&
                p.PageNumber == 1 &&
                p.TextContent == "Page content"
            )), Times.Once);
        }

        [Fact]
        public async Task DeletePageAsync_Should_Delete_Page()
        {
            // Arrange
            var page = new DocumentPage { Id = 1, DocumentId = 5 };
            _pageRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(page);

            // Act
            await _service.DeletePageAsync(1);

            // Assert
            _pageRepoMock.Verify(r => r.Delete(page), Times.Once);
        }

        [Fact]
        public async Task DeletePageAsync_Should_Throw_When_NotFound()
        {
            // Arrange
            _pageRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((DocumentPage?)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.DeletePageAsync(999)
            );
        }

        [Fact]
        public async Task GetPagesWithBookmarksAsync_Should_Return_Pages_With_Bookmarks()
        {
            // Arrange
            var expectedPages = new List<DocumentPage>
            {
                new DocumentPage 
                { 
                    Id = 1, 
                    PageNumber = 1,
                    Bookmark = new Bookmark { Id = 1, Title = "Bookmark 1" }
                },
                new DocumentPage 
                { 
                    Id = 2, 
                    PageNumber = 2,
                    Bookmark = null
                }
            };
            _pageRepoMock.Setup(r => r.GetPagesWithBookmarksAsync(5))
                         .ReturnsAsync(expectedPages);

            // Act
            var result = await _service.GetPagesWithBookmarksAsync(5);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.NotNull(result.First().Bookmark);
            Assert.Null(result.Last().Bookmark);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
