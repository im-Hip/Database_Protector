using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using QLBenhVien.Services;           // KeyVaultService
using System.Text.Json;

namespace QLBenhVien.Web.Controllers
{
    [Authorize(Policy = "AdminPolicy")]
    public class AdminController : Controller
    {
        private readonly string _accountConn;
        private readonly string _qlbvConn;           // Connection string cho QLBenhVien
        private readonly KeyVaultService _keyVaultService;

        public AdminController(IConfiguration configuration, KeyVaultService keyVaultService)
        {
            _accountConn = configuration.GetConnectionString("AccountDB");
            _qlbvConn = configuration.GetConnectionString("QLBenhVienDB");   // Thêm dòng này
            _keyVaultService = keyVaultService;
        }

        [HttpGet]
        public async Task<IActionResult> QuanLyQuyen()
        {
            using var conn = new SqlConnection(_accountConn);
        
            var accounts = await conn.QueryAsync<dynamic>(
                "SELECT * FROM vw_QuanLyQuyen_Admin ORDER BY createdDate DESC");
        
            ViewBag.Accounts = accounts;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAccount(string targetUsername, string newTypeID, bool newIsActive)
        {
            using var conn = new SqlConnection(_accountConn);

            await conn.ExecuteAsync("sp_Admin_UpdateAccount",
                new
                {
                    adminUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
                    targetUsername,
                    newTypeID,
                    newIsActive
                },
                commandType: CommandType.StoredProcedure);

            TempData["Message"] = $"Đã cập nhật typeID = {newTypeID} và Active = {newIsActive} cho tài khoản {targetUsername}";
            return RedirectToAction("QuanLyQuyen");
        }

        // ==================== TẠO KEY CHO BÁC SĨ ====================
        [HttpGet]
        public async Task<IActionResult> AddKey()
        {
            using var conn = new SqlConnection(_qlbvConn);

            var bacSisWithoutKey = (await conn.QueryAsync<dynamic>("EXEC dbo.sp_CheckKeyAccount")).ToList();

            ViewBag.UsernameList = bacSisWithoutKey
                .Select(x => (string)x.maBacSi)
                .Distinct()
                .ToList();

            ViewBag.Accounts = bacSisWithoutKey;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddKey(string maBacSi, string secretKey)
        {
            if (string.IsNullOrWhiteSpace(maBacSi) || string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return RedirectToAction("AddKey");
            }

            try
            {
                // 1. Lưu secret value lên Azure Key Vault
                string secretName = $"{maBacSi}-key";
                await _keyVaultService.SetSecretAsync(secretName, secretKey);

                // 2. Lấy tên certificate từ Azure Key Vault
                string certName = await _keyVaultService.GetSecretAsync("CertificateBacSi");

                // 3. Gọi stored procedure tạo key trong SQL Server
                using var conn = new SqlConnection(_qlbvConn);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("sp_ThucHienTaoKey", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@keyValue", secretKey);
                cmd.Parameters.AddWithValue("@BSCert", certName);
                cmd.Parameters.AddWithValue("@maBS", maBacSi);

                await cmd.ExecuteNonQueryAsync();

                TempData["Success"] = $"Đã tạo key cho {maBacSi} thành công! Secret name: {secretName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tạo key: " + ex.Message;
            }

            return RedirectToAction("AddKey");
        }
    }
}