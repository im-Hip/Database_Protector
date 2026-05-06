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
        
        public async Task<IActionResult> KhamBenh(int maKhamBenh, string maPhongKham)
        {
            using var connection = new SqlConnection(_connStr);
        
            var benhNhan = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "sp_LayThongTinBenhNhan",
                new { maKhamBenh },
                commandType: CommandType.StoredProcedure);
        
            if (benhNhan == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin bệnh nhân.";
                return RedirectToAction("GoiBenhNhan", new { maPhongKham });
            }
        
            // Truyền maPhongKham sang View
            ViewBag.MaPhongKham = maPhongKham;
        
            return View(benhNhan);
        }

        // Hiển thị form chọn phòng xét nghiệm
        public async Task<IActionResult> ChonXetNghiem(int maKhamBenh, string maBenhNhan, string maPhongKham)
        {
            using var connection = new SqlConnection(_connStr);

            var phongList = await connection.QueryAsync<dynamic>(
                "SELECT * FROM vw_PhongXetNghiem ORDER BY tenPhongKham");

            ViewBag.PhongList = phongList;
            ViewBag.MaKhamBenh = maKhamBenh;
            ViewBag.MaBenhNhan = maBenhNhan;
            ViewBag.MaPhongKham = maPhongKham;

            return View();
        }
        
        // Lưu yêu cầu xét nghiệm
        [HttpPost]
        public async Task<IActionResult> LuuXetNghiem(int maKhamBenh, List<string> maPhongKham, string maPhongKhamGoc)
        {
            if (maPhongKham == null || maPhongKham.Count == 0)
            {
                TempData["Error"] = "Bạn chưa chọn phòng nào!";
                return RedirectToAction("ChonXetNghiem", new { maKhamBenh });
            }
        
            var maBacSiYeuCau = User.FindFirst("MaNhanVien")?.Value;
        
            if (string.IsNullOrEmpty(maBacSiYeuCau))
            {
                TempData["Error"] = "Không xác định được bác sĩ.";
                return RedirectToAction("ChonXetNghiem", new { maKhamBenh });
            }
        
            using var connection = new SqlConnection(_connStr);
        
            foreach (var phong in maPhongKham)
            {
                await connection.ExecuteAsync(
                    "sp_ThemChiTietKhamBenh_XetNghiem",
                    new { maKhamBenh, maBacSiYeuCau, maPhongKham = phong },
                    commandType: CommandType.StoredProcedure);
            }
        
            TempData["Success"] = $"Đã gửi {maPhongKham.Count} yêu cầu xét nghiệm thành công!";
        
            // Quay lại trang Gọi bệnh nhân
            return RedirectToAction("GoiBenhNhan", new { maPhongKham = maPhongKhamGoc });
        }
    }
}   