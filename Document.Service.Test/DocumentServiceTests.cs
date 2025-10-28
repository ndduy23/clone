using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using BookDb.Models;
using BookDb.Services.Implementations;
using BookDb.Services.Interfaces;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Tests.Services
{
    public class DocumentServiceTests : IDisposable
    {
        private readonly Mock<IDocumentRepository> _docRepoMock;
        private readonly Mock<IDocumentPageRepository> _pageRepoMock;
        private readonly Mock<IBookmarkRepository> _bookmarkRepoMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly AppDbContext _context;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly DocumentService _service;
        private readonly string _tempPath;

        public DocumentServiceTests()
        {
            _docRepoMock = new Mock<IDocumentRepository>();
            _pageRepoMock = new Mock<IDocumentPageRepository>();
            _bookmarkRepoMock = new Mock<IBookmarkRepository>();
            _envMock = new Mock<IWebHostEnvironment>();
            _notificationServiceMock = new Mock<INotificationService>();

            // Create temp directory for file operations
            _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempPath);
            Directory.CreateDirectory(Path.Combine(_tempPath, "Uploads"));

            _envMock.Setup(e => e.WebRootPath).Returns(_tempPath);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _service = new DocumentService(
                _docRepoMock.Object,
                _pageRepoMock.Object,
                _bookmarkRepoMock.Object,
                _envMock.Object,
                _context,
                _notificationServiceMock.Object
            );
        }

        [Fact]
        public async Task CreateDocumentAsync_Should_Create_PDF_Document()
        {
            // Arrange
            var mockFile = CreateMockPdfFile("test.pdf", "application/pdf");
            _docRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).Returns(Task.CompletedTask);
            _notificationServiceMock.Setup(n => n.NotifyDocumentUploadedAsync(It.IsAny<string>()))
                                   .Returns(Task.CompletedTask);

            // Act
            await _service.CreateDocumentAsync(mockFile.Object, "Test Title", "Category", "Author", "Description");

            // Assert
            _docRepoMock.Verify(r => r.AddAsync(It.Is<Document>(d =>
                d.Title == "Test Title" &&
                d.Category == "Category" &&
                d.Author == "Author" &&
                d.Description == "Description" &&
                d.ContentType == "application/pdf"
            )), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyDocumentUploadedAsync("Test Title"), Times.Once);
        }

        [Fact]
        public async Task CreateDocumentAsync_Should_Throw_When_FileIsNull()
        {
            // Arrange
            IFormFile? nullFile = null;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateDocumentAsync(nullFile, "Title", "Cat", "Auth", "Desc"));

            Assert.Equal("File not selected.", ex.Message);
        }

        [Fact]
        public async Task CreateDocumentAsync_Should_Throw_When_FileIsEmpty()
        {
            // Arrange
            var mockFile = CreateMockFile("test.pdf", "application/pdf", 0);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateDocumentAsync(mockFile.Object, "Title", "Cat", "Auth", "Desc"));

            Assert.Equal("File not selected.", ex.Message);
        }

        [Fact]
        public async Task CreateDocumentAsync_Should_Throw_When_InvalidExtension()
        {
            // Arrange
            var mockFile = CreateMockFile("malware.exe", "application/exe", 1024);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateDocumentAsync(mockFile.Object, "Title", "Cat", "Auth", "Desc"));

            Assert.Equal("Format not supported.", ex.Message);
        }

        [Theory]
        [InlineData("document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
        [InlineData("document.txt", "text/plain")]
        public async Task CreateDocumentAsync_Should_Accept_ValidFormats(string fileName, string contentType)
        {
            // Arrange
            var mockFile = CreateMockFile(fileName, contentType, 1024);
            _docRepoMock.Setup(r => r.AddAsync(It.IsAny<Document>())).Returns(Task.CompletedTask);

            // Act
            await _service.CreateDocumentAsync(mockFile.Object, "Title", "Cat", "Auth", "Desc");

            // Assert
            _docRepoMock.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
        }

        [Fact]
        public async Task GetDocumentsAsync_Should_Return_Filtered_Documents()
        {
            // Arrange
            var expectedDocs = new List<Document>
            {
                new Document { Id = 1, Title = "Doc 1" },
                new Document { Id = 2, Title = "Doc 2" }
            };
            _docRepoMock.Setup(r => r.GetPagedAndSearchedAsync("search", 1, 10))
                        .ReturnsAsync(expectedDocs);

            // Act
            var result = await _service.GetDocumentsAsync("search", 1, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            _docRepoMock.Verify(r => r.GetPagedAndSearchedAsync("search", 1, 10), Times.Once);
        }

        [Fact]
        public async Task GetDocumentByIdAsync_Should_Return_Document()
        {
            // Arrange
            var expectedDoc = new Document { Id = 1, Title = "Test Doc" };
            _docRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(expectedDoc);

            // Act
            var result = await _service.GetDocumentByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Doc", result.Title);
        }

        [Fact]
        public async Task GetDocumentForViewingAsync_Should_Include_Pages()
        {
            // Arrange
            var expectedDoc = new Document 
            { 
                Id = 1, 
                Title = "Test",
                Pages = new List<DocumentPage>()
            };
            _docRepoMock.Setup(r => r.GetByIdWithPagesAsync(1)).ReturnsAsync(expectedDoc);

            // Act
            var result = await _service.GetDocumentForViewingAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Pages);
            _docRepoMock.Verify(r => r.GetByIdWithPagesAsync(1), Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_Should_Delete_And_Notify()
        {
            // Arrange
            var doc = new Document 
            { 
                Id = 1, 
                Title = "Test Doc",
                FilePath = "/uploads/test.pdf" 
            };
            _docRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(doc);
            _notificationServiceMock.Setup(n => n.NotifyDocumentDeletedAsync(It.IsAny<string>()))
                                   .Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteDocumentAsync(1);

            // Assert
            Assert.True(result);
            _docRepoMock.Verify(r => r.Delete(doc), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyDocumentDeletedAsync("Test Doc"), Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_Should_ReturnFalse_When_NotFound()
        {
            // Arrange
            _docRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Document?)null);

            // Act
            var result = await _service.DeleteDocumentAsync(999);

            // Assert
            Assert.False(result);
            _docRepoMock.Verify(r => r.Delete(It.IsAny<Document>()), Times.Never);
        }

        [Fact]
        public async Task DeleteDocumentAsync_Should_Continue_When_NotificationFails()
        {
            // Arrange
            var doc = new Document { Id = 1, Title = "Test", FilePath = "/test.pdf" };
            _docRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(doc);
            _notificationServiceMock.Setup(n => n.NotifyDocumentDeletedAsync(It.IsAny<string>()))
                                   .ThrowsAsync(new Exception("SignalR error"));

            // Act
            var result = await _service.DeleteDocumentAsync(1);

            // Assert - Should still succeed
            Assert.True(result);
        }

        [Fact]
        public async Task UpdateDocumentAsync_Should_Update_Metadata_Only()
        {
            // Arrange
            var doc = new Document 
            { 
                Id = 1, 
                Title = "Old Title",
                Category = "Old Category",
                Author = "Old Author",
                Description = "Old Description"
            };
            _docRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(doc);
            _notificationServiceMock.Setup(n => n.NotifyDocumentUpdatedAsync(It.IsAny<string>()))
                                   .Returns(Task.CompletedTask);

            // Act
            var result = await _service.UpdateDocumentAsync(
                1, null, "New Title", "New Category", "New Author", "New Description");

            // Assert
            Assert.True(result);
            Assert.Equal("New Title", doc.Title);
            Assert.Equal("New Category", doc.Category);
            Assert.Equal("New Author", doc.Author);
            Assert.Equal("New Description", doc.Description);
            _docRepoMock.Verify(r => r.Update(doc), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyDocumentUpdatedAsync("New Title"), Times.Once);
        }

        [Fact]
        public async Task UpdateDocumentAsync_Should_ReturnFalse_When_NotFound()
        {
            // Arrange
            _docRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Document?)null);

            // Act
            var result = await _service.UpdateDocumentAsync(999, null, "Title", "Cat", "Auth", "Desc");

            // Assert
            Assert.False(result);
            _docRepoMock.Verify(r => r.Update(It.IsAny<Document>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_Should_Update_File_When_Provided()
        {
            // Arrange
            var doc = new Document 
            { 
                Id = 1, 
                Title = "Test",
                FilePath = "/Uploads/old.pdf"
            };
            // Create the old file so deletion works
            var oldFilePath = Path.Combine(_tempPath, "Uploads", "old.pdf");
            File.WriteAllText(oldFilePath, "old content");
            
            var newFile = CreateMockFile("new.pdf", "application/pdf", 2048);
            _docRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(doc);

            // Act
            var result = await _service.UpdateDocumentAsync(1, newFile.Object, "Test", "Cat", "Auth", "Desc");

            // Assert
            Assert.True(result);
            Assert.NotEqual("/Uploads/old.pdf", doc.FilePath);
        }

        [Fact]
        public async Task GetDocumentPageByIdAsync_Should_Return_Page_With_Bookmark()
        {
            // Arrange
            var expectedPage = new DocumentPage 
            { 
                Id = 1,
                Bookmark = new Bookmark { Id = 1, Title = "Test Bookmark" }
            };
            _pageRepoMock.Setup(r => r.GetByIdWithDocumentAsync(1)).ReturnsAsync(expectedPage);

            // Act
            var result = await _service.GetDocumentPageByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Bookmark);
            _pageRepoMock.Verify(r => r.GetByIdWithDocumentAsync(1), Times.Once);
        }

        [Fact]
        public async Task GetBookmarksAsync_Should_Return_Bookmarks()
        {
            // Arrange
            var expectedBookmarks = new List<Bookmark>
            {
                new Bookmark { Id = 1 },
                new Bookmark { Id = 2 }
            };
            _bookmarkRepoMock.Setup(r => r.GetAllWithDetailsAsync()).ReturnsAsync(expectedBookmarks);

            // Act
            var result = await _service.GetBookmarksAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        private Mock<IFormFile> CreateMockFile(string fileName, string contentType, long length)
        {
            var mockFile = new Mock<IFormFile>();
            var content = new byte[length];
            var ms = new MemoryStream(content);

            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, CancellationToken>((stream, token) => 
                    {
                        ms.Position = 0;
                        ms.CopyTo(stream);
                    })
                    .Returns(Task.CompletedTask);

            return mockFile;
        }

        private Mock<IFormFile> CreateMockPdfFile(string fileName, string contentType)
        {
            var mockFile = new Mock<IFormFile>();
            // Create a minimal valid PDF file
            var pdfContent = System.Text.Encoding.ASCII.GetBytes(
                "%PDF-1.4\n" +
                "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
                "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\n" +
                "xref\n0 4\n0000000000 65535 f\n" +
                "0000000009 00000 n\n" +
                "0000000058 00000 n\n" +
                "0000000115 00000 n\n" +
                "trailer<</Size 4/Root 1 0 R>>\n" +
                "startxref\n178\n%%EOF");
            var ms = new MemoryStream(pdfContent);

            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(pdfContent.Length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, CancellationToken>((stream, token) => 
                    {
                        ms.Position = 0;
                        ms.CopyTo(stream);
                    })
                    .Returns(Task.CompletedTask);

            return mockFile;
        }

        public void Dispose()
        {
            _context?.Dispose();
            if (Directory.Exists(_tempPath))
            {
                try
                {
                    Directory.Delete(_tempPath, true);
                }
                catch { }
            }
        }
    }
}
