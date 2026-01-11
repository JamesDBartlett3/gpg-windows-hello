using System.Text;

namespace GpgWindowsHello;

/// <summary>
/// Implements a pinentry-compatible interface for GPG agent
/// This replaces the standard pinentry program with Windows Hello authentication
/// </summary>
public class PinentryServer
{
    private readonly PassphraseProvider _passphraseProvider;
    private string? _description;
    private string? _prompt;
    private string? _keyInfo;
    private const string DebugEnvVarName = "GPGWINDOWSHELLO_DEBUG";

    public PinentryServer(PassphraseProvider passphraseProvider)
    {
        _passphraseProvider = passphraseProvider;
        Log("PinentryServer started");
    }

    /// <summary>
    /// Runs the pinentry server, reading commands from stdin and writing responses to stdout
    /// </summary>
    public async Task RunAsync()
    {
        // Send initial OK
        await WriteLineAsync("OK Pleased to meet you");

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            Log($"Received command: {line}");
            await ProcessCommandAsync(line);
            
            if (line.StartsWith("BYE"))
                break;
        }
        Log("PinentryServer stopped");
    }

    private async Task ProcessCommandAsync(string command)
    {
        try
        {
            if (command.StartsWith("SETDESC "))
            {
                _description = Unescape(command.Substring(8));
                await WriteLineAsync("OK");
            }
            else if (command.StartsWith("SETPROMPT "))
            {
                _prompt = Unescape(command.Substring(10));
                await WriteLineAsync("OK");
            }
            else if (command.StartsWith("SETKEYINFO "))
            {
                _keyInfo = command.Substring(11);
                await WriteLineAsync("OK");
            }
            else if (command.StartsWith("GETPIN"))
            {
                await GetPinAsync();
            }
            else if (command.StartsWith("CONFIRM"))
            {
                await ConfirmAsync();
            }
            else if (command.StartsWith("MESSAGE"))
            {
                // Just acknowledge messages
                await WriteLineAsync("OK");
            }
            else if (command.StartsWith("OPTION"))
            {
                // Accept all options
                await WriteLineAsync("OK");
            }
            else if (command.StartsWith("GETINFO"))
            {
                var info = command.Substring(8).Trim();
                if (info == "version")
                {
                    await WriteLineAsync("D 1.0.0");
                    await WriteLineAsync("OK");
                }
                else if (info == "pid")
                {
                    await WriteLineAsync($"D {Environment.ProcessId}");
                    await WriteLineAsync("OK");
                }
                else
                {
                    await WriteLineAsync("OK");
                }
            }
            else if (command.StartsWith("BYE"))
            {
                await WriteLineAsync("OK closing connection");
            }
            else if (command.StartsWith("RESET"))
            {
                _description = null;
                _prompt = null;
                _keyInfo = null;
                await WriteLineAsync("OK");
            }
            else
            {
                // Unknown command, but don't error
                await WriteLineAsync("OK");
            }
        }
        catch (Exception ex)
        {
            await WriteLineAsync($"ERR 83886179 {ex.Message}");
        }
    }

    private async Task GetPinAsync()
    {
        try
        {
            Log("GetPinAsync called");
            // Extract key ID from description or keyinfo
            var keyId = ExtractKeyId();
            Log($"Extracted keyId: {keyId}");
            
            // Get passphrase using Windows Hello
            var passphrase = await _passphraseProvider.GetPassphraseAsync(keyId ?? "default");
            
            if (passphrase == null)
            {
                Log("Passphrase was null");
                await WriteLineAsync("ERR 83886179 Operation cancelled");
                return;
            }

            Log("Passphrase retrieved successfully");
            // Return the passphrase - encode special characters
            var encoded = EncodePassphrase(passphrase);
            Log("Encoded passphrase for transmission");
            await WriteLineAsync($"D {encoded}");
            await WriteLineAsync("OK");
        }
        catch (Exception ex)
        {
            Log($"GetPinAsync error: {ex.Message}");
            await WriteLineAsync($"ERR 83886179 {ex.Message}");
        }
    }

    private string EncodePassphrase(string passphrase)
    {
        // Pinentry protocol requires percent-encoding of certain characters
        var result = new StringBuilder();
        foreach (char c in passphrase)
        {
            if (c == '%' || c == '\n' || c == '\r' || c < 32 || c > 126)
            {
                result.Append($"%{((int)c):X2}");
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    private async Task ConfirmAsync()
    {
        try
        {
            var message = _description ?? "Confirm operation";
            var authenticated = await WindowsHelloAuth.AuthenticateAsync(message);
            
            if (authenticated)
            {
                await WriteLineAsync("OK");
            }
            else
            {
                await WriteLineAsync("ERR 83886179 Operation cancelled");
            }
        }
        catch (Exception ex)
        {
            await WriteLineAsync($"ERR 83886179 {ex.Message}");
        }
    }

    private string? ExtractKeyId()
    {
        // Try to extract key ID from keyinfo
        if (!string.IsNullOrEmpty(_keyInfo))
        {
            // Format can be like "n/KEYID" or just "KEYID"
            var parts = _keyInfo.Split('/');
            return parts.Length > 1 ? parts[1] : parts[0];
        }

        // Try to extract from description
        if (!string.IsNullOrEmpty(_description))
        {
            // Look for patterns like "key XXXXXXXX" or just a key ID
            var words = _description.Split(' ');
            foreach (var word in words)
            {
                if (word.Length >= 8 && word.All(c => char.IsLetterOrDigit(c)))
                {
                    return word;
                }
            }
        }

        return null;
    }

    private static string Unescape(string input)
    {
        // Handle percent-encoding used by pinentry protocol
        var result = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '%' && i + 2 < input.Length)
            {
                var hex = input.Substring(i + 1, 2);
                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value))
                {
                    result.Append((char)value);
                    i += 2;
                    continue;
                }
            }
            result.Append(input[i]);
        }
        return result.ToString();
    }

    private static async Task WriteLineAsync(string line)
    {
        await Console.Out.WriteLineAsync(line);
        await Console.Out.FlushAsync();
    }

    private static void Log(string message)
    {
        if (!IsDebugEnabled()) return;
        try { Console.Error.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - PinentryServer: {message}"); }
        catch { }
    }

    private static bool IsDebugEnabled()
    {
        var value = Environment.GetEnvironmentVariable(DebugEnvVarName);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
