using BookDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Controllers.Api
{
    [Route("api/authors")]
    [ApiController]
    public class AuthorsApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuthorsApiController> _logger;

        public AuthorsApiController(AppDbContext context, ILogger<AuthorsApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/authors
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAuthors([FromQuery] string? search)
        {
            try
            {
                var query = _context.Authors.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(a => a.Name.Contains(search));
                }

                var authors = await query
                    .OrderBy(a => a.Name)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Bio
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = authors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authors");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // GET: api/authors/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAuthor(int id)
        {
            try
            {
                var author = await _context.Authors.FindAsync(id);

                if (author == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tác giả" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        author.Id,
                        author.Name,
                        author.Bio
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting author {AuthorId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/authors
        [HttpPost]
        [Authorize(Policy = Policies.RequireAdminRole)]
        public async Task<IActionResult> CreateAuthor([FromBody] CreateAuthorDto model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return BadRequest(new { success = false, message = "Tên tác giả là bắt buộc" });
                }

                // Check if author already exists
                var existingAuthor = await _context.Authors
                    .FirstOrDefaultAsync(a => a.Name == model.Name);

                if (existingAuthor != null)
                {
                    return BadRequest(new { success = false, message = "Tác giả đã tồn tại" });
                }

                var author = new Author
                {
                    Name = model.Name.Trim(),
                    Bio = model.Bio?.Trim()
                };

                _context.Authors.Add(author);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Author created: {AuthorName}", author.Name);

                return Ok(new
                {
                    success = true,
                    message = "Đã thêm tác giả thành công",
                    data = new
                    {
                        author.Id,
                        author.Name,
                        author.Bio
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating author");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // PUT: api/authors/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = Policies.RequireAdminRole)]
        public async Task<IActionResult> UpdateAuthor(int id, [FromBody] UpdateAuthorDto model)
        {
            try
            {
                var author = await _context.Authors.FindAsync(id);

                if (author == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tác giả" });
                }

                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return BadRequest(new { success = false, message = "Tên tác giả là bắt buộc" });
                }

                // Check if name is already used by another author
                var existingAuthor = await _context.Authors
                    .FirstOrDefaultAsync(a => a.Name == model.Name && a.Id != id);

                if (existingAuthor != null)
                {
                    return BadRequest(new { success = false, message = "Tên tác giả đã được sử dụng" });
                }

                author.Name = model.Name.Trim();
                author.Bio = model.Bio?.Trim();

                await _context.SaveChangesAsync();

                _logger.LogInformation("Author updated: {AuthorName}", author.Name);

                return Ok(new
                {
                    success = true,
                    message = "Đã cập nhật tác giả thành công",
                    data = new
                    {
                        author.Id,
                        author.Name,
                        author.Bio
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating author {AuthorId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // DELETE: api/authors/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.RequireAdminRole)]
        public async Task<IActionResult> DeleteAuthor(int id)
        {
            try
            {
                var author = await _context.Authors.FindAsync(id);

                if (author == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tác giả" });
                }

                // Check if author is being used by any documents
                var documentsCount = await _context.Documents
                    .CountAsync(d => d.AuthorId == id);

                if (documentsCount > 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Không thể xóa tác giả vì có {documentsCount} tài liệu đang sử dụng"
                    });
                }

                _context.Authors.Remove(author);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Author deleted: {AuthorName}", author.Name);

                return Ok(new
                {
                    success = true,
                    message = "Đã xóa tác giả thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting author {AuthorId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }
    }

    public class CreateAuthorDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Bio { get; set; }
    }

    public class UpdateAuthorDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Bio { get; set; }
    }
}