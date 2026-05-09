public class ToaThuocViewModel
{
    public int MaKhamBenh { get; set; }
    public string TenBenhNhan { get; set; }          // Hiển thị
    public List<ChiTietToaThuocViewModel> DanhSachThuoc { get; set; } = new();
}

public class ChiTietToaThuocViewModel
{
    public string TenThuoc { get; set; }
    public int SoLuong { get; set; }
    public string LieuDung { get; set; }
    public string GhiChu { get; set; }
}