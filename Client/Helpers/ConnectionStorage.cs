using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class ConnectionStorage
{
    private static readonly string ConnectionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteApp",
        "ConnectionId.dat"
    );

    public static void SaveConnectionId(string connectionId)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(connectionId);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

            var dir = Path.GetDirectoryName(ConnectionFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(ConnectionFilePath, encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi lưu ConnectionId: " + ex.Message);
        }
    }

    public static string LoadConnectionId()
    {
        try
        {
            if (!File.Exists(ConnectionFilePath))
                return null;

            var encrypted = File.ReadAllBytes(ConnectionFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi đọc ConnectionId: " + ex.Message);
            return null;
        }
    }

    public static void ClearConnectionId()
    {
        try
        {
            if (File.Exists(ConnectionFilePath))
                File.Delete(ConnectionFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi khi xóa ConnectionId: " + ex.Message);
        }
    }
}
