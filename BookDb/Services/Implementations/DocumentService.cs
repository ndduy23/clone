using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Services.Interfaces;
using BookDb.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

namespace BookDb.Services.Implementations
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _docRepo;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;
        private readonly INotificationService? _notificationService;

        public DocumentService(
            IDocumentRepository docRepo,
            IWebHostEnvironment env,
            AppDbContext context,
            INotificationService? notificationService = null)
        {
            _docRepo = docRepo;
            _env = env;
            _context = context;
            _notificationService = notificationService;
        }

        public Task<List<Document>> GetDocumentsAsync(string? q, int page, int pageSize)
        {
            return _docRepo.GetPagedAndSearchedAsync(q, page, pageSize);
        }

        public Task<List<Document>> GetDocumentsAsync(string? q, string? userId, bool onlyMine, int page, int pageSize)
        {
            return _docRepo.GetPagedAndSearchedAsync(q, userId, onlyMine, page, pageSize);
        }

        public Task<Document?> GetDocumentForViewingAsync(int id)
        {
            return _docRepo.GetByIdWithPagesAsync(id);
        }

        public async Task CreateDocumentAsync(IFormFile file, string title, string category, string? authorName, string description, int? authorId = null, string? ownerId = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File not selected.");

            var allowed = new[] { ".pdf", ".docx", ".txt", ".xlsx", ".doc", ".xls" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                throw new ArgumentException("Format not supported. Allowed: " + string.Join(", ", allowed));

            var uploads = Path.Combine(_env.WebRootPath, "Uploads");
            if (!Directory.Exists(uploads))
                Directory.CreateDirectory(uploads);

            var storedName = $"{Guid.NewGuid()}{ext}";
            var savePath = Path.Combine(uploads, storedName);

            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new Document
            {
                Title = title,
                Category = category,
                Author = authorName ?? string.Empty,
                Description = description,
                FilePath = $"/Uploads/{storedName}",
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OwnerId = ownerId,
                AuthorId = authorId
            };

            // If authorId provided, resolve name
            if (authorId.HasValue)
            {
                var author = await _context.Authors.FindAsync(authorId.Value);
                if (author != null)
                {
                    doc.Author = author.Name;
                }
            }

            await _docRepo.AddAsync(doc);
            await _context.SaveChangesAsync();

            // Send notification
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyDocumentUploadedAsync(doc.Title);
                }
                catch
                {
                    // Ignore notification errors
                }
            }
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            var doc = await _docRepo.GetByIdAsync(id);
            if (doc == null) return false;

            var title = doc.Title;

            // Delete physical file
            if (!string.IsNullOrEmpty(doc.FilePath))
            {
                var physicalPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }

            _docRepo.Delete(doc);
            await _context.SaveChangesAsync();

            // Send notification
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyDocumentDeletedAsync(title);
                }
                catch
                {
                    // Ignore notification errors
                }
            }

            return true;
        }

        public async Task<bool> UpdateDocumentAsync(int id, IFormFile? file, string title, string category, int? authorId, string description)
        {
            var doc = await _docRepo.GetByIdAsync(id);
            if (doc == null) return false;

            doc.Title = title;
            doc.Category = category;
            doc.Description = description;
            doc.UpdatedAt = DateTime.UtcNow;

            // Update author information
            if (authorId.HasValue && authorId.Value > 0)
            {
                var author = await _context.Authors.FindAsync(authorId.Value);
                if (author != null)
                {
                    doc.AuthorId = authorId.Value;
                    doc.Author = author.Name;
                }
            }
            else
            {
                // Clear author if none selected
                doc.AuthorId = null;
                doc.Author = string.Empty;
            }

            // Update file if provided
            if (file != null && file.Length > 0)
            {
                // Delete old file
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // Save new file
                var uploads = Path.Combine(_env.WebRootPath, "Uploads");
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var storedName = $"{Guid.NewGuid()}{ext}";
                var newPath = Path.Combine(uploads, storedName);

                using (var stream = new FileStream(newPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                doc.FilePath = "/Uploads/" + storedName;
                doc.FileName = file.FileName;
                doc.FileSize = file.Length;
                doc.ContentType = file.ContentType;
            }

            _docRepo.Update(doc);
            await _context.SaveChangesAsync();

            // Send notification
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyDocumentUpdatedAsync(doc.Title);
                }
                catch
                {
                    // Ignore notification errors
                }
            }

            return true;
        }

        public Task<Document?> GetDocumentByIdAsync(int id) => _docRepo.GetByIdAsync(id);

        public async Task<DocumentPage?> GetDocumentPageByIdAsync(int id)
        {
            return await _context.DocumentPages
                .Include(p => p.Document)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}