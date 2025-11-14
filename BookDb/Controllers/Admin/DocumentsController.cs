using BookDb.Models;
using BookDb.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookDb.Controllers.Admin
{
    [Area("Admin")]
    [Route("admin/documents")]
    [Authorize(Roles = Roles.Admin)]
    public class DocumentsController : Controller
    {
        private readonly IDocumentService _docService;

        public DocumentsController(IDocumentService docService)
        {
            _docService = docService;
        }

        // GET admin/documents -> redirect to public documents index which already supports admin management
        [HttpGet("")]
        public IActionResult Index(string? q, int page = 1, int pageSize = 20)
        {
            // Redirect to the main documents index to reuse search, filters and scripts
            return RedirectToAction("Index", "Documents", new { q, page, pageSize, onlyMine = false });
        }

        // GET admin/documents/view/{id}
        [HttpGet("view/{id}")]
        public IActionResult ViewDocument(int id)
        {
            // Reuse public view action
            return RedirectToAction("ViewDocument", "Documents", new { id });
        }

        // GET admin/documents/edit/{id}
        [HttpGet("edit/{id}")]
        public IActionResult Edit(int id)
        {
            // Redirect to the Documents edit page which performs authorization checks
            return RedirectToAction("Edit", "Documents", new { id });
        }

        // POST admin/documents/delete/{id}
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _docService.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();

            var success = await _docService.DeleteDocumentAsync(id);
            if (!success) return BadRequest(new { success = false, message = "Không thể xóa tài liệu" });

            // If AJAX request, return JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true, message = "Đã xóa" });
            }

            return RedirectToAction("Index");
        }
    }
}
