using System.Security.Cryptography;
using System.Text;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace GpgWindowsHello;

/// <summary>
/// Provides passphrase management using Windows Hello authentication
/// Passphrases are stored securely using Windows DataProtectionProvider, with a DPAPI fallback.
/// </summary>
public class PassphraseProvider
{
    private readonly Dictionary<string, string> _cachedPassphrases = new();
    private readonly string _storageFile;
    private const string ProtectionDescriptor = "LOCAL=user";
    private const string DebugEnvVarName = "GPGWINDOWSHELLO_DEBUG";

    public PassphraseProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "GpgWindowsHello");
        Directory.CreateDirectory(appDir);
        _storageFile = Path.Combine(appDir, "gpg-auth.bin");
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
        var authenticated = await WindowsHelloAuth.AuthenticateAsync($"Authenticate to access GPG key {keyId}");
        
        if (authenticated)
        {
            try
            {
                // Authenticate to access key material
                LogDebug("Windows Hello auth successful");
            }
            catch (Exception ex)
            {
                LogDebug($"Error during auth logging: {ex.Message}");
            }
        }
        
        if (!authenticated)
        {
            return null;
        }

        // Try to load stored passphrase
        var passphrase = LoadStoredPassphrase(keyId);
        
        if (passphrase == null)
        {
            var protectionNotice = await GetProtectionNoticeAsync();
            // Prompt for the actual GPG passphrase using a credential dialog
            passphrase = await PromptForPassphraseAsync(keyId, protectionNotice);
            
            if (passphrase != null) // null means cancelled, empty string is valid (no passphrase)
            {
                // Store the passphrase for future use
                StorePassphrase(keyId, passphrase);
            }
            else
            {
                return null;
            }
        }

        if (!string.IsNullOrEmpty(passphrase))
        {
            _cachedPassphrases[keyId] = passphrase;
        }

        return passphrase;
    }

    private async Task<string?> PromptForPassphraseAsync(string keyId, string protectionNotice)
    {
        // Use a simple input dialog to prompt for passphrase
        // This works even when console I/O is redirected
        return await Task.Run(() => PromptForPassphraseWithDialog(keyId, protectionNotice));
    }

    private string? PromptForPassphraseWithDialog(string keyId, string protectionNotice)
    {
        // Use a custom WinForms dialog instead of VB InputBox
        // This avoids the Microsoft.VisualBasic dependency which can trigger AV heuristics
        try
        {
            LogDebug($"Showing InputDialog for key {keyId}");
            
            string? result = null;
            
            // Run on a proper STA thread if needed, or just ShowDialog if we are already in one (we might not be)
            // Since we are likely in a console app, we can just instantiate the form.
            // For console apps, the thread logic for UI can be tricky, but ShowDialog pumps its own message loop.
            
            var prompt = $"Enter your GPG passphrase for key {keyId}.\n\n" +
                         $"{protectionNotice}\n" +
                         "You'll only need to enter it once.\n\n" +
                         "If your key has NO passphrase, leave this blank and click OK.";

            using (var form = new PassphraseInputDialog("GpgWindowsHello - First Time Setup", prompt))
            {
                // Ensure the form appears on top
                form.TopMost = true;
                form.Activate();
                
                var dialogResult = form.ShowDialog();
                if (dialogResult == DialogResult.OK)
                {
                    result = form.Passphrase;
                }
                else
                {
                    result = null; // Cancelled
                }
            }
            
            LogDebug($"InputDialog returned: {(result == null ? "null/cancelled" : result.Length == 0 ? "empty string" : "has value")}");
            return result;
        }
        catch (Exception ex)
        {
            LogDebug($"InputDialog error: {ex.Message}");
            return null;
        }
    }

    private static void LogDebug(string message)
    {
        if (!IsDebugEnabled()) return;
        try { Console.Error.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - PassphraseProvider: {message}"); }
        catch { }
    }

    private static bool IsDebugEnabled()
    {
        var value = Environment.GetEnvironmentVariable(DebugEnvVarName);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetProtectionNoticeAsync()
    {
        // Do not promise hardware-backed protection; instead, report the preferred method
        // and whether it appears available on this machine.
        bool supportsPreferred = await CanUseDataProtectionProviderAsync();
        if (supportsPreferred)
        {
            return "Storage encryption (expected): Windows DataProtectionProvider. If that fails at save time, a Windows DPAPI (CurrentUser) fallback will be used.";
        }

        return "Storage encryption (expected): Windows DPAPI (CurrentUser) fallback (DataProtectionProvider not available).";
    }

    private async Task<bool> CanUseDataProtectionProviderAsync()
    {
        try
        {
            var probe = await EncryptWithTpmAsync(new byte[] { 0x00 });
            return probe != null;
        }
        catch
        {
            return false;
        }
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
                LogDebug("Passphrase stored securely using Windows DataProtectionProvider.");
            }
            else
            {
                // Fallback to DPAPI if TPM encryption fails
                LogDebug("Preferred protection not available, using Windows DPAPI (CurrentUser) fallback.");
                encryptedResult = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_storageFile, encryptedResult);
                LogDebug("Passphrase stored securely using Windows DPAPI (CurrentUser).");
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
