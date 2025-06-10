using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class TokenStorage
{
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteApp", 
        "token.dat"
    );

    public static void SaveToken(string token)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

            var dir = Path.GetDirectoryName(TokenFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(TokenFilePath, encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi lưu token: " + ex.Message);
        }
    }

    public static string LoadToken()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
                return null;

            var encrypted = File.ReadAllBytes(TokenFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi đọc token: " + ex.Message);
            return null;
        }
    }
    public static bool HasToken()
    {
        return File.Exists(TokenFilePath);
    }

    public static void ClearToken()
    {
        try
        {
            if (File.Exists(TokenFilePath))
                File.Delete(TokenFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi xóa token: " + ex.Message);
        }
    }
}
