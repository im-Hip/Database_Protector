using System.Security.Cryptography;
using System.Text;

namespace QLBenhVien.Helpers 
{
    public static class SecurityHelper
    {
        // Khóa bí mật (Key) - TRONG THỰC TẾ NÊN LƯU Ở appsettings.json
        private static readonly string _aesKey = "BenhVienSecretKey123456789012345"; 

        public static byte[] MaHoaAES(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return null;

            byte[] keyBytes = Encoding.UTF8.GetBytes(_aesKey);
            Array.Resize(ref keyBytes, 32); // AES-256 cần 32 bytes

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = new byte[16]; // Initialization Vector 16 bytes

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                return encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            }
        }
    }
}