using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using QLBenhVien.Models;
using Microsoft.AspNetCore.Authorization;

namespace QLBenhVien.Web.Controllers
{
    [Authorize(Policy = "TiepTanPolicy")]
    public class DieuPhoiController : Controller
    {
        private readonly string _benhVienConn;

        public DieuPhoiController(IConfiguration configuration)
        {
            _benhVienConn = configuration.GetConnectionString("QLBenhVienDB")
                ?? throw new InvalidOperationException("Không tìm thấy ConnectionString 'QLBenhVienDB'");
        }

        // ====================== DANH SÁCH + TÌM KIẾM ======================
        public async Task<IActionResult> DanhSachBenhNhan(string searchTerm = null)
        {
            using var conn = new SqlConnection(_benhVienConn);
            var ds = await conn.QueryAsync<BenhNhan>("sp_DanhSachBenhNhan", 
                new { searchTerm }, 
                commandType: CommandType.StoredProcedure);

            ViewBag.SearchTerm = searchTerm;   // để giữ lại từ khóa tìm kiếm
            return View(ds);
        }

        // ====================== THÊM ======================
        [HttpGet]
        public IActionResult ThemBenhNhan() => View();

        [HttpPost]
        public async Task<IActionResult> ThemBenhNhan(BenhNhan model)
        {
            if (!ModelState.IsValid) return View(model);

            using var conn = new SqlConnection(_benhVienConn);
            await conn.ExecuteAsync("sp_ThemBenhNhan",
                new 
                { 
                    hoTen = model.hoTen,
                    gioiTinh = model.gioiTinh,
                    chieuCao = model.chieuCao,
                    canNang = model.canNang,
                    namSinh = model.namSinh,
                    diaChi = model.diaChi,
                    soDienThoai = model.soDienThoai
                },
                commandType: CommandType.StoredProcedure);

            TempData["Success"] = "Thêm bệnh nhân thành công!";
            return RedirectToAction("DanhSachBenhNhan");
        }

        // ====================== SỬA ======================
        [HttpGet]
        public async Task<IActionResult> SuaBenhNhan(string id)
        {
            using var conn = new SqlConnection(_benhVienConn);
            var bn = await conn.QueryFirstOrDefaultAsync<BenhNhan>(
                "SELECT * FROM vw_BenhNhan WHERE maBenhNhan = @id", new { id });

            if (bn == null) return NotFound();
            return View(bn);
        }

        [HttpPost]
        public async Task<IActionResult> SuaBenhNhan(BenhNhan model)
        {
            using var conn = new SqlConnection(_benhVienConn);
        
            await conn.ExecuteAsync("sp_SuaBenhNhan",
                new 
                { 
                    maBenhNhan = model.maBenhNhan,
                    hoTen      = model.hoTen,
                    gioiTinh   = model.gioiTinh,
                    chieuCao   = model.chieuCao,
                    canNang    = model.canNang,
                    namSinh    = model.namSinh,
                    diaChi     = model.diaChi,
                    soDienThoai = model.soDienThoai
                },
                commandType: CommandType.StoredProcedure);
        
            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction("DanhSachBenhNhan");
        }

        // ====================== ĐIỀU PHỐI ======================
        [HttpGet]
        public async Task<IActionResult> DieuPhoi()
        {
            using var conn = new SqlConnection(_benhVienConn);

            // Dùng lại view vw_BenhNhan bạn đã có
            var benhNhanList = await conn.QueryAsync<dynamic>(
                "SELECT maBenhNhan, hoTen + ' (' + maBenhNhan + ')' AS TenHienThi FROM vw_BenhNhan ORDER BY hoTen");

            // View phòng khám (giữ nguyên)
            var phongKhamList = await conn.QueryAsync<dynamic>(
                "SELECT maPhongKham, tenPhongKham FROM vw_DanhSachPhongKham_DieuPhoi");

            ViewBag.BenhNhanList = benhNhanList;
            ViewBag.PhongKhamList = phongKhamList;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DieuPhoi(string maBenhNhan, string maPhongKham)
        {
            using var conn = new SqlConnection(_benhVienConn);

            try
            {
                var result = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_DieuPhoi_TaoPhienKham",
                    new { maBenhNhan, maPhongKham },
                    commandType: CommandType.StoredProcedure);

                if (result != null)
                {
                    string msg = $"Đã tạo phiên khám #{result.MaKhamBenhMoi} thành công!";
                    if (result.MaBacSiDaGan != null)
                    {
                        msg += $" Đã tự gán bác sĩ (maNV): {result.MaBacSiDaGan}.";
                    }
                    else
                    {
                        msg += " (Chưa có bác sĩ được gán tự động).";
                    }
                    msg += " Đã tự động tạo chi tiết khám bệnh (lấy maKhoa & maDichVu từ phòng khám).";
                    TempData["Message"] = msg;
                }
                else
                {
                    TempData["Message"] = "Đã tạo phiên khám thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("DieuPhoi");
        }
    }
}