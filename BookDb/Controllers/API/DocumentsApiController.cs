using Microsoft.AspNetCore.Mvc;
using BookDb.Services.Interfaces;
using BookDb.Models;
using Microsoft.AspNetCore.SignalR;
using BookDb.Hubs;

namespace BookDb.Controllers.Api
{
    [Route("api/documents")]
    [ApiController]
    public class DocumentsApiController : ControllerBase
    {
        private readonly IDocumentService _docService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public DocumentsApiController(IDocumentService docService, IHubContext<NotificationHub> hubContext)
        {
            _docService = docService;
            _hubContext = hubContext;
        }

        // GET api/documents
        [HttpGet]
        public async Task<IActionResult> GetDocuments([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var documents = await _docService.GetDocumentsAsync(q, page, pageSize);
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
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET api/documents/{id}
        [HttpGet("{id}")]
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
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE api/documents/{id}
        [HttpDelete("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var doc = await _docService.GetDocumentByIdAsync(id);
                if (doc == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tài liệu" });

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
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST api/documents/upload
        [HttpPost("upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument([FromForm] IFormFile file,
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string author,
            [FromForm] string description)
        {
            try
            {
                await _docService.CreateDocumentAsync(file, title, category, author, description);

                // Send SignalR notification
                await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                    $"Tài liệu mới đã được thêm: {title}");

                return Ok(new { success = true, message = "Tải lên thành công" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // PUT api/documents/{id}
        [HttpPut("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDocument(int id,
            [FromForm] IFormFile? file,
            [FromForm] string title,
            [FromForm] string category,
            [FromForm] string author,
            [FromForm] string description)
        {
            try
            {
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
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET api/documents/search
        [HttpGet("search")]
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