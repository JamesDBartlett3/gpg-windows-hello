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

    public PinentryServer(PassphraseProvider passphraseProvider)
    {
        _passphraseProvider = passphraseProvider;
    }

    /// <summary>
    /// Runs the pinentry server, reading commands from stdin and writing responses to stdout
    /// </summary>
    public async Task RunAsync()
    {
        // Send initial OK
        await WriteResponseAsync("OK Pleased to meet you");

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            var response = await ProcessCommandAsync(line);
            await WriteResponseAsync(response);
            
            if (line.StartsWith("BYE"))
                break;
        }
    }

    private async Task<string> ProcessCommandAsync(string command)
    {
        try
        {
            if (command.StartsWith("SETDESC "))
            {
                _description = Unescape(command.Substring(8));
                return "OK";
            }
            else if (command.StartsWith("SETPROMPT "))
            {
                _prompt = Unescape(command.Substring(10));
                return "OK";
            }
            else if (command.StartsWith("SETKEYINFO "))
            {
                _keyInfo = command.Substring(11);
                return "OK";
            }
            else if (command.StartsWith("GETPIN"))
            {
                return await GetPinAsync();
            }
            else if (command.StartsWith("CONFIRM"))
            {
                return await ConfirmAsync();
            }
            else if (command.StartsWith("MESSAGE"))
            {
                // Just acknowledge messages
                return "OK";
            }
            else if (command.StartsWith("OPTION"))
            {
                // Accept all options
                return "OK";
            }
            else if (command.StartsWith("GETINFO"))
            {
                var info = command.Substring(8).Trim();
                if (info == "version")
                {
                    return "D 1.0.0";
                }
                else if (info == "pid")
                {
                    return $"D {Environment.ProcessId}";
                }
                return "OK";
            }
            else if (command.StartsWith("BYE"))
            {
                return "OK closing connection";
            }
            else if (command.StartsWith("RESET"))
            {
                _description = null;
                _prompt = null;
                _keyInfo = null;
                return "OK";
            }
            else
            {
                // Unknown command, but don't error
                return "OK";
            }
        }
        catch (Exception ex)
        {
            return $"ERR 83886179 {ex.Message}";
        }
    }

    private async Task<string> GetPinAsync()
    {
        try
        {
            // Extract key ID from description or keyinfo
            var keyId = ExtractKeyId();
            
            // Get passphrase using Windows Hello
            var passphrase = await _passphraseProvider.GetPassphraseAsync(keyId ?? "default");
            
            if (passphrase == null)
            {
                return "ERR 83886179 Operation cancelled";
            }

            // Return the passphrase
            return $"D {passphrase}\nOK";
        }
        catch (Exception ex)
        {
            return $"ERR 83886179 {ex.Message}";
        }
    }

    private async Task<string> ConfirmAsync()
    {
        try
        {
            var message = _description ?? "Confirm operation";
            var authenticated = await WindowsHelloAuth.AuthenticateAsync(message);
            
            if (authenticated)
            {
                return "OK";
            }
            else
            {
                return "ERR 83886179 Operation cancelled";
            }
        }
        catch (Exception ex)
        {
            return $"ERR 83886179 {ex.Message}";
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

    private static async Task WriteResponseAsync(string response)
    {
        await Console.Out.WriteLineAsync(response);
        await Console.Out.FlushAsync();
    }
}
