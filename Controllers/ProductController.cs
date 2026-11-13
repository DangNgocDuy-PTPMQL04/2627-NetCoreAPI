using Microsoft.AspNetCore.Authorization;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List(
            string? keyword,
            int page = 1,
            int pageSize = 5)
        {
            page = Math.Max(1, page);
            pageSize = ValidPageSize(pageSize);

            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    p.Category.Contains(keyword));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            page = totalPages > 0 ? Math.Min(page, totalPages) : 1;

            var items = await query
                .OrderBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Json(new
            {
                success = true,
                data = new
                {
                    items,
                    totalCount,
                    page,
                    pageSize,
                    totalPages
                }
            });
        }
[Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sản phẩm."
                });
            }

            return Json(new { success = true, data = product });
        }
[Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Product product)
        {
            product.Name = product.Name?.Trim() ?? "";
            product.Category = product.Category?.Trim() ?? "";

            
if (!ModelState.IsValid)
{
    var errors = ModelState
        .Where(x => x.Value != null && x.Value.Errors.Count > 0)
        .ToDictionary(
            x => x.Key,
            x => x.Value!.Errors.Select(e =>
                string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? "Dữ liệu không đúng định dạng."
                    : e.ErrorMessage
            ).ToArray()
        );

    var errorMessage = string.Join(
        " ",
        errors.SelectMany(x => x.Value)
    );

    return BadRequest(new
    {
        success = false,
        message = errorMessage,
        errors
    });
}


            if (product.Id == 0)
            {
                _context.Products.Add(product);
            }
            else
            {
                var existing = await _context.Products.FindAsync(product.Id);

                if (existing == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm cần cập nhật."
                    });
                }

                existing.Name = product.Name;
                existing.Price = product.Price;
                existing.Quantity = product.Quantity;
                existing.Category = product.Category;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = product.Id == 0
                    ? "Thêm sản phẩm thành công."
                    : "Cập nhật sản phẩm thành công."
            });
        }
[Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sản phẩm."
                });
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Xóa sản phẩm thành công."
            });
        }

        private static int ValidPageSize(int pageSize)
        {
            return new[] { 5, 10, 20, 50 }.Contains(pageSize)
                ? pageSize : 5;
        }
    }
}
