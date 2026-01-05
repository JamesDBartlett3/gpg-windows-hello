using System.Diagnostics;
using System.Text;

namespace GpgWindowsHello;

/// <summary>
/// Manages interaction with the GPG agent
/// </summary>
public class GpgAgentManager
{
    private const string GpgAgentSocketEnvVar = "GPG_AGENT_INFO";
    
    /// <summary>
    /// Gets the GPG agent socket path from environment
    /// </summary>
    public static string? GetAgentSocketPath()
    {
        // Check common environment variables
        var agentInfo = Environment.GetEnvironmentVariable(GpgAgentSocketEnvVar);
        if (!string.IsNullOrEmpty(agentInfo))
        {
            // Format is typically: /path/to/socket:pid:protocol
            return agentInfo.Split(':').FirstOrDefault();
        }

        // Check for GPG home directory
        var gpgHome = Environment.GetEnvironmentVariable("GNUPGHOME") 
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gnupg");
        
        var socketPath = Path.Combine(gpgHome, "S.gpg-agent");
        if (File.Exists(socketPath))
        {
            return socketPath;
        }

        return null;
    }

    /// <summary>
    /// Checks if GPG agent is running
    /// </summary>
    public static bool IsAgentRunning()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gpg-connect-agent",
                    Arguments = "/bye",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets list of available GPG keys
    /// </summary>
    public static async Task<List<string>> GetAvailableKeysAsync()
    {
        var keys = new List<string>();
        
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gpg",
                    Arguments = "--list-secret-keys --with-colons",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse the output to extract key IDs
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("sec:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 4)
                    {
                        keys.Add(parts[4]); // Key ID is in the 5th field
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting GPG keys: {ex.Message}");
        }

        return keys;
    }
}
