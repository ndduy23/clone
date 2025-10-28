using BookDb.Models;
using BookDb.Repositories.Interfaces;
using BookDb.Repository.Interfaces;
using BookDb.Services.Interfaces;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using System.Net;
using System.Text;
using System.Linq;
using BookDb.Services;

namespace BookDb.Services.Implementations
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _docRepo;
        private readonly IDocumentPageRepository _pageRepo;
        private readonly IBookmarkRepository _bookmarkRepo;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _context;
        private readonly INotificationService? _notificationService;

        public DocumentService(
            IDocumentRepository docRepo,
            IDocumentPageRepository pageRepo,
            IBookmarkRepository bookmarkRepo,
            IWebHostEnvironment env,
            AppDbContext context,
            INotificationService? notificationService = null)
        {
            _docRepo = docRepo;
            _pageRepo = pageRepo;
            _bookmarkRepo = bookmarkRepo;
            _env = env;
            _context = context;
            _notificationService = notificationService;
        }

        public Task<List<Document>> GetDocumentsAsync(string? q, int page, int pageSize)
        {
            return _docRepo.GetPagedAndSearchedAsync(q, page, pageSize);
        }

        public Task<Document?> GetDocumentForViewingAsync(int id)
        {
            return _docRepo.GetByIdWithPagesAsync(id);
        }

        public async Task CreateDocumentAsync(IFormFile file, string title, string category, string author, string description)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File not selected.");

            var allowed = new[] { ".pdf", ".docx", ".txt", ".xlsx" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                throw new ArgumentException("Format not supported.");

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
                Author = author,
                Description = description,
                FilePath = $"/Uploads/{storedName}",
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _docRepo.AddAsync(doc);
            await _context.SaveChangesAsync();

            // Process file based on type
            string pageDir = Path.Combine(uploads, $"doc_{doc.Id}");
            if (!Directory.Exists(pageDir))
                Directory.CreateDirectory(pageDir);

            if (ext == ".pdf")
            {
                await SplitPdf(savePath, pageDir, doc.Id);
            }
            else if (ext == ".xlsx")
            {
                await SplitExcel(savePath, pageDir, doc.Id);
            }
            else if (ext == ".txt")
            {
                await SplitText(savePath, pageDir, doc.Id);
            }

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

        public async Task<bool> UpdateDocumentAsync(int id, IFormFile? file, string title, string category, string author, string description)
        {
            var doc = await _docRepo.GetByIdAsync(id);
            if (doc == null) return false;

            doc.Title = title;
            doc.Category = category;
            doc.Author = author;
            doc.Description = description;
            doc.UpdatedAt = DateTime.UtcNow;

            if (file != null && file.Length > 0)
            {
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var uploads = Path.Combine(_env.WebRootPath, "Uploads");
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var storedName = $"{Guid.NewGuid()}{ext}";
                var newPath = Path.Combine(uploads, storedName);
                using (var stream = new FileStream(newPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                doc.FilePath = "/Uploads/" + storedName;
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

        public Task<DocumentPage?> GetDocumentPageByIdAsync(int id) => _pageRepo.GetByIdWithDocumentAsync(id);

        public Task<List<Bookmark>> GetBookmarksAsync() => _bookmarkRepo.GetAllWithDetailsAsync();

        private async Task SplitPdf(string sourcePath, string outputDir, int documentId)
        {
            using var reader = new PdfReader(sourcePath);
            using var pdf = new PdfDocument(reader);
            int totalPages = pdf.GetNumberOfPages();

            for (int i = 1; i <= totalPages; i++)
            {
                string outputFile = Path.Combine(outputDir, $"page_{i}.pdf");
                using (var writer = new PdfWriter(outputFile))
                using (var newPdf = new PdfDocument(writer))
                {
                    pdf.CopyPagesTo(i, i, newPdf);
                }

                var page = new DocumentPage
                {
                    DocumentId = documentId,
                    PageNumber = i,
                    FilePath = outputFile.Replace(_env.WebRootPath, "").Replace("\\", "/"),
                    TextContent = null
                };
                await _pageRepo.AddAsync(page);
            }
        }

        private async Task SplitExcel(string sourcePath, string outputDir, int documentId)
        {
            using var workbook = new XLWorkbook(sourcePath);

            foreach (var sheet in workbook.Worksheets)
            {
                string safeName = string.Join("_", sheet.Name.Split(Path.GetInvalidFileNameChars()));
                string outputFile = Path.Combine(outputDir, $"{safeName}.html");
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    await writer.WriteLineAsync("<!doctype html>");
                    await writer.WriteLineAsync("<html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + WebUtility.HtmlEncode(sheet.Name) + "</title>");
                    await writer.WriteLineAsync("<style>table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:8px;text-align:left}th{background:#f4f4f4;font-weight:bold}</style>");
                    await writer.WriteLineAsync("</head><body>");
                    await writer.WriteLineAsync("<h3>Sheet: " + WebUtility.HtmlEncode(sheet.Name) + "</h3>");

                    var range = sheet.RangeUsed();
                    await writer.WriteLineAsync("<table>");
                    if (range != null)
                    {
                        for (int row = 1; row <= range.RowCount(); row++)
                        {
                            await writer.WriteLineAsync("<tr>");
                            for (int col = 1; col <= range.ColumnCount(); col++)
                            {
                                var cellValue = sheet.Cell(row, col).GetValue<string>();
                                var encoded = WebUtility.HtmlEncode(cellValue);
                                if (row == 1)
                                    await writer.WriteLineAsync($"<th>{encoded}</th>");
                                else
                                    await writer.WriteLineAsync($"<td>{encoded}</td>");
                            }
                            await writer.WriteLineAsync("</tr>");
                        }
                    }
                    else
                    {
                        await writer.WriteLineAsync("<tr><td>(Empty sheet)</td></tr>");
                    }
                    await writer.WriteLineAsync("</table>");
                    await writer.WriteLineAsync("</body></html>");
                }

                var page = new DocumentPage
                {
                    DocumentId = documentId,
                    PageNumber = sheet.Position,
                    FilePath = outputFile.Replace(_env.WebRootPath, "").Replace("\\", "/"),
                    TextContent = $"Sheet: {sheet.Name}",
                    ContentType = "text/html"
                };

                await _pageRepo.AddAsync(page);
            }
        }

        private async Task SplitText(string sourcePath, string outputDir, int documentId)
        {
            string content = await File.ReadAllTextAsync(sourcePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(content))
                return;

            var words = content.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            const int wordsPerPage = 700;
            int totalPages = (int)Math.Ceiling((double)words.Length / wordsPerPage);

            for (int p = 0; p < totalPages; p++)
            {
                var pageWords = words.Skip(p * wordsPerPage).Take(wordsPerPage);
                var pageText = string.Join(" ", pageWords);

                string outputFile = Path.Combine(outputDir, $"page_{p + 1}.html");
                using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    await writer.WriteLineAsync("<!doctype html>");
                    await writer.WriteLineAsync("<html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><style>body{font-family:Arial,Helvetica,sans-serif;padding:20px;line-height:1.6;white-space:pre-wrap}</style></head><body>");
                    await writer.WriteLineAsync(WebUtility.HtmlEncode(pageText));
                    await writer.WriteLineAsync("</body></html>");
                }

                var page = new DocumentPage
                {
                    DocumentId = documentId,
                    PageNumber = p + 1,
                    FilePath = outputFile.Replace(_env.WebRootPath, "").Replace("\\", "/"),
                    TextContent = pageText.Length > 700 ? pageText.Substring(0, 700) + "..." : pageText,
                    ContentType = "text/html"
                };

                await _pageRepo.AddAsync(page);
            }
        }
    }
}