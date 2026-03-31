using System.Security.Cryptography;
using Snapp.Shared.Interfaces;

namespace Snapp.Service.LinkedIn.Services;

public class LocalFileEncryptor : IFieldEncryptor
{
    private readonly byte[] _masterKey;
    private const string KeyId = "local-dev-key";

    public LocalFileEncryptor(IConfiguration configuration)
    {
        var keyPath = configuration["Encryption:KeyFilePath"]
            ?? throw new InvalidOperationException("Encryption:KeyFilePath is not configured");
        _masterKey = File.ReadAllBytes(keyPath);
        if (_masterKey.Length != 32)
            throw new InvalidOperationException("Master key must be exactly 32 bytes (AES-256)");
    }

    public Task<string> EncryptAsync(string plaintext)
    {
        var (encrypted, _) = Encrypt(plaintext);
        return Task.FromResult(encrypted);
    }

    public Task<string> DecryptAsync(string ciphertext)
    {
        var bytes = Convert.FromBase64String(ciphertext);

        // Layout: [12-byte nonce][ciphertext][16-byte tag]
        var nonce = bytes[..12];
        var tag = bytes[^16..];
        var cipher = bytes[12..^16];

        var plainBytes = new byte[cipher.Length];
        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);

        return Task.FromResult(System.Text.Encoding.UTF8.GetString(plainBytes));
    }

    public Task<(string Encrypted, string KeyId)> EncryptWithKeyIdAsync(string plaintext)
    {
        var (encrypted, keyId) = Encrypt(plaintext);
        return Task.FromResult((encrypted, keyId));
    }

    private (string Encrypted, string KeyId) Encrypt(string plaintext)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        // Layout: [12-byte nonce][ciphertext][16-byte tag]
        var result = new byte[nonce.Length + cipher.Length + tag.Length];
        nonce.CopyTo(result, 0);
        cipher.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + cipher.Length);

        return (Convert.ToBase64String(result), KeyId);
    }
}
