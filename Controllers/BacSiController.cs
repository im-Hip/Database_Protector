using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using QLBenhVien.Models;   // nếu có ViewModel

namespace QLBenhVien.Controllers
{
    [Authorize(Policy = "BacSiPolicy")]
    public class BacSiController : Controller
    {
        private readonly string _connStr;

        public BacSiController(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("QLBenhVienDB")
                ?? throw new InvalidOperationException("Không tìm thấy ConnectionString 'QLBenhVienDB' trong cấu hình.");
        }

        // ==================== TRANG CHÍNH - LỊCH LÀM VIỆC ====================
        public async Task<IActionResult> Index(DateTime? ngay = null)
        {
            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;
            if (string.IsNullOrEmpty(maNhanVien))
                return RedirectToAction("Login", "Account");

            using var connection = new SqlConnection(_connStr);

            var lichLamViec = await connection.QueryAsync<dynamic>(
                "sp_GetLichLamViecBacSi",
                new { maNhanVien, ngay = ngay?.Date },
                commandType: CommandType.StoredProcedure);

            ViewBag.NgayHienTai = ngay?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
            return View(lichLamViec);
        }

        // ==================== GỌI BỆNH NHÂN VÀO KHÁM ====================
        public async Task<IActionResult> GoiBenhNhan(string maPhongKham)
        {
            if (string.IsNullOrEmpty(maPhongKham))
                return RedirectToAction("Index");

            using var connection = new SqlConnection(_connStr);

            var danhSachBenhNhan = await connection.QueryAsync<dynamic>(
                "sp_LayBNDauTienCuaPhong",
                new { maPhongKham },
                commandType: CommandType.StoredProcedure);

            ViewBag.MaPhongKham = maPhongKham;
            return View(danhSachBenhNhan);
        }

        // (Tùy chọn) Action cập nhật tình trạng khi bác sĩ gọi bệnh nhân
        [HttpPost]
        public async Task<IActionResult> GoiKham(int maKhamBenh, string maBenhNhan, string maPhongKham)
        {
            using var connection = new SqlConnection(_connStr);

            // Cập nhật tình trạng = '2' (đang khám)
            await connection.ExecuteAsync(@"
                UPDATE DANHSACH_BENHNHAN 
                SET tinhTrang = '2' 
                WHERE maKhamBenh = @maKhamBenh 
                  AND maBenhNhan = @maBenhNhan 
                  AND maPhongKham = @maPhongKham",
                new { maKhamBenh, maBenhNhan, maPhongKham });

            // Có thể thêm logic tạo bản ghi KHAMBENH ở đây nếu cần

            TempData["Success"] = "Đã gọi bệnh nhân vào khám thành công!";
            return RedirectToAction("GoiBenhNhan", new { maPhongKham });
        }
    }
}