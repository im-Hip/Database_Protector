using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using QLBenhVien.Models;                    
using Microsoft.Extensions.Configuration;
using Dapper;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace QLBenhVien.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _accountConn;

        public AccountController(IConfiguration configuration)
        {
            _accountConn = configuration.GetConnectionString("AccountDB")
                ?? throw new InvalidOperationException("Không tìm thấy ConnectionString 'AccountDB'");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login() => View();

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using var connection = new SqlConnection(_accountConn);

            // Hash mật khẩu (giữ nguyên cách bạn đang dùng)
            byte[] passwordHash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                passwordHash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(model.Password ?? ""));
            }

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "sp_Login_CheckAccount",
                new { username = model.Username, passwordHash },
                commandType: CommandType.StoredProcedure);

            if (result == null)
            {
                ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
                return View(model);
            }

            if (result.IsActive == false)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            // === Đăng nhập thành công ===
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, result.username ?? ""),
                new Claim("MaNhanVien", result.maNhanVien ?? ""),
                new Claim("TypeID", result.typeID ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            // Redirect theo typeID
            return result.typeID switch
            {
                "0" => RedirectToAction("QuanLyQuyen", "Admin"),
                "2" => RedirectToAction("DanhSachBenhNhan", "DieuPhoi"),
                "3" => RedirectToAction("DanhSachChoThuTien", "TaiVu"),      
                "1" => RedirectToAction("Index", "BacSi"),
                _   => RedirectToAction("Index", "Home")
            };
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}