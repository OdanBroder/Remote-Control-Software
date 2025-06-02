using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using System.Security.Cryptography;
using System.Text;
using OtpNet;
using QRCoder;

namespace Server.Services
{
    public class SecurityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SecurityService> _logger;

        public SecurityService(AppDbContext context, ILogger<SecurityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> EnableTwoFactorAuth(int userId)
        {
            try
            {
                var secretKey = RandomNumberGenerator.GetBytes(20);
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                var twoFactor = new TwoFactorAuth
                {
                    UserId = userId,
                    SecretKey = Convert.ToBase64String(secretKey),
                    IsEnabled = true,
                    BackupCodes = GenerateBackupCodes()
                };

                _context.TwoFactorAuths.Add(twoFactor);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enabling 2FA for user: {userId}");
                return false;
            }
        }

        public async Task<bool> VerifyTwoFactorCode(int userId, string code)
        {
            try
            {
                var twoFactor = await _context.TwoFactorAuths
                    .FirstOrDefaultAsync(t => t.UserId == userId && t.IsEnabled);

                if (twoFactor == null) return false;

                var totp = new Totp(Convert.FromBase64String(twoFactor.SecretKey));
                var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(2, 2));

                if (isValid)
                {
                    twoFactor.LastUsed = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying 2FA code for user: {userId}");
                return false;
            }
        }

        public async Task<bool> AddIpToWhitelist(int userId, string ipAddress, string description)
        {
            try
            {
                var whitelist = new IpWhitelist
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    Description = description
                };

                _context.IpWhitelists.Add(whitelist);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding IP to whitelist for user: {userId}");
                return false;
            }
        }

        public async Task<bool> IsIpWhitelisted(int userId, string ipAddress)
        {
            try
            {
                var whitelist = await _context.IpWhitelists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.IpAddress == ipAddress);

                if (whitelist != null)
                {
                    whitelist.LastUsed = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return whitelist != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking IP whitelist for user: {userId}");
                return false;
            }
        }

        private string GenerateBackupCodes()
        {
            var codes = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(4))
                    .Replace("+", "")
                    .Replace("/", "")
                    .Substring(0, 8);
                codes.Add(code);
            }
            return string.Join(",", codes);
        }

        public string GenerateQrCode(string secretKey, string email)
        {
            var totp = new Totp(Convert.FromBase64String(secretKey));
            var provisioningUri = $"otpauth://totp/RemoteControl:{email}?secret={secretKey}&issuer=RemoteControl";
            
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(provisioningUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20);

            return Convert.ToBase64String(qrCodeImage);
        }
    }
} 