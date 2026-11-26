
using ClosedXML.Excel;
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DemoMVC.Services
{
    public class ExcelImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ExcelImportService(
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        // =========================
        // 1. XEM TRƯỚC VÀ KIỂM TRA EXCEL
        // =========================
        public async Task<ImportViewModel> PreviewAsync(Stream stream)
        {
            var result = new ImportViewModel
            {
                IsPreview = true
            };

            try
            {
                using var workbook = new XLWorkbook(stream);

                var faculties = ReadSheet(
                    workbook,
                    "Faculties",
                    new[] { "Name" },
                    result);

                var students = ReadSheet(
                    workbook,
                    "Students",
                    new[]
                    {
                        "StudentCode",
                        "FullName",
                        "Address",
                        "University",
                        "FacultyName"
                    },
                    result);

                var products = ReadSheet(
                    workbook,
                    "Products",
                    new[] { "Name", "Price", "Quantity", "Category" },
                    result);

                var permissions = ReadSheet(
                    workbook,
                    "Permissions",
                    new[] { "Controller", "Action", "DisplayName" },
                    result);

                var rolePermissions = ReadSheet(
                    workbook,
                    "RolePermissions",
                    new[] { "RoleName", "Controller", "Action" },
                    result);

                result.TotalRows =
                    faculties.Count +
                    students.Count +
                    products.Count +
                    permissions.Count +
                    rolePermissions.Count;

                var errors = new List<string>();

                ValidateFaculties(faculties, errors);
                ValidateStudents(students, errors);
                ValidateProducts(products, errors);
                ValidatePermissions(permissions, errors);
                ValidateRolePermissions(rolePermissions, errors);

                await ValidateDatabaseAsync(
                    faculties,
                    students,
                    permissions,
                    rolePermissions,
                    errors);

                result.Errors.AddRange(errors);

                result.InvalidRows = errors.Count;

                result.ValidRows = Math.Max(
                    0,
                    result.TotalRows - result.InvalidRows);

                result.Message = errors.Count == 0
                    ? "File Excel hợp lệ, có thể tiến hành nhập dữ liệu."
                    : "File Excel có dữ liệu chưa hợp lệ. Vui lòng kiểm tra lỗi.";
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    "Không thể đọc file Excel: " + ex.Message);

                result.InvalidRows = 1;
                result.Message = "Đọc file Excel thất bại.";
            }

            return result;
        }

        // =========================
        // 2. NHẬP DỮ LIỆU EXCEL
        // =========================
        public async Task<ImportViewModel> ImportAsync(
            Stream stream,
            string importMode,
            bool confirmOverwrite)
        {
            var preview = await PreviewAsync(stream);

            if (preview.Errors.Count > 0)
                return preview;

            if (importMode != "Append" &&
                importMode != "Overwrite")
            {
                preview.Errors.Add(
                    "Chế độ nhập dữ liệu không hợp lệ.");

                return preview;
            }

            if (importMode == "Overwrite" && !confirmOverwrite)
            {
                preview.Errors.Add(
                    "Bạn phải xác nhận trước khi ghi đè dữ liệu.");

                return preview;
            }

            try
            {
                if (!stream.CanSeek)
                {
                    throw new Exception(
                        "Không thể đọc lại file Excel để nhập dữ liệu.");
                }

                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);

                var readResult = new ImportViewModel();

                var faculties = ReadSheet(
                    workbook,
                    "Faculties",
                    new[] { "Name" },
                    readResult);

                var students = ReadSheet(
                    workbook,
                    "Students",
                    new[]
                    {
                        "StudentCode",
                        "FullName",
                        "Address",
                        "University",
                        "FacultyName"
                    },
                    readResult);

                var products = ReadSheet(
                    workbook,
                    "Products",
                    new[] { "Name", "Price", "Quantity", "Category" },
                    readResult);

                var permissions = ReadSheet(
                    workbook,
                    "Permissions",
                    new[] { "Controller", "Action", "DisplayName" },
                    readResult);

                var rolePermissions = ReadSheet(
                    workbook,
                    "RolePermissions",
                    new[] { "RoleName", "Controller", "Action" },
                    readResult);

                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    // =========================
                    // GHI ĐÈ DỮ LIỆU
                    // =========================
                    if (importMode == "Overwrite")
                    {
                        // Chỉ xóa các bảng nghiệp vụ.
                        // Không xóa bất kỳ bảng Identity nào.

                        _context.RolePermissions.RemoveRange(
                            _context.RolePermissions);

                        _context.Students.RemoveRange(
                            _context.Students);

                        _context.Products.RemoveRange(
                            _context.Products);

                        _context.Permissions.RemoveRange(
                            _context.Permissions);

                        _context.Faculties.RemoveRange(
                            _context.Faculties);

                        await _context.SaveChangesAsync();
                    }

                    // =========================
                    // 1. IMPORT FACULTIES
                    // =========================
                    foreach (var row in faculties)
                    {
                        string name = Cell(row, "Name");

                        bool exists = await _context.Faculties
                            .AnyAsync(x => x.Name == name);

                        if (!exists)
                        {
                            _context.Faculties.Add(new Faculty
                            {
                                Name = name
                            });
                        }
                    }

                    await _context.SaveChangesAsync();

                    // =========================
                    // 2. IMPORT STUDENTS
                    // =========================
                    foreach (var row in students)
                    {
                        string facultyName = Cell(row, "FacultyName");

                        var faculty = await _context.Faculties
                            .FirstOrDefaultAsync(
                                x => x.Name == facultyName);

                        if (faculty == null)
                        {
                            throw new Exception(
                                $"Không tìm thấy khoa: {facultyName}");
                        }

                        string studentCode = Cell(row, "StudentCode");

                        if (!string.IsNullOrWhiteSpace(studentCode))
                        {
                            bool exists = await _context.Students
                                .AnyAsync(
                                    x => x.StudentCode == studentCode);

                            if (exists)
                                continue;
                        }

                        _context.Students.Add(new Student
                        {
                            StudentCode = string.IsNullOrWhiteSpace(studentCode)
                                ? null
                                : studentCode,

                            FullName = Cell(row, "FullName"),

                            Address = NullableCell(row, "Address"),

                            University = NullableCell(row, "University"),

                            FacultyId = faculty.Id
                        });
                    }

                    await _context.SaveChangesAsync();

                    // =========================
                    // 3. IMPORT PRODUCTS
                    // =========================
                    foreach (var row in products)
                    {
                        string name = Cell(row, "Name");
                        string category = Cell(row, "Category");

                        decimal? price = ReadNullableDecimal(
                            row, "Price");

                        int? quantity = ReadNullableInt(
                            row, "Quantity");

                        bool exists = await _context.Products
                            .AnyAsync(x =>
                                x.Name == name &&
                                x.Category == category);

                        if (!exists)
                        {
                            _context.Products.Add(new Product
                            {
                                Name = name,
                                Category = category,
                                Price = price,
                                Quantity = quantity
                            });
                        }
                    }

                    await _context.SaveChangesAsync();

                    // =========================
                    // 4. IMPORT PERMISSIONS
                    // =========================
                    foreach (var row in permissions)
                    {
                        string controller = Cell(row, "Controller");
                        string action = Cell(row, "Action");

                        bool exists = await _context.Permissions
                            .AnyAsync(x =>
                                x.Controller == controller &&
                                x.Action == action);

                        if (!exists)
                        {
                            _context.Permissions.Add(new Permission
                            {
                                Controller = controller,
                                Action = action,
                                DisplayName = Cell(row, "DisplayName")
                            });
                        }
                    }

                    await _context.SaveChangesAsync();

                    // =========================
                    // 5. IMPORT ROLE PERMISSIONS
                    // =========================
                    foreach (var row in rolePermissions)
                    {
                        string roleName = Cell(row, "RoleName");
                        string controller = Cell(row, "Controller");
                        string action = Cell(row, "Action");

                        var role = await _roleManager
                            .FindByNameAsync(roleName);

                        if (role == null)
                        {
                            throw new Exception(
                                $"Không tìm thấy Role: {roleName}");
                        }

                        var permission = await _context.Permissions
                            .FirstOrDefaultAsync(x =>
                                x.Controller == controller &&
                                x.Action == action);

                        if (permission == null)
                        {
                            throw new Exception(
                                $"Không tìm thấy quyền: {controller}/{action}");
                        }

                        bool exists = await _context.RolePermissions
                            .AnyAsync(x =>
                                x.RoleId == role.Id &&
                                x.PermissionId == permission.Id);

                        if (!exists)
                        {
                            _context.RolePermissions.Add(
                                new RolePermission
                                {
                                    RoleId = role.Id,
                                    PermissionId = permission.Id
                                });
                        }
                    }

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    preview.Message = importMode == "Append"
                        ? "Nhập bổ sung dữ liệu thành công."
                        : "Ghi đè dữ liệu thành công.";

                    preview.IsPreview = false;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                preview.Errors.Add(
                    "Nhập dữ liệu thất bại: " + ex.Message);

                preview.Message = "Không thể nhập dữ liệu.";
            }

            return preview;
        }

        // =========================
        // 3. ĐỌC SHEET EXCEL
        // =========================
        private static List<IXLRow> ReadSheet(
            XLWorkbook workbook,
            string sheetName,
            string[] requiredHeaders,
            ImportViewModel result)
        {
            var rows = new List<IXLRow>();

            if (!workbook.Worksheets.TryGetWorksheet(
                sheetName, out var sheet))
            {
                result.Errors.Add(
                    $"Thiếu sheet '{sheetName}'.");

                return rows;
            }

            var headerRow = sheet.FirstRowUsed();

            if (headerRow == null)
            {
                result.Errors.Add(
                    $"Sheet '{sheetName}' đang trống.");

                return rows;
            }

            var headers = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.CellsUsed())
            {
                string header = cell.GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header))
                {
                    headers[header] =
                        cell.Address.ColumnNumber;
                }
            }

            foreach (string header in requiredHeaders)
            {
                if (!headers.ContainsKey(header))
                {
                    result.Errors.Add(
                        $"Sheet '{sheetName}' thiếu cột '{header}'.");
                }
            }

            if (requiredHeaders.Any(
                x => !headers.ContainsKey(x)))
            {
                return rows;
            }

            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                if (row.CellsUsed().All(
                    c => string.IsNullOrWhiteSpace(c.GetString())))
                {
                    continue;
                }

                rows.Add(row);
            }

            return rows;
        }

        // =========================
        // 4. ĐỌC GIÁ TRỊ Ô
        // =========================
        private static string Cell(
            IXLRow row,
            string column)
        {
            var header = row.Worksheet.FirstRowUsed();

            if (header == null)
                return string.Empty;

            var headerCell = header.CellsUsed()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.GetString().Trim(),
                        column,
                        StringComparison.OrdinalIgnoreCase));

            if (headerCell == null)
                return string.Empty;

            int columnNumber =
                headerCell.Address.ColumnNumber;

            return row.Cell(columnNumber).GetString().Trim();
        }

        private static string? NullableCell(
            IXLRow row,
            string column)
        {
            string value = Cell(row, column);

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }

        private static decimal? ReadNullableDecimal(
            IXLRow row,
            string column)
        {
            string value = Cell(row, column);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal result))
            {
                return result;
            }

            if (decimal.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.GetCultureInfo("vi-VN"),
                out result))
            {
                return result;
            }

            throw new Exception(
                $"{column} không hợp lệ tại dòng {row.RowNumber()}.");
        }

        private static int? ReadNullableInt(
            IXLRow row,
            string column)
        {
            string value = Cell(row, column);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(value, out int result))
                return result;

            throw new Exception(
                $"{column} không hợp lệ tại dòng {row.RowNumber()}.");
        }

        // =========================
        // 5. KIỂM TRA FACULTIES
        // =========================
        private static void ValidateFaculties(
            List<IXLRow> rows,
            List<string> errors)
        {
            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                string name = Cell(row, "Name");

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(
                        $"Faculties dòng {row.RowNumber()}: Thiếu Name.");

                    continue;
                }

                if (!names.Add(name))
                {
                    errors.Add(
                        $"Faculties dòng {row.RowNumber()}: Tên khoa bị trùng.");
                }
            }
        }

        // =========================
        // 6. KIỂM TRA STUDENTS
        // =========================
        private static void ValidateStudents(
            List<IXLRow> rows,
            List<string> errors)
        {
            var codes = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(
                    Cell(row, "FullName")))
                {
                    errors.Add(
                        $"Students dòng {row.RowNumber()}: Thiếu FullName.");
                }

                if (string.IsNullOrWhiteSpace(
                    Cell(row, "FacultyName")))
                {
                    errors.Add(
                        $"Students dòng {row.RowNumber()}: Thiếu FacultyName.");
                }

                string code = Cell(row, "StudentCode");

                if (!string.IsNullOrWhiteSpace(code) &&
                    !codes.Add(code))
                {
                    errors.Add(
                        $"Students dòng {row.RowNumber()}: StudentCode bị trùng.");
                }
            }
        }

        // =========================
        // 7. KIỂM TRA PRODUCTS
        // =========================
        private static void ValidateProducts(
            List<IXLRow> rows,
            List<string> errors)
        {
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(
                    Cell(row, "Name")))
                {
                    errors.Add(
                        $"Products dòng {row.RowNumber()}: Thiếu Name.");
                }

                if (string.IsNullOrWhiteSpace(
                    Cell(row, "Category")))
                {
                    errors.Add(
                        $"Products dòng {row.RowNumber()}: Thiếu Category.");
                }

                try
                {
                    decimal? price =
                        ReadNullableDecimal(row, "Price");

                    int? quantity =
                        ReadNullableInt(row, "Quantity");

                    if (price.HasValue &&
                        (price.Value < 1000 ||
                         price.Value > 100000000))
                    {
                        errors.Add(
                            $"Products dòng {row.RowNumber()}: Price ngoài khoảng cho phép.");
                    }

                    if (quantity.HasValue &&
                        (quantity.Value < 1 ||
                         quantity.Value > 10000))
                    {
                        errors.Add(
                            $"Products dòng {row.RowNumber()}: Quantity ngoài khoảng cho phép.");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }

        // =========================
        // 8. KIỂM TRA PERMISSIONS
        // =========================
        private static void ValidatePermissions(
            List<IXLRow> rows,
            List<string> errors)
        {
            var keys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                string controller = Cell(row, "Controller");
                string action = Cell(row, "Action");

                if (string.IsNullOrWhiteSpace(controller) ||
                    string.IsNullOrWhiteSpace(action))
                {
                    errors.Add(
                        $"Permissions dòng {row.RowNumber()}: Thiếu Controller hoặc Action.");

                    continue;
                }

                if (!keys.Add(controller + "/" + action))
                {
                    errors.Add(
                        $"Permissions dòng {row.RowNumber()}: Quyền bị trùng.");
                }
            }
        }

        // =========================
        // 9. KIỂM TRA ROLE PERMISSIONS
        // =========================
        private static void ValidateRolePermissions(
            List<IXLRow> rows,
            List<string> errors)
        {
            var keys = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                string role = Cell(row, "RoleName");
                string controller = Cell(row, "Controller");
                string action = Cell(row, "Action");

                if (string.IsNullOrWhiteSpace(role) ||
                    string.IsNullOrWhiteSpace(controller) ||
                    string.IsNullOrWhiteSpace(action))
                {
                    errors.Add(
                        $"RolePermissions dòng {row.RowNumber()}: Thiếu dữ liệu.");

                    continue;
                }

                string key =
                    role + "/" + controller + "/" + action;

                if (!keys.Add(key))
                {
                    errors.Add(
                        $"RolePermissions dòng {row.RowNumber()}: Quyền gán bị trùng.");
                }
            }
        }

        // =========================
        // 10. KIỂM TRA DỮ LIỆU SQL SERVER
        // =========================
        private async Task ValidateDatabaseAsync(
            List<IXLRow> faculties,
            List<IXLRow> students,
            List<IXLRow> permissions,
            List<IXLRow> rolePermissions,
            List<string> errors)
        {
            var facultyNames = faculties
                .Select(x => Cell(x, "Name"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in students)
            {
                string facultyName =
                    Cell(row, "FacultyName");

                if (!facultyNames.Contains(facultyName))
                {
                    bool exists = await _context.Faculties
                        .AnyAsync(x => x.Name == facultyName);

                    if (!exists)
                    {
                        errors.Add(
                            $"Students dòng {row.RowNumber()}: Không tìm thấy khoa '{facultyName}'.");
                    }
                }
            }

            var permissionKeys = permissions
                .Select(x =>
                    Cell(x, "Controller") + "/" +
                    Cell(x, "Action"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rolePermissions)
            {
                string roleName = Cell(row, "RoleName");

                string key =
                    Cell(row, "Controller") + "/" +
                    Cell(row, "Action");

                var role = await _roleManager
                    .FindByNameAsync(roleName);

                if (role == null)
                {
                    errors.Add(
                        $"RolePermissions dòng {row.RowNumber()}: Không tồn tại Role '{roleName}'.");
                }

                if (!permissionKeys.Contains(key))
                {
                    string controller =
                        Cell(row, "Controller");

                    string action =
                        Cell(row, "Action");

                    bool exists = await _context.Permissions
                        .AnyAsync(x =>
                            x.Controller == controller &&
                            x.Action == action);

                    if (!exists)
                    {
                        errors.Add(
                            $"RolePermissions dòng {row.RowNumber()}: Không tồn tại quyền '{key}'.");
                    }
                }
            }
        }
    }
}
