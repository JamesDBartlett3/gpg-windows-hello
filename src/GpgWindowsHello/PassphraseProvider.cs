using System.Security.Cryptography;
using System.Text;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace GpgWindowsHello;

/// <summary>
/// Provides passphrase management using Windows Hello authentication
/// Passphrases are stored securely using the hardware Trusted Platform Module (TPM)
/// </summary>
public class PassphraseProvider
{
    private readonly Dictionary<string, string> _cachedPassphrases = new();
    private readonly string _storageFile;
    private const string ProtectionDescriptor = "LOCAL=user";

    public PassphraseProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "GpgWindowsHello");
        Directory.CreateDirectory(appDir);
        _storageFile = Path.Combine(appDir, "passphrases.dat");
    }

    /// <summary>
    /// Gets a passphrase for the specified key ID, prompting with Windows Hello
    /// </summary>
    public async Task<string?> GetPassphraseAsync(string keyId)
    {
        // Check cache first
        if (_cachedPassphrases.TryGetValue(keyId, out var cachedPassphrase))
        {
            return cachedPassphrase;
        }

        // Authenticate with Windows Hello
        Console.WriteLine($"Windows Hello authentication required for GPG key: {keyId}");
        var authenticated = await WindowsHelloAuth.AuthenticateAsync($"Authenticate to access GPG key {keyId}");
        
        if (!authenticated)
        {
            Console.Error.WriteLine("Windows Hello authentication failed");
            return null;
        }

        // Try to load stored passphrase
        var passphrase = LoadStoredPassphrase(keyId);
        
        if (passphrase == null)
        {
            Console.WriteLine("No stored passphrase found. Please enter your GPG passphrase:");
            Console.Write("Passphrase: ");
            passphrase = ReadPassword();
            
            if (!string.IsNullOrEmpty(passphrase))
            {
                // Store the passphrase for future use
                StorePassphrase(keyId, passphrase);
            }
        }

        if (!string.IsNullOrEmpty(passphrase))
        {
            _cachedPassphrases[keyId] = passphrase;
        }

        return passphrase;
    }

    /// <summary>
    /// Clears cached passphrases
    /// </summary>
    public void ClearCache()
    {
        _cachedPassphrases.Clear();
    }

    private string? LoadStoredPassphrase(string keyId)
    {
        try
        {
            if (!File.Exists(_storageFile))
                return null;

            var encryptedData = File.ReadAllBytes(_storageFile);
            
            // Decrypt using TPM-backed DataProtectionProvider
            var decryptedData = DecryptWithTpmAsync(encryptedData).GetAwaiter().GetResult();
            if (decryptedData == null)
            {
                // Fallback to DPAPI if TPM decryption fails (backward compatibility)
                try
                {
                    decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                }
                catch
                {
                    return null;
                }
            }
            
            var json = Encoding.UTF8.GetString(decryptedData);
            
            // Simple key-value parsing
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim() == keyId)
                {
                    return parts[1].Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading stored passphrase: {ex.Message}");
        }

        return null;
    }

    private void StorePassphrase(string keyId, string passphrase)
    {
        try
        {
            // Load existing passphrases
            var passphrases = new Dictionary<string, string>();
            if (File.Exists(_storageFile))
            {
                try
                {
                    var encryptedData = File.ReadAllBytes(_storageFile);
                    var decryptedData = DecryptWithTpmAsync(encryptedData).GetAwaiter().GetResult();
                    
                    if (decryptedData == null)
                    {
                        // Try DPAPI fallback
                        try
                        {
                            decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                        }
                        catch { }
                    }
                    
                    if (decryptedData != null)
                    {
                        var json = Encoding.UTF8.GetString(decryptedData);
                        var lines = json.Split('\n');
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                passphrases[parts[0].Trim()] = parts[1].Trim();
                            }
                        }
                    }
                }
                catch { }
            }

            // Add or update the passphrase
            passphrases[keyId] = passphrase;

            // Serialize and encrypt with TPM
            var sb = new StringBuilder();
            foreach (var kvp in passphrases)
            {
                sb.AppendLine($"{kvp.Key}={kvp.Value}");
            }

            var data = Encoding.UTF8.GetBytes(sb.ToString());
            var encryptedResult = EncryptWithTpmAsync(data).GetAwaiter().GetResult();
            
            if (encryptedResult != null)
            {
                File.WriteAllBytes(_storageFile, encryptedResult);
                Console.WriteLine("Passphrase stored securely using hardware TPM.");
            }
            else
            {
                // Fallback to DPAPI if TPM encryption fails
                Console.WriteLine("TPM not available, using DPAPI fallback.");
                encryptedResult = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_storageFile, encryptedResult);
                Console.WriteLine("Passphrase stored securely using DPAPI.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error storing passphrase: {ex.Message}");
        }
    }

    /// <summary>
    /// Encrypts data using TPM-backed DataProtectionProvider
    /// </summary>
    private async Task<byte[]?> EncryptWithTpmAsync(byte[] data)
    {
        try
        {
            var provider = new DataProtectionProvider(ProtectionDescriptor);
            
            // Convert byte array to IBuffer
            var buffer = CryptographicBuffer.CreateFromByteArray(data);
            
            // Encrypt
            var protectedBuffer = await provider.ProtectAsync(buffer);
            
            // Convert back to byte array
            byte[] result;
            CryptographicBuffer.CopyToByteArray(protectedBuffer, out result);
            
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TPM encryption error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Decrypts data using TPM-backed DataProtectionProvider
    /// </summary>
    private async Task<byte[]?> DecryptWithTpmAsync(byte[] encryptedData)
    {
        try
        {
            var provider = new DataProtectionProvider(ProtectionDescriptor);
            
            // Convert byte array to IBuffer
            var buffer = CryptographicBuffer.CreateFromByteArray(encryptedData);
            
            // Decrypt
            var unprotectedBuffer = await provider.UnprotectAsync(buffer);
            
            // Convert back to byte array
            byte[] result;
            CryptographicBuffer.CopyToByteArray(unprotectedBuffer, out result);
            
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TPM decryption error: {ex.Message}");
            return null;
        }
    }

    private static string ReadPassword()
    {
        var password = new StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password.ToString();
    }
}
