using Microsoft.AspNetCore.Authorization;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FacultyController(ApplicationDbContext context)
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

            var query = _context.Faculties.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(f => f.Name.Contains(keyword));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            page = totalPages > 0 ? Math.Min(page, totalPages) : 1;

            var items = await query
                .OrderBy(f => f.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new
                {
                    f.Id,
                    f.Name,
                    StudentCount = f.Students.Count()
                })
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
            var faculty = await _context.Faculties
                .Where(f => f.Id == id)
                .Select(f => new
                {
                    f.Id,
                    f.Name
                })
                .FirstOrDefaultAsync();

            if (faculty == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy khoa."
                });
            }

            return Json(new { success = true, data = faculty });
        }
[Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Faculty faculty)
        {
            ModelState.Remove(nameof(Faculty.Students));

            faculty.Name = faculty.Name?.Trim() ?? "";

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Vui lòng kiểm tra lại thông tin.",
                    errors = ModelState
                        .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
                });
            }

            if (faculty.Id == 0)
            {
                _context.Faculties.Add(faculty);
            }
            else
            {
                var existing = await _context.Faculties.FindAsync(faculty.Id);

                if (existing == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy khoa cần cập nhật."
                    });
                }

                existing.Name = faculty.Name;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = faculty.Id == 0
                    ? "Thêm khoa thành công."
                    : "Cập nhật khoa thành công."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var faculty = await _context.Faculties
                .Include(f => f.Students)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (faculty == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy khoa."
                });
            }

            if (faculty.Students.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể xóa khoa đang có sinh viên."
                });
            }

            _context.Faculties.Remove(faculty);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Xóa khoa thành công."
            });
        }

        private static int ValidPageSize(int pageSize)
        {
            return new[] { 5, 10, 20, 50 }.Contains(pageSize)
                ? pageSize : 5;
        }
    }
}
