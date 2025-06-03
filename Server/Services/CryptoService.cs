using System.Security.Cryptography;
using System.Text;

namespace Server.Services
{
    public class CryptoService
    {
        private readonly ILogger<CryptoService> _logger;
        private readonly Dictionary<string, byte[]> _sessionKeys = new();

        public CryptoService(ILogger<CryptoService> logger)
        {
            _logger = logger;
        }

        public (string publicKey, string privateKey) GenerateKeyPair()
        {
            using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var publicKey = Convert.ToBase64String(ecdh.PublicKey.ExportSubjectPublicKeyInfo());
            var privateKey = Convert.ToBase64String(ecdh.ExportPkcs8PrivateKey());
            return (publicKey, privateKey);
        }

        public string DeriveSharedSecret(string sessionId, string privateKey, string peerPublicKey)
        {
            try
            {
                using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
                ecdh.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);

                var peerKey = ECDiffieHellman.Create();
                peerKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(peerPublicKey), out _);

                var sharedSecret = ecdh.DeriveKeyMaterial(peerKey.PublicKey);
                var sessionKey = DeriveSessionKey(sharedSecret);
                
                _sessionKeys[sessionId] = sessionKey;
                return Convert.ToBase64String(sessionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deriving shared secret");
                throw;
            }
        }

        public byte[] EncryptMessage(string sessionId, byte[] data)
        {
            if (!_sessionKeys.TryGetValue(sessionId, out var key))
                throw new KeyNotFoundException("Session key not found");

            using var aes = new AesGcm(key, 16);
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = new byte[data.Length];
            var tag = new byte[16];

            aes.Encrypt(nonce, data, ciphertext, tag);

            var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

            return result;
        }

        public byte[] DecryptMessage(string sessionId, byte[] encryptedData)
        {
            if (!_sessionKeys.TryGetValue(sessionId, out var key))
                throw new KeyNotFoundException("Session key not found");

            using var aes = new AesGcm(key, 16);
            var nonce = new byte[12];
            var tag = new byte[16];
            var ciphertext = new byte[encryptedData.Length - nonce.Length - tag.Length];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(encryptedData, nonce.Length, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(encryptedData, nonce.Length + ciphertext.Length, tag, 0, tag.Length);

            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }

        private byte[] DeriveSessionKey(byte[] sharedSecret)
        {
            using var hkdf = new HKDF();
            return hkdf.Expand(sharedSecret, 32, "WebRTC-Session-Key");
        }

        public void ClearSessionKey(string sessionId)
        {
            _sessionKeys.Remove(sessionId);
        }
    }

    // HKDF implementation for key derivation
    internal class HKDF : IDisposable
    {
        private readonly HMACSHA256 _hmac;
        private bool _disposed;

        public HKDF()
        {
            _hmac = new HMACSHA256();
        }

        public byte[] Expand(byte[] ikm, int length, string info)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HKDF));
            
            _hmac.Key = ikm;
            var infoBytes = Encoding.UTF8.GetBytes(info);
            var result = new byte[length];
            var counter = 1;
            var offset = 0;

            while (offset < length)
            {
                var counterBytes = BitConverter.GetBytes(counter);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(counterBytes);

                var hash = _hmac.ComputeHash(Concat(counterBytes, infoBytes));
                var copyLength = Math.Min(hash.Length, length - offset);
                Buffer.BlockCopy(hash, 0, result, offset, copyLength);
                offset += copyLength;
                counter++;
            }

            return result;
        }

        private byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _hmac.Dispose();
                _disposed = true;
            }
        }
    }
} 