using System.Collections.Generic;

namespace QLBenhVien.Models
{
    public class TaoHoaDonThuocViewModel
    {
        public int MaToaThuoc { get; set; }

        public List<BanThuocItemViewModel> DanhSachBoSung { get; set; }
            = new List<BanThuocItemViewModel>();
    }
}