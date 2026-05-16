using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using QLBenhVien.Services;
using Microsoft.AspNetCore.Authorization;

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

            // === DÙNG SP (không SELECT trực tiếp) ===
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

            // Lấy key của BÁC SĨ
            var symKey   = await _keyVault.GetSecretAsync($"{maBacSiKham}-key");
            var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
            var certPass = await _keyVault.GetSecretAsync("PassCertBS");

            // Lấy chi tiết + giải mã
            var chiTiet = await connection.QueryAsync<dynamic>(
                "sp_LayChiTietToaThuoc",
                new { maToaThuoc, SymKeyName = symKey, CertName = certName, CertPassword = certPass },
                commandType: CommandType.StoredProcedure);

            ViewBag.MaToaThuoc = maToaThuoc;
            return View(chiTiet);
        }

        // ==================== TẠO HÓA ĐƠN (XÁC NHẬN BÁN THUỐC) ====================
        [HttpPost]
        public async Task<IActionResult> TaoHoaDon(int maToaThuoc, List<ChiTietToaThuocViewModel> DanhSachBoSung)
        {
            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;
        
            using var connection = new SqlConnection(_connStr);
        
            // 1. Tạo hóa đơn
            var param = new DynamicParameters();
            param.Add("maToaThuoc", maToaThuoc);
            param.Add("nhanVienThu", maNhanVien);
            param.Add("maHoaDon", dbType: DbType.Int32, direction: ParameterDirection.Output);
        
            await connection.ExecuteAsync("sp_TaoHoaDonTuToaThuoc", param, commandType: CommandType.StoredProcedure);
            int maHoaDon = param.Get<int>("maHoaDon");
        
            // 2. Thêm thuốc bổ sung (nếu có)
            if (DanhSachBoSung != null && DanhSachBoSung.Any())
            {
                foreach (var thuoc in DanhSachBoSung)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO CHITIET_HOADON_THUOC (maHoaDon, maThuoc, soLuong, donViTinh)
                        VALUES (@maHoaDon, NULL, @soLuong, @lieuDung)",
                        new { maHoaDon, soLuong = thuoc.SoLuong, lieuDung = thuoc.LieuDung });
                }
            }
        
            TempData["Success"] = $"Đã tạo hóa đơn #{maHoaDon} thành công!";
            return RedirectToAction("Index");
        }
    }
}