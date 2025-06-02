using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class SessionStorage
{
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteApp",
        "Session.dat"
    );

    public static void SaveSession(string Session)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(Session);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

            var dir = Path.GetDirectoryName(SessionFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(SessionFilePath, encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi lưu session: " + ex.Message);
        }
    }

    public static string LoadSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return null;

            var encrypted = File.ReadAllBytes(SessionFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi đọc session: " + ex.Message);
            return null;
        }
    }

    public static void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
                File.Delete(SessionFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi xóa session: " + ex.Message);
        }
    }
}
