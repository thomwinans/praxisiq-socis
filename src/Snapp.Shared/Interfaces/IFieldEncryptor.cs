namespace Snapp.Shared.Interfaces;

public interface IFieldEncryptor
{
    Task<string> EncryptAsync(string plaintext);

    Task<string> DecryptAsync(string ciphertext);

    Task<(string Encrypted, string KeyId)> EncryptWithKeyIdAsync(string plaintext);
}
