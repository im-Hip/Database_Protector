using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;

namespace QLBenhVien.ViewComponents
{
    public class DoctorSidebarMenuViewComponent : ViewComponent
    {
        private readonly string _connStr;

        public DoctorSidebarMenuViewComponent(IConfiguration config)
        {
            _connStr = config.GetConnectionString("QLBenhVienDB");
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var maNhanVien = HttpContext.User.FindFirst("MaNhanVien")?.Value;
            bool showXetNghiemMenu = false;

            if (!string.IsNullOrEmpty(maNhanVien))
            {
                using var connection = new SqlConnection(_connStr);

                // Chỉ query View SQL, không viết logic phức tạp
                var exists = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM vw_BacSiCoLichPhongChinh WHERE maNhanVien = @maNhanVien",
                    new { maNhanVien });

                showXetNghiemMenu = exists > 0;
            }

            return View(new { ShowXetNghiemMenu = showXetNghiemMenu });
        }
    }
}