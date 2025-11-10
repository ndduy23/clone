using Microsoft.AspNetCore.Mvc;
using BookDb.Services.Interfaces;
using BookDb.Models;
using Microsoft.AspNetCore.SignalR;
using BookDb.Hubs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BookDb.Controllers.Api
{
    [Route("api/documents")]
    [ApiController]
    public class DocumentsApiController : ControllerBase
    {
        private readonly IDocumentService _docService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<DocumentsApiController> _logger;

        public DocumentsApiController(
            IDocumentService docService,
            IHubContext<NotificationHub> hubContext,
            ILogger<DocumentsApiController> logger)
        {
            _docService = docService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // GET api/documents
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetDocuments([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyMine = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                List<Document> documents;

                if (onlyMine && !string.IsNullOrEmpty(userId))
                {
                    documents = await _docService.GetDocumentsAsync(q, userId, true, page, pageSize);
                }
                else
                {
                    documents = await _docService.GetDocumentsAsync(q, page, pageSize);
                }

                return Ok(new
                {
                    success = true,
                    data = documents.Select(d => new
                    {
                        d.Id,
                        d.Title,
                        d.Category,
                        d.Author,
                        CreatedAt = d.CreatedAt.ToString("dd/MM/yyyy"),
                        d.FileName,
                        FileSize = FormatFileSize(d.FileSize)
                    }),
                    count = documents.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET api/documents/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDocument(int id)
        {
            try
            {
                var doc = await _docService.GetDocumentByIdAsync(id);
                if (doc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tài liệu" });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        doc.Id,
                        doc.Title,
                        doc.Category,
                        doc.Author,
                        doc.Description,
                        doc.FileName,
                        FileSize = FormatFileSize(doc.FileSize),
                        CreatedAt = doc.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        UpdatedAt = doc.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE api/documents/{id}
        [HttpDelete("{id}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var doc = await _docService.GetDocumentByIdAsync(id);
                if (doc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tài liệu" });

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole(Roles.Admin);

                if (!isAdmin && !string.Equals(doc.OwnerId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                var title = doc.Title;
                var success = await _docService.DeleteDocumentAsync(id);

                if (success)
                {
                    // Send SignalR notification
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                        $"Tài liệu '{title}' đã bị xóa");

                    return Ok(new { success = true, message = "Đã xóa tài liệu thành công" });
                }

                return BadRequest(new { success = false, message = "Không thể xóa tài liệu" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST api/documents/upload
        [HttpPost("upload")]
        [Authorize]
        [RequestSizeLimit(100_000_000)] //100MB limit
        public async Task<IActionResult> UploadDocument([FromForm] IFormFile file,
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string? author,
            [FromForm] int? authorId,
            [FromForm] string description)
        {
            try
            {
                _logger.LogInformation("Upload attempt - Title: {Title}, File: {FileName}",
                    title, file?.FileName ?? "null");

                // Validate inputs
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Upload failed - No file provided");
                    return BadRequest(new { success = false, message = "Vui lòng chọn file" });
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    _logger.LogWarning("Upload failed - No title provided");
                    return BadRequest(new { success = false, message = "Vui lòng nhập tiêu đề" });
                }

                // Validate file size (50MB max)
                if (file.Length > 50 * 1024 * 1024)
                {
                    _logger.LogWarning("Upload failed - File too large: {Size}MB", file.Length / (1024 * 1024));
                    return BadRequest(new { success = false, message = "File quá lớn (tối đa 50MB)" });
                }

                // Validate file extension
                var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".xlsx", ".doc", ".xls" };
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    _logger.LogWarning("Upload failed - Invalid extension: {Extension}", ext);
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Định dạng file không hỗ trợ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}"
                    });
                }

                _logger.LogInformation("Starting document creation...");

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Create document
                await _docService.CreateDocumentAsync(file, title, category, author, description, authorId, userId);

                _logger.LogInformation("Document created successfully - Title: {Title}", title);

                // Send SignalR notification
                try
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                        $"Tài liệu mới đã được thêm: {title}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR notification");
                }

                return Ok(new { success = true, message = "Tải lên thành công" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Upload validation error");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // PUT api/documents/{id}
        [HttpPut("{id}")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateDocument(int id,
            [FromForm] IFormFile? file,
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string author,
            [FromForm] string description)
        {
            try
            {
                var doc = await _docService.GetDocumentByIdAsync(id);
                if (doc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tài liệu" });

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var isAdmin = User.IsInRole(Roles.Admin);

                if (!isAdmin && !string.Equals(doc.OwnerId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                var success = await _docService.UpdateDocumentAsync(id, file, title, category, author, description);

                if (!success)
                    return NotFound(new { success = false, message = "Không tìm thấy tài liệu" });

                // Get updated document to send full data via SignalR
                var updatedDoc = await _docService.GetDocumentByIdAsync(id);

                // Send SignalR notification with detailed document info
                await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                    $"Tài liệu '{title}' đã được cập nhật");

                // Send detailed document update event for auto-refresh
                await _hubContext.Clients.All.SendAsync("DocumentUpdated", new
                {
                    Id = id,
                    Title = updatedDoc.Title,
                    Category = updatedDoc.Category,
                    Author = updatedDoc.Author,
                    CreatedAt = updatedDoc.CreatedAt.ToString("dd/MM/yyyy"),
                    UpdatedAt = updatedDoc.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                return Ok(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET api/documents/search
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchDocuments([FromQuery] string query)
        {
            try
            {
                var documents = await _docService.GetDocumentsAsync(query, 1, 50);
                return Ok(new
                {
                    success = true,
                    data = documents.Select(d => new
                    {
                        d.Id,
                        d.Title,
                        d.Category,
                        d.Author
                    }),
                    count = documents.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}