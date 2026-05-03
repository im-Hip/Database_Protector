using System.ComponentModel.DataAnnotations;

namespace QLBenhVien.Models
{
    public class BenhNhan
    {
        public string? maBenhNhan { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string hoTen { get; set; } = string.Empty;

        public string? gioiTinh { get; set; }

        public float? chieuCao { get; set; }

        public float? canNang { get; set; }

        public int? namSinh { get; set; }

        public string? diaChi { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? soDienThoai { get; set; }

        public DateTime? ngayTao { get; set; }
    }
}