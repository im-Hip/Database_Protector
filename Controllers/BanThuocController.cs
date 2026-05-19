using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using QLBenhVien.Services;
using Microsoft.AspNetCore.Authorization;
using QLBenhVien.Models;

namespace QLBenhVien.Controllers
{
    [Authorize(Policy = "BanThuocPolicy")]   // Thêm policy này trong Program.cs
    public class BanThuocController : Controller
    {
        private readonly string _connStr;
        private readonly KeyVaultService _keyVault;

        public BanThuocController(IConfiguration config, KeyVaultService keyVault)
        {
            _connStr = config.GetConnectionString("QLBenhVienDB");
            _keyVault = keyVault;
        }

        // ==================== DANH SÁCH TOA THUỐC ====================
        public async Task<IActionResult> Index(DateTime? tuNgay, DateTime? denNgay)
        {
            using var connection = new SqlConnection(_connStr);

            var danhSach = await connection.QueryAsync<dynamic>(
                "sp_LayDanhSachToaThuoc",
                new { tuNgay, denNgay },
                commandType: CommandType.StoredProcedure);

            return View(danhSach);
        }

        // ==================== CHI TIẾT TOA THUỐC + GIẢI MÃ ====================
        public async Task<IActionResult> ChiTietToaThuoc(int maToaThuoc)
        {
            using var connection = new SqlConnection(_connStr);

            var thongTin = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "sp_LayMaBacSiTuToaThuoc",
                new { maToaThuoc },
                commandType: CommandType.StoredProcedure);

            if (thongTin == null)
            {
                TempData["Error"] = "Không tìm thấy toa thuốc.";
                return RedirectToAction("Index");
            }

            string maBacSiKham = thongTin.maBacSi;

            var symKey = await _keyVault.GetSecretAsync($"{maBacSiKham}-key");
            var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
            var certPass = await _keyVault.GetSecretAsync("PassCertBS");

            var chiTiet = await connection.QueryAsync<dynamic>(
                "sp_LayChiTietToaThuoc",
                new 
                { 
                    maToaThuoc, 
                    SymKeyName = symKey, 
                    CertName = certName, 
                    CertPassword = certPass 
                },
                commandType: CommandType.StoredProcedure);

            var dsThuoc = await connection.QueryAsync<dynamic>(
                "sp_LayDanhSachThuocBan",
                commandType: CommandType.StoredProcedure);

            ViewBag.MaToaThuoc = maToaThuoc;
            ViewBag.DanhSachThuoc = dsThuoc;

            return View(chiTiet);
        }

        [HttpPost]
        public async Task<IActionResult> TaoHoaDon(TaoHoaDonThuocViewModel model)
        {
            if (model.DanhSachBoSung == null || !model.DanhSachBoSung.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một thuốc để bán.";
                return RedirectToAction("ChiTietToaThuoc", new { maToaThuoc = model.MaToaThuoc });
            }

            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;

            if (string.IsNullOrEmpty(maNhanVien))
            {
                TempData["Error"] = "Không xác định được nhân viên bán thuốc.";
                return RedirectToAction("Index");
            }

            using var connection = new SqlConnection(_connStr);

            var param = new DynamicParameters();
            param.Add("maToaThuoc", model.MaToaThuoc);
            param.Add("nhanVienThu", maNhanVien);
            param.Add("maHoaDon", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "sp_TaoHoaDonThuoc",
                param,
                commandType: CommandType.StoredProcedure);

            int maHoaDon = param.Get<int>("maHoaDon");

            foreach (var item in model.DanhSachBoSung)
            {
                await connection.ExecuteAsync(
                    "sp_ThemChiTietHoaDonThuoc",
                    new
                    {
                        maHoaDon,
                        maThuoc = item.MaThuoc,
                        soLuong = item.SoLuong
                    },
                    commandType: CommandType.StoredProcedure);
            }

            TempData["Success"] = $"Đã tạo hóa đơn thuốc #{maHoaDon} thành công.";
            return RedirectToAction("ThanhToan", new { maHoaDon });
        }

        public async Task<IActionResult> ThanhToan(int maHoaDon)
        {
            using var connection = new SqlConnection(_connStr);

            var chiTiet = await connection.QueryAsync<dynamic>(
                "sp_LayChiTietThanhToanThuoc",
                new { maHoaDon },
                commandType: CommandType.StoredProcedure);

            ViewBag.MaHoaDon = maHoaDon;
            ViewBag.TongTien = chiTiet.Sum(x => (decimal)x.ThanhTien);

            return View(chiTiet);
        }

        [HttpPost]
        public async Task<IActionResult> XacNhanThanhToan(int maHoaDon, decimal soTienNhan)
        {
            using var connection = new SqlConnection(_connStr);
        
            await connection.ExecuteAsync(
                "sp_XacNhanThanhToanThuoc",
                new
                {
                    maHoaDon,
                    soTienNhan
                },
                commandType: CommandType.StoredProcedure);
        
            TempData["Success"] = "Thanh toán hóa đơn thuốc thành công.";
            return RedirectToAction("Index");
        }
    }
}