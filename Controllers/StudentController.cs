using Microsoft.AspNetCore.Authorization;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const string StudentCodePrefix = "282105";

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Trang danh sách sinh viên
        public IActionResult Index()
        {
            return View();
        }

        // Danh sách sinh viên, tìm kiếm và phân trang
        [HttpGet]
        public async Task<IActionResult> List(
            string? keyword,
            int page = 1,
            int pageSize = 5)
        {
            page = Math.Max(1, page);

            int[] allowedPageSizes = { 5, 10, 20, 50 };

            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 5;
            }

            var query = _context.Students
                .AsNoTracking()
                .Include(s => s.Faculty)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(s =>
                    (s.StudentCode != null &&
                     s.StudentCode.Contains(keyword)) ||

                    s.FullName.Contains(keyword) ||

                    (s.Address != null &&
                     s.Address.Contains(keyword)) ||

                    (s.University != null &&
                     s.University.Contains(keyword)) ||

                    (s.Faculty != null &&
                     s.Faculty.Name.Contains(keyword))
                );
            }

            int totalCount = await query.CountAsync();

            int totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize
            );

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.FullName,
                    s.Address,
                    s.University,
                    s.FacultyId,
                    FacultyName = s.Faculty != null
                        ? s.Faculty.Name
                        : ""
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

        // Lấy danh sách khoa cho dropdown
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Faculties()
        {
            var faculties = await _context.Faculties
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .Select(f => new
                {
                    id = f.Id,
                    name = f.Name
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                data = faculties
            });
        }

        // Lấy thông tin một sinh viên để sửa
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.FullName,
                    s.Address,
                    s.University,
                    s.FacultyId
                })
                .FirstOrDefaultAsync();

            if (student == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sinh viên."
                });
            }

            return Json(new
            {
                success = true,
                data = student
            });
        }

        // Thêm hoặc cập nhật sinh viên
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Student student)
        {
            // Mã sinh viên do hệ thống tạo, không lấy từ biểu mẫu.
            ModelState.Remove(nameof(Student.StudentCode));

            // Không kiểm tra navigation property từ biểu mẫu.
            ModelState.Remove(nameof(Student.Faculty));

            student.FullName = student.FullName?.Trim() ?? "";
            student.Address = student.Address?.Trim();
            student.University = student.University?.Trim();

            // Kiểm tra khoa được chọn có tồn tại trong database không.
            if (!student.FacultyId.HasValue ||
                !await _context.Faculties.AnyAsync(
                    f => f.Id == student.FacultyId.Value))
            {
                ModelState.AddModelError(
                    nameof(Student.FacultyId),
                    "Vui lòng chọn khoa hợp lệ."
                );
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x =>
                        x.Value != null &&
                        x.Value.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors
                            .Select(e =>
                                string.IsNullOrWhiteSpace(e.ErrorMessage)
                                    ? "Dữ liệu không hợp lệ."
                                    : e.ErrorMessage)
                            .ToArray()
                    );

                return BadRequest(new
                {
                    success = false,
                    message = "Vui lòng kiểm tra lại thông tin.",
                    errors
                });
            }

            try
            {
                if (student.Id == 0)
                {
                    student.StudentCode =
                        await GenerateStudentCodeAsync();

                    student.Faculty = null;

                    _context.Students.Add(student);
                }
                else
                {
                    var existing = await _context.Students
                        .FirstOrDefaultAsync(s => s.Id == student.Id);

                    if (existing == null)
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = "Không tìm thấy sinh viên."
                        });
                    }

                    // Giữ nguyên mã sinh viên khi cập nhật.
                    existing.FullName = student.FullName;
                    existing.Address = student.Address;
                    existing.University = student.University;
                    existing.FacultyId = student.FacultyId;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = student.Id == 0
                        ? "Thêm sinh viên thành công."
                        : "Cập nhật sinh viên thành công."
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Không thể lưu dữ liệu vào cơ sở dữ liệu.",
                    detail = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi lưu sinh viên.",
                    detail = ex.Message
                });
            }
        }
[Authorize(Roles = "Admin")]
        // Xóa sinh viên
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy sinh viên."
                });
            }

            _context.Students.Remove(student);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Xóa sinh viên thành công."
            });
        }

        // Tạo mã sinh viên tự động: 2821050001, 2821050002...
        private async Task<string> GenerateStudentCodeAsync()
        {
            var codes = await _context.Students
                .Where(s =>
                    s.StudentCode != null &&
                    s.StudentCode.StartsWith(StudentCodePrefix))
                .Select(s => s.StudentCode!)
                .ToListAsync();

            int maxNumber = 0;

            foreach (var code in codes)
            {
                string suffix = code.Substring(StudentCodePrefix.Length);

                if (int.TryParse(suffix, out int number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }

            return StudentCodePrefix +
                (maxNumber + 1).ToString("D4");
        }
    }
}
