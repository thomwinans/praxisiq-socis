namespace Snapp.Shared.Interfaces;

/// <summary>
/// Abstracts field-level PII encryption. Implementations use AES-256-GCM
/// envelope encryption — KMS master key in prod, local file key in dev.
/// </summary>
public interface IFieldEncryptor
{
    /// <summary>
    /// Encrypts a plaintext string and returns the Base64-encoded ciphertext.
    /// Uses the current default encryption key.
    /// </summary>
    Task<string> EncryptAsync(string plaintext);

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext string back to plaintext.
    /// Determines the correct key from the ciphertext envelope.
    /// </summary>
    Task<string> DecryptAsync(string ciphertext);

    /// <summary>
    /// Encrypts a plaintext string and returns both the Base64-encoded ciphertext
    /// and the key ID used, for storage alongside the encrypted value.
    /// </summary>
    Task<(string Encrypted, string KeyId)> EncryptWithKeyIdAsync(string plaintext);
}
