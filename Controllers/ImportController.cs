
using ClosedXML.Excel;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ImportController : Controller
    {
        private readonly ExcelImportService _excelImportService;
        private readonly IWebHostEnvironment _environment;

        private const long MaxFileSize = 10 * 1024 * 1024;

        public ImportController(
            ExcelImportService excelImportService,
            IWebHostEnvironment environment)
        {
            _excelImportService = excelImportService;
            _environment = environment;
        }

        // Trang Import Excel
        [HttpGet]
        public IActionResult Index()
        {
            return View(new ImportViewModel());
        }

        // Tải file Excel mẫu
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();

            // Sheet Faculties
            var faculties = workbook.Worksheets.Add("Faculties");

            faculties.Cell(1, 1).Value = "Name";
            faculties.Cell(2, 1).Value = "Công nghệ thông tin";
            faculties.Cell(3, 1).Value = "Kinh tế";

            // Sheet Students
            var students = workbook.Worksheets.Add("Students");

            students.Cell(1, 1).Value = "StudentCode";
            students.Cell(1, 2).Value = "FullName";
            students.Cell(1, 3).Value = "Address";
            students.Cell(1, 4).Value = "University";
            students.Cell(1, 5).Value = "FacultyName";

            students.Cell(2, 1).Value = "SV001";
            students.Cell(2, 2).Value = "Nguyễn Văn A";
            students.Cell(2, 3).Value = "Hà Nội";
            students.Cell(2, 4).Value = "Đại học ABC";
            students.Cell(2, 5).Value = "Công nghệ thông tin";

            // Sheet Products
            var products = workbook.Worksheets.Add("Products");

            products.Cell(1, 1).Value = "Name";
            products.Cell(1, 2).Value = "Price";
            products.Cell(1, 3).Value = "Quantity";
            products.Cell(1, 4).Value = "Category";

            products.Cell(2, 1).Value = "Laptop";
            products.Cell(2, 2).Value = 15000000;
            products.Cell(2, 3).Value = 10;
            products.Cell(2, 4).Value = "Máy tính";

            // Sheet Permissions
            var permissions = workbook.Worksheets.Add("Permissions");

            permissions.Cell(1, 1).Value = "Controller";
            permissions.Cell(1, 2).Value = "Action";
            permissions.Cell(1, 3).Value = "DisplayName";

            permissions.Cell(2, 1).Value = "Product";
            permissions.Cell(2, 2).Value = "Index";
            permissions.Cell(2, 3).Value = "Xem sản phẩm";

            // Sheet RolePermissions
            var rolePermissions = workbook.Worksheets.Add("RolePermissions");

            rolePermissions.Cell(1, 1).Value = "RoleName";
            rolePermissions.Cell(1, 2).Value = "Controller";
            rolePermissions.Cell(1, 3).Value = "Action";

            rolePermissions.Cell(2, 1).Value = "User";
            rolePermissions.Cell(2, 2).Value = "Product";
            rolePermissions.Cell(2, 3).Value = "Index";

            // Định dạng các sheet
            foreach (var sheet in workbook.Worksheets)
            {
                var header = sheet.Row(1);

                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightBlue;

                sheet.Columns().AdjustToContents();
                sheet.SheetView.FreezeRows(1);
            }

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DemoMVC_Import_Template.xlsx");
        }

        // Xem trước và kiểm tra dữ liệu Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(ImportViewModel model)
        {
            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
            {
                model.Message = "Vui lòng chọn file Excel.";
                return View("Index", model);
            }

            if (model.ExcelFile.Length > MaxFileSize)
            {
                model.Message = "File Excel không được vượt quá 10 MB.";
                return View("Index", model);
            }

            var extension = Path.GetExtension(model.ExcelFile.FileName);

            if (!string.Equals(
                extension,
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
            {
                model.Message = "Chỉ hỗ trợ file Excel định dạng .xlsx.";
                return View("Index", model);
            }

            string? filePath = null;

            try
            {
                string folder = Path.Combine(
                    _environment.ContentRootPath,
                    "App_Data",
                    "ExcelImports");

                Directory.CreateDirectory(folder);

                string token = Guid.NewGuid().ToString("N");

                filePath = Path.Combine(folder, token + ".xlsx");

                // Lưu file Excel vào thư mục tạm
                using (var fileStream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await model.ExcelFile.CopyToAsync(fileStream);
                }

                // Đọc và kiểm tra dữ liệu
                ImportViewModel result;

                using (var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    result = await _excelImportService.PreviewAsync(stream);
                }

                result.PreviewToken = token;
                result.ImportMode = model.ImportMode;

                return View("Index", result);
            }
            catch (Exception ex)
            {
                // Nếu xem trước thất bại thì xóa file tạm
                if (filePath != null && System.IO.File.Exists(filePath))
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                    }
                    catch
                    {
                        // Bỏ qua lỗi xóa file tạm
                    }
                }

                model.Message = "Không thể xử lý file: " + ex.Message;

                return View("Index", model);
            }
        }

        // Xác nhận nhập dữ liệu Excel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(ImportViewModel model)
        {
            ModelState.Remove(nameof(model.ExcelFile));

            if (string.IsNullOrWhiteSpace(model.PreviewToken) ||
                !Guid.TryParseExact(model.PreviewToken, "N", out _))
            {
                model.Message =
                    "Phiên xem trước không hợp lệ. Vui lòng tải file lại.";

                return View("Index", model);
            }

            string folder = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "ExcelImports");

            string filePath = Path.Combine(
                folder,
                model.PreviewToken + ".xlsx");

            if (!System.IO.File.Exists(filePath))
            {
                model.Message =
                    "File xem trước đã hết hạn hoặc không tồn tại. Vui lòng tải file lại.";

                return View("Index", model);
            }

            try
            {
                ImportViewModel result;

                // Đọc và nhập dữ liệu Excel
                using (var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    result = await _excelImportService.ImportAsync(
                        stream,
                        model.ImportMode,
                        model.ConfirmOverwrite);
                }

                // QUAN TRỌNG:
                // FileStream đã đóng sau khi thoát khỏi using.
                // Lúc này mới được phép xóa file.
                if (!result.IsPreview)
                {
                    System.IO.File.Delete(filePath);

                    result.PreviewToken = null;
                }
                else
                {
                    result.PreviewToken = model.PreviewToken;
                }

                result.ImportMode = model.ImportMode;

                return View("Index", result);
            }
            catch (Exception ex)
            {
                model.Message = "Nhập dữ liệu thất bại: " + ex.Message;

                return View("Index", model);
            }
        }
    }
}
