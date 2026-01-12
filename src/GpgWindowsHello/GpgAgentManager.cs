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
            var socketCandidate = agentInfo.Split(':').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(socketCandidate) && PathUtils.TryNormalizeWindowsPath(socketCandidate, out var normalizedSocket))
            {
                if (File.Exists(normalizedSocket))
                {
                    return normalizedSocket;
                }
            }
            else if (!string.IsNullOrWhiteSpace(socketCandidate) && File.Exists(socketCandidate))
            {
                return socketCandidate;
            }
        }

        // Check for GPG home directory
        var gpgHome = PathUtils.GetGnuPgHomeDirectory();
        
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
    /// <param name="gpgExecutable">Optional path to gpg.exe; if provided, uses gpgconf.exe from same directory</param>
    public static bool IsAgentRunning(string? gpgExecutable = null)
    {
        try
        {
            string gpgConnectAgent = "gpg-connect-agent";
            
            if (!string.IsNullOrEmpty(gpgExecutable))
            {
                var gpgDir = Path.GetDirectoryName(gpgExecutable);
                if (gpgDir != null)
                {
                    var gpgConnectAgentPath = Path.Combine(gpgDir, "gpg-connect-agent.exe");
                    if (File.Exists(gpgConnectAgentPath))
                    {
                        gpgConnectAgent = gpgConnectAgentPath;
                    }
                }
            }
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gpgConnectAgent,
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
    /// <param name="gpgExecutable">Optional path to gpg.exe; if not provided, uses 'gpg' from PATH</param>
    public static async Task<List<string>> GetAvailableKeysAsync(string? gpgExecutable = null)
    {
        var keys = new List<string>();
        
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gpgExecutable ?? "gpg",
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
