using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using QLBenhVien.Models;   
using QLBenhVien.Services;           

namespace QLBenhVien.Controllers
{
    [Authorize(Policy = "BacSiPolicy")]
    public class BacSiController : Controller
    {
        private readonly string _connStr;
        private readonly KeyVaultService _keyVault;

        public BacSiController(IConfiguration configuration, KeyVaultService keyVault)
        {
            _connStr = configuration.GetConnectionString("QLBenhVienDB")
                ?? throw new InvalidOperationException("Không tìm thấy ConnectionString 'QLBenhVienDB' trong cấu hình.");
            _keyVault = keyVault;
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
        
        public async Task<IActionResult> KhamBenh(int maKhamBenh, string maPhongKham, int id)
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
        
            // Truyền maPhongKham và id sang View
            ViewBag.MaPhongKham = maPhongKham;
            ViewBag.ID = id;
        
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

        [HttpPost]
        public async Task<IActionResult> CapNhatGhiChu(int id, string ghiChu, string maPhongKham)
        {
            if (string.IsNullOrWhiteSpace(ghiChu))
                return BadRequest("Ghi chú không được để trống.");
            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;
            if (string.IsNullOrEmpty(maNhanVien))
                return Unauthorized("Không xác định được bác sĩ.");
            // Lấy thông tin từ Azure Key Vault
            var symKey = await _keyVault.GetSecretAsync($"{maNhanVien}-key");
            var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
            var certPass = await _keyVault.GetSecretAsync("PassCertBS");

            using var connection = new SqlConnection(_connStr);

            var parameters = new
            {
                ID = id,
                ghiChuBacSiKham = ghiChu,
                SymKeyName = symKey,
                CertName = certName,
                CertPassword = certPass
            };

            await connection.ExecuteAsync("sp_CapNhatGhiChuBacSiKham", parameters, 
                commandType: CommandType.StoredProcedure);

            TempData["Success"] = "Đã lưu và mã hóa ghi chú thành công!";

            return RedirectToAction("GoiBenhNhan", new { maPhongKham = maPhongKham });
        }

        public async Task<IActionResult> DanhSachXetNghiem()
        {
            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;

            using var connection = new SqlConnection(_connStr);

            var danhSach = await connection.QueryAsync<dynamic>(
                "sp_LayDanhSachBenhNhanTrangThai2_TheoPhong",
                new { maNhanVien },
                commandType: CommandType.StoredProcedure);

            return View(danhSach);
        }

        public async Task<IActionResult> ChiTietXetNghiem(int maKhamBenh)
        {
            var maNhanVienDangNhap = User.FindFirst("MaNhanVien")?.Value;
            if (string.IsNullOrEmpty(maNhanVienDangNhap))
                return Unauthorized();

            using var connection = new SqlConnection(_connStr);

            var danhSachChiTiet = (await connection.QueryAsync<dynamic>(
                "sp_LayChiTietKhamBenh",
                new { maKhamBenh },
                commandType: CommandType.StoredProcedure)).ToList();

            if (!danhSachChiTiet.Any())
            {
                TempData["Error"] = "Không tìm thấy chi tiết khám.";
                return RedirectToAction("DanhSachXetNghiem");
            }

            // Giải mã từng dòng theo bác sĩ của dòng đó
            foreach (var ct in danhSachChiTiet)
            {
                if (ct.ghiChuBacSiKham != null)
                {
                    string doctorId = ct.maBacSiKham;

                    var symKey   = await _keyVault.GetSecretAsync($"{doctorId}-key");
                    var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
                    var certPass = await _keyVault.GetSecretAsync("PassCertBS");

                    var decrypted = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "sp_GiaiMaGhiChuBacSiKham",
                        new 
                        { 
                            ID = ct.ID, 
                            SymKeyName = symKey, 
                            CertName = certName, 
                            CertPassword = certPass 
                        },
                        commandType: CommandType.StoredProcedure);

                    ct.GhiChuGiaiMa = decrypted?.GhiChuGiaiMa;
                }
            }

            ViewBag.MaKhamBenh = maKhamBenh;
            return View(danhSachChiTiet);
        }

        [HttpGet]
        public async Task<IActionResult> KeToaThuoc(int maKhamBenh)
        {
            using var connection = new SqlConnection(_connStr);

            var benhNhan = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "sp_LayThongTinBenhNhan",
                new { maKhamBenh },
                commandType: CommandType.StoredProcedure);

            var model = new ToaThuocViewModel
            {
                MaKhamBenh = maKhamBenh,
                TenBenhNhan = benhNhan?.hoTen
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> KeToaThuoc(ToaThuocViewModel model)
        {
            if (model.DanhSachThuoc == null || !model.DanhSachThuoc.Any())
            {
                ModelState.AddModelError("", "Vui lòng thêm ít nhất 1 loại thuốc");
                return View(model);
            }

            var maNhanVienDangNhap = User.FindFirst("MaNhanVien")?.Value;
            if (string.IsNullOrEmpty(maNhanVienDangNhap))
                return Unauthorized();

            using var connection = new SqlConnection(_connStr);

            // === LẤY KEY TỪ AZURE KEY VAULT ===
            var symKey   = await _keyVault.GetSecretAsync($"{maNhanVienDangNhap}-key");
            var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
            var certPass = await _keyVault.GetSecretAsync("PassCertBS");

            // 1. Tạo toa thuốc
            var paramToa = new DynamicParameters();
            paramToa.Add("maKhamBenh", model.MaKhamBenh);
            paramToa.Add("maToaThuoc", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync("sp_TaoToaThuoc", paramToa, commandType: CommandType.StoredProcedure);
            int maToaThuoc = paramToa.Get<int>("maToaThuoc");

            // 2. Thêm từng loại thuốc (có mã hóa)
            foreach (var thuoc in model.DanhSachThuoc)
            {
                await connection.ExecuteAsync("sp_ThemChiTietToaThuoc", new
                {
                    maToaThuoc = maToaThuoc,
                    tenThuoc = thuoc.TenThuoc,
                    soLuong = thuoc.SoLuong,
                    lieuDung = thuoc.LieuDung,
                    ghiChu = thuoc.GhiChu,
                    SymKeyName = symKey,
                    CertName = certName,
                    CertPassword = certPass
                }, commandType: CommandType.StoredProcedure);
            }

            TempData["Success"] = $"Đã kê toa thuốc thành công! Mã toa: {maToaThuoc}";
            return RedirectToAction("ChiTietXetNghiem", new { maKhamBenh = model.MaKhamBenh });
        }

        // ==================== CẬP NHẬT TRIỆU CHỨNG + CHẨN ĐOÁN + NGÀY TÁI KHÁM ====================
        [HttpPost]
        public async Task<IActionResult> CapNhatChanDoan(int maKhamBenh, string trieuChung, string chanDoanCuoiCung, DateTime? ngayTaiKham)
        {
            var maNhanVien = User.FindFirst("MaNhanVien")?.Value;
            if (string.IsNullOrEmpty(maNhanVien))
                return Unauthorized();

            // Lấy key từ Azure Key Vault
            var symKey   = await _keyVault.GetSecretAsync($"{maNhanVien}-key");
            var certName = await _keyVault.GetSecretAsync("CertificateBacSi");
            var certPass = await _keyVault.GetSecretAsync("PassCertBS");

            using var connection = new SqlConnection(_connStr);

            await connection.ExecuteAsync("sp_CapNhatChanDoan", new
            {
                maKhamBenh = maKhamBenh,
                trieuChung = trieuChung ?? "",
                chanDoanCuoiCung = chanDoanCuoiCung ?? "",
                ngayTaiKham = ngayTaiKham,
                SymKeyName = symKey,
                CertName = certName,
                CertPassword = certPass
            }, commandType: CommandType.StoredProcedure);

            TempData["Success"] = "Đã cập nhật triệu chứng, chẩn đoán và ngày tái khám thành công!";
            return RedirectToAction("DanhSachXetNghiem");
        }
    }
}   