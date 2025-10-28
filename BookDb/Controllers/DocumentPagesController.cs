using BookDb.Models;
using BookDb.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BookDb.Controllers
{
    public class DocumentPagesController : Controller
    {
        private readonly IDocumentPageService _pageService;

        public DocumentPagesController(IDocumentPageService pageService)
        {
            _pageService = pageService;
        }

        [HttpGet("edit-page/{id}")]
        public async Task<IActionResult> EditPage(int id)
        {
            var page = await _pageService.GetPageByIdAsync(id);
            if (page == null) return NotFound();
            return View(page);
        }

        [HttpPost("edit-page/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPage(int id, DocumentPage model)
        {
            if (id != model.Id) return BadRequest();

            try
            {
                await _pageService.UpdatePageAsync(id, model.TextContent);
                return RedirectToAction("ViewDocument", "Documents", new { id = model.DocumentId, page = model.PageNumber });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet("document/{documentId}/pages")]
        public async Task<IActionResult> ListPages(int documentId)
        {
            var pages = await _pageService.GetPagesOfDocumentAsync(documentId);
            return View(pages);
        }
    }
}
