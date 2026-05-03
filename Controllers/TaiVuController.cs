using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace QLBenhVien.Controllers
{
    [Authorize(Policy = "TaiVuPolicy")]
    public class TaiVuController : Controller
    {
        private readonly string _qlBenhVienConn;

        public TaiVuController(IConfiguration configuration)
        {
            _qlBenhVienConn = configuration.GetConnectionString("QLBenhVienDB");
        }

        // ==================== DANH SÁCH CHỜ THU TIỀN ====================
        [HttpGet]
        public async Task<IActionResult> DanhSachChoThuTien()
        {
            using var conn = new SqlConnection(_qlBenhVienConn);
            var list = await conn.QueryAsync<dynamic>("SELECT * FROM vw_DanhSachChoThuTien");
            return View(list);
        }

        // ==================== BƯỚC 1: TẠO HÓA ĐƠN ====================
        [HttpPost]
        public async Task<IActionResult> TaoHoaDon(int maKhamBenh)
        {
            using var conn = new SqlConnection(_qlBenhVienConn);
            try
            {
                var result = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_TaiVu_TaoHoaDon", new { maKhamBenh },
                    commandType: CommandType.StoredProcedure);

                TempData["Message"] = $"Đã tạo hóa đơn #{result.MaHoaDonMoi} thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("DanhSachChoThuTien");
        }

        // ==================== BƯỚC 2: XÁC NHẬN THANH TOÁN ====================
        [HttpGet]
        public async Task<IActionResult> XacNhanThanhToan(int maKhamBenh)
        {
            using var conn = new SqlConnection(_qlBenhVienConn);

            // Gọi SP để lấy maHoaDon (không SELECT trực tiếp)
            var hoaDon = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "sp_GetLatestHoaDon", new { maKhamBenh },
                commandType: CommandType.StoredProcedure);

            if (hoaDon == null)
            {
                TempData["Error"] = "Không tìm thấy hóa đơn!";
                return RedirectToAction("DanhSachChoThuTien");
            }

            var chiTiet = await conn.QueryAsync<dynamic>(
                "sp_view_HoaDon_ChiTiet_GiaiMa",
                new { maHoaDon = hoaDon.maHoaDon },
                commandType: CommandType.StoredProcedure);

            return View(chiTiet);
        }

        [HttpPost]
        public async Task<IActionResult> XacNhanThanhToan(int maHoaDon, decimal soTienNhan, decimal soTienThoi)
        {
            using var conn = new SqlConnection(_qlBenhVienConn);
            try
            {
                await conn.ExecuteAsync(
                    "sp_TaiVu_XacNhanThanhToan",
                    new { maHoaDon, soTienNhan, soTienThoi },
                    commandType: CommandType.StoredProcedure);

                TempData["Message"] = "Thanh toán thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("DanhSachChoThuTien");
        }
    }
}