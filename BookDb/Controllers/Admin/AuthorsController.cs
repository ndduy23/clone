using BookDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Controllers.Admin
{
 [Area("Admin")]
 [Route("admin/authors")]
 [Authorize(Roles = Roles.Admin)]
 public class AuthorsController : Controller
 {
 private readonly AppDbContext _context;
 private readonly ILogger<AuthorsController> _logger;

 public AuthorsController(AppDbContext context, ILogger<AuthorsController> logger)
 {
 _context = context;
 _logger = logger;
 }

 // GET admin/authors
 [HttpGet("")]
 public async Task<IActionResult> Index()
 {
 var authors = await _context.Authors.OrderBy(a => a.Name).ToListAsync();
 return View(authors);
 }

 // POST admin/authors/create
 [HttpPost("create")]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> Create(string name, string? bio)
 {
 if (string.IsNullOrWhiteSpace(name))
 {
 TempData["Error"] = "Tên tác gi? là b?t bu?c.";
 return RedirectToAction("Index");
 }

 if (await _context.Authors.AnyAsync(a => a.Name == name.Trim()))
 {
 TempData["Error"] = "Tác gi? ?ã t?n t?i.";
 return RedirectToAction("Index");
 }

 var author = new Author { Name = name.Trim(), Bio = bio };
 _context.Authors.Add(author);
 await _context.SaveChangesAsync();

 TempData["Success"] = "?ã thêm tác gi?.";
 return RedirectToAction("Index");
 }

 // POST admin/authors/delete/{id}
 [HttpPost("delete/{id}")]
 [ValidateAntiForgeryToken]
 public async Task<IActionResult> Delete(int id)
 {
 var author = await _context.Authors.FindAsync(id);
 if (author == null)
 {
 TempData["Error"] = "Không tìm th?y tác gi?.";
 return RedirectToAction("Index");
 }

 // Optional: prevent deleting author linked to documents
 var hasDocs = await _context.Documents.AnyAsync(d => d.AuthorId == id);
 if (hasDocs)
 {
 TempData["Error"] = "Không th? xóa tác gi? có tài li?u liên quan.";
 return RedirectToAction("Index");
 }

 _context.Authors.Remove(author);
 await _context.SaveChangesAsync();

 TempData["Success"] = "?ã xóa tác gi?.";
 return RedirectToAction("Index");
 }
 }
}
