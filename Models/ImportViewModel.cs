
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class ImportViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn file Excel.")]
        public IFormFile? ExcelFile { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chế độ nhập dữ liệu.")]
        public string ImportMode { get; set; } = "Append";

        public string? PreviewToken { get; set; }

        public bool ConfirmOverwrite { get; set; }

        public int TotalRows { get; set; }

        public int ValidRows { get; set; }

        public int InvalidRows { get; set; }

        public bool IsPreview { get; set; }

        public List<string> Errors { get; set; } = new();

        public string? Message { get; set; }
    }
}
