using GpgWindowsHello;

namespace GpgWindowsHello;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Check if Windows Hello is available
            if (!await WindowsHelloAuth.IsAvailableAsync())
            {
                Console.Error.WriteLine("Windows Hello is not available on this system.");
                Console.Error.WriteLine("Please ensure Windows Hello is set up in Windows Settings.");
                return 1;
            }

            // Check if running without arguments from Explorer (installer mode)
            if (args.Length == 0)
            {
                // If stdin is connected to a console (interactive terminal), run installer
                // If stdin is redirected (pipe from GPG agent), run pinentry mode
                bool isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
                
                if (isInteractive)
                {
                    // Interactive mode - run setup
                    await SetupAsync();
                    return 0;
                }
                else
                {
                    // Redirected I/O - likely GPG agent calling us as pinentry
                    var passphraseProvider = new PassphraseProvider();
                    var pinentry = new PinentryServer(passphraseProvider);
                    await pinentry.RunAsync();
                    return 0;
                }
            }

            // Check if running as pinentry replacement
            if (args[0] == "--pinentry")
            {
                // Run in pinentry mode
                var passphraseProvider = new PassphraseProvider();
                var pinentry = new PinentryServer(passphraseProvider);
                await pinentry.RunAsync();
                return 0;
            }

            // Interactive mode
            if (args[0] == "--setup")
            {
                await SetupAsync();
                return 0;
            }
            else if (args[0] == "--test")
            {
                await TestAsync();
                return 0;
            }
            else
            {
                ShowHelp();
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task SetupAsync(string? installPathOverride = null)
    {
        Console.WriteLine("GpgWindowsHello Setup");
        Console.Write("Checking Windows Hello availability... ");
        if (await WindowsHelloAuth.IsAvailableAsync())
        {
            Console.WriteLine("OK");
        }
        else
        {
            Console.WriteLine("FAILED");
            Console.WriteLine("Please set up Windows Hello in Windows Settings before continuing.");
            return;
        }

        // Check GPG
        Console.Write("Checking GPG installation... ");
        if (GpgAgentManager.IsAgentRunning())
        {
            Console.WriteLine("OK");
        }
        else
        {
            Console.WriteLine("WARNING - GPG agent may not be running");
        }

        // List available keys
        Console.WriteLine();
        Console.WriteLine("Available GPG keys:");
        var keys = await GpgAgentManager.GetAvailableKeysAsync();
        if (keys.Count == 0)
        {
            Console.WriteLine("  No secret keys found");
            Console.WriteLine();
            Console.WriteLine("You need a GPG key to use GpgWindowsHello.");
            Console.WriteLine();
            Console.WriteLine("Would you like to:");
            Console.WriteLine("  1. Create a new GPG key");
            Console.WriteLine("  2. Import an existing GPG key");
            Console.WriteLine("  3. Skip this step");
            Console.WriteLine();
            Console.Write("Enter your choice (1-3): ");
            
            var choice = Console.ReadLine()?.Trim();
            
            if (choice == "1")
            {
                await CreateGpgKeyAsync();
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine();
            }
            else if (choice == "2")
            {
                await ImportGpgKeyAsync();
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
                Console.WriteLine();
            }
        }
        else
        {
            foreach (var key in keys)
            {
                Console.WriteLine($"  - {key}");
            }
        }

        // Detect all GPG installations
        Console.WriteLine();
        Console.WriteLine("Detecting GPG Installations:");
        Console.WriteLine("============================");
        Console.WriteLine();
        
        var gpgInstallations = await DetectAllGpgInstallationsAsync();
        
        if (gpgInstallations.Count == 0)
        {
            Console.WriteLine("✗ No GPG installations found!");
            Console.WriteLine("Please install GPG before using GpgWindowsHello.");
            return;
        }
        
        Console.WriteLine($"Found {gpgInstallations.Count} GPG installation{(gpgInstallations.Count > 1 ? "s" : "")}:");
        for (int i = 0; i < gpgInstallations.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {gpgInstallations[i]}");
        }
        Console.WriteLine();
        
        if (gpgInstallations.Count > 1)
        {
            Console.WriteLine("Enter the number(s) of the GPG installation(s) to configure.");
            Console.WriteLine("Use commas and/or spaces as separators (example: 1, 3). Use 'all' to configure everything.");
            Console.WriteLine();
            Console.Write($"Selection (1-{gpgInstallations.Count}): ");

            var selection = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(selection))
            {
                Console.WriteLine("No selection provided. Aborting.");
                return;
            }

            List<int> indices;
            if (string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase))
            {
                indices = Enumerable.Range(1, gpgInstallations.Count).ToList();
            }
            else
            {
                var tokens = selection
                    .Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                indices = new List<int>();
                foreach (var token in tokens)
                {
                    if (!int.TryParse(token, out var parsed))
                    {
                        Console.WriteLine($"Invalid selection token: '{token}'. Aborting.");
                        return;
                    }

                    if (parsed < 1 || parsed > gpgInstallations.Count)
                    {
                        Console.WriteLine($"Selection out of range: {parsed}. Aborting.");
                        return;
                    }

                    indices.Add(parsed);
                }

                indices = indices.Distinct().OrderBy(i => i).ToList();
                if (indices.Count == 0)
                {
                    Console.WriteLine("No valid selections provided. Aborting.");
                    return;
                }
            }

            Console.WriteLine();
            foreach (var index in indices)
            {
                var selectedGpg = gpgInstallations[index - 1];
                Console.WriteLine($"✓ Selected: {selectedGpg}");
            }
            Console.WriteLine();

            foreach (var index in indices)
            {
                var selectedGpg = gpgInstallations[index - 1];
                Console.WriteLine($"Configuring: {selectedGpg}");
                Console.WriteLine(new string('-', 80));
                await ConfigureSingleGpgInstallationAsync(selectedGpg, installPathOverride);
                Console.WriteLine();
            }

            Console.WriteLine("Configuration complete!");
            return;
        }

        // Only one installation, configure it
        var onlyGpg = gpgInstallations[0];
        await ConfigureSingleGpgInstallationAsync(onlyGpg, installPathOverride);
    }

    static async Task ConfigureAllGpgInstallationsAsync(List<string> gpgInstallations, string? installPathOverride = null)
    {
        var exePath = installPathOverride ?? Environment.ProcessPath ?? "GpgWindowsHello.exe";
        
        foreach (var gpgExecutable in gpgInstallations)
        {
            Console.WriteLine($"Configuring: {gpgExecutable}");
            Console.WriteLine(new string('-', 80));
            
            var agentConfPath = await GetGpgAgentConfPathAsync(gpgExecutable);
            await ConfigurePinentryAsync(gpgExecutable, agentConfPath, exePath);
            
            Console.WriteLine();
        }
        
        Console.WriteLine("Configuration complete for all GPG installations!");
    }

    static async Task ConfigureSingleGpgInstallationAsync(string gpgExecutable, string? installPathOverride = null)
    {
        var exePath = installPathOverride ?? Environment.ProcessPath ?? "GpgWindowsHello.exe";
        var agentConfPath = await GetGpgAgentConfPathAsync(gpgExecutable);
        
        await CheckGitGpgConfigurationAsync(gpgExecutable);

        // Check if pinentry-program is already configured correctly
        Console.WriteLine();
        await ConfigurePinentryAsync(gpgExecutable, agentConfPath, exePath);
    }

    static async Task ConfigurePinentryAsync(string gpgExecutable, string agentConfPath, string exePath)
    {
        Console.WriteLine("Pinentry Configuration:");
        Console.WriteLine("=======================");
        Console.WriteLine();
        Console.WriteLine($"Config file: {agentConfPath}");
        Console.WriteLine();
        
        var isConfigured = false;
        var needsUpdate = false;
        var existingLines = new List<string>();
        
        if (File.Exists(agentConfPath))
        {
            existingLines = (await File.ReadAllLinesAsync(agentConfPath)).ToList();
            foreach (var line in existingLines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("pinentry-program", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it points to our executable
                    if (trimmedLine.Contains(exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"✓ Already configured: {trimmedLine}");
                        isConfigured = true;
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Current configuration: {trimmedLine}");
                        Console.WriteLine($"  This needs to be updated to use GpgWindowsHello.");
                        needsUpdate = true;
                        break;
                    }
                }
            }
        }
        
        if (!isConfigured)
        {
            Console.WriteLine();
            if (needsUpdate)
            {
                Console.Write("Would you like to update the pinentry-program configuration? (y/n): ");
            }
            else
            {
                Console.Write("Would you like to add GpgWindowsHello as the pinentry program? (y/n): ");
            }
            
            var response = Console.ReadLine()?.Trim().ToLower();
            
            if (response == "y" || response == "yes")
            {
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(agentConfPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var newLines = new List<string>();
                    bool pinentryLineAdded = false;
                    
                    if (existingLines.Count > 0)
                    {
                        // Update existing file
                        foreach (var line in existingLines)
                        {
                            var trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("pinentry-program", StringComparison.OrdinalIgnoreCase))
                            {
                                // Replace the line
                                newLines.Add($"pinentry-program {exePath}");
                                pinentryLineAdded = true;
                            }
                            else
                            {
                                newLines.Add(line);
                            }
                        }
                    }
                    
                    // Add the line if it wasn't replaced
                    if (!pinentryLineAdded)
                    {
                        newLines.Add($"pinentry-program {exePath}");
                    }
                    
                    // Write the file
                    await File.WriteAllLinesAsync(agentConfPath, newLines);
                    
                    Console.WriteLine($"✓ Configuration updated successfully!");
                    Console.WriteLine();
                    Console.WriteLine("Please restart the GPG agent for changes to take effect:");
                    
                    var gpgDir = Path.GetDirectoryName(gpgExecutable);
                    var gpgconfExecutable = gpgDir != null ? Path.Combine(gpgDir, "gpgconf.exe") : "gpgconf";
                    Console.WriteLine($"  \"{gpgconfExecutable}\" --kill gpg-agent");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed to update configuration: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine("Manual configuration required:");
                    Console.WriteLine($"  Add this line to {agentConfPath}:");
                    Console.WriteLine($"  pinentry-program {exePath}");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Skipped. To configure manually, add this line to your gpg-agent.conf:");
                Console.WriteLine($"  pinentry-program {exePath}");
                Console.WriteLine();
                Console.WriteLine($"Configuration file location:");
                Console.WriteLine($"  {agentConfPath}");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("No configuration changes needed!");
        }
    }

    static async Task TestAsync()
    {
        Console.WriteLine("GpgWindowsHello Test");
        Console.WriteLine("====================");
        Console.WriteLine();

        // Test Windows Hello
        Console.WriteLine("Testing Windows Hello authentication...");
        var authenticated = await WindowsHelloAuth.AuthenticateAsync("Test authentication");
        
        if (authenticated)
        {
            Console.WriteLine("✓ Windows Hello authentication successful!");
        }
        else
        {
            Console.WriteLine("✗ Windows Hello authentication failed!");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Test completed successfully!");
    }

    static void ShowHelp()
    {
        Console.WriteLine("GpgWindowsHello - Windows Hello authentication for GPG");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  GpgWindowsHello [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --install    Install GpgWindowsHello to your system");
        Console.WriteLine("  --setup      Run setup wizard and configure GPG installations");
        Console.WriteLine("  --test       Test Windows Hello authentication");
        Console.WriteLine("  --pinentry   Run in pinentry mode (used by GPG agent)");
        Console.WriteLine("  --help       Show this help message");
        Console.WriteLine();
        Console.WriteLine("When run without arguments from Windows Explorer, installer mode launches automatically.");
        Console.WriteLine();
        Console.WriteLine("For more information, visit:");
        Console.WriteLine("  https://github.com/JamesDBartlett3/gpg-windows-hello");
        Console.WriteLine();
    }

    static async Task CreateGpgKeyAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Create New GPG Key");
        Console.WriteLine("==================");
        Console.WriteLine();
        
        Console.Write("Enter your name: ");
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Name cannot be empty. Aborting.");
            return;
        }

        Console.Write("Enter your email address: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            Console.WriteLine("Email cannot be empty. Aborting.");
            return;
        }

        Console.Write("Enter a comment (optional): ");
        var comment = Console.ReadLine()?.Trim();

        Console.WriteLine();
        Console.WriteLine("Creating GPG key...");
        Console.WriteLine("This may take a few moments.");
        Console.WriteLine();

        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gpg",
                    Arguments = "--full-generate-key --batch",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };

            // Build batch input for GPG
            var batchInput = $@"Key-Type: RSA
Key-Length: 4096
Subkey-Type: RSA
Subkey-Length: 4096
Name-Real: {name}
Name-Email: {email}";

            if (!string.IsNullOrEmpty(comment))
            {
                batchInput += $"\nName-Comment: {comment}";
            }

            batchInput += "\nExpire-Date: 0\n%no-protection\n%commit\n";

            process.Start();
            await process.StandardInput.WriteAsync(batchInput);
            process.StandardInput.Close();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✓ GPG key created successfully!");
                Console.WriteLine();
                
                // Show the new key
                var keys = await GpgAgentManager.GetAvailableKeysAsync();
                if (keys.Count > 0)
                {
                    Console.WriteLine("Your new key ID: " + keys.Last());
                }
            }
            else
            {
                Console.WriteLine("✗ Failed to create GPG key.");
                Console.WriteLine(error);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error creating GPG key: {ex.Message}");
        }
    }

    static async Task ImportGpgKeyAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Import GPG Key");
        Console.WriteLine("==============");
        Console.WriteLine();
        Console.WriteLine("You need to import your PRIVATE key (which includes the public key).");
        Console.WriteLine();
        Console.WriteLine("If you have an existing key on another machine, export it first:");
        Console.WriteLine("  gpg --export-secret-keys --armor YOUR_KEY_ID > private-key.asc");
        Console.WriteLine();
        Console.WriteLine("Then provide the path to that file, or press Enter to paste the key content:");
        Console.Write("Path: ");
        
        var path = Console.ReadLine()?.Trim();

        try
        {
            if (!string.IsNullOrEmpty(path))
            {
                // Remove surrounding quotes if present
                path = path.Trim('"', '\'');
                
                // Import from file
                if (!File.Exists(path))
                {
                    Console.WriteLine($"File not found: {path}");
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("Importing GPG key from file...");
                Console.WriteLine();

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gpg",
                        Arguments = $"--batch --import \"{path}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // Add timeout to prevent hanging
                var timeoutTask = Task.Delay(30000); // 30 second timeout
                var completedTask = await Task.WhenAny(process.WaitForExitAsync(), timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    // Graceful attempt first (if a window exists), then last-resort terminate the process.
                    try
                    {
                        if (process.CloseMainWindow())
                        {
                            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
                        }
                    }
                    catch { }

                    try { process.Kill(entireProcessTree: false); }
                    catch { }
                    Console.WriteLine("✗ Import timed out. The key may require a passphrase.");
                    Console.WriteLine("Try importing manually: gpg --import <path-to-key>");
                    return;
                }
                
                var output = await outputTask;
                var error = await errorTask;

                Console.WriteLine();
                if (process.ExitCode == 0 || error.Contains("imported") || error.Contains("not changed"))
                {
                    Console.WriteLine("✓ GPG key imported successfully!");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine(error);
                    }
                }
                else
                {
                    Console.WriteLine("✗ Failed to import GPG key.");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine(error);
                    }
                }
            }
            else
            {
                // Import from stdin
                Console.WriteLine();
                Console.WriteLine("Paste your GPG key (including -----BEGIN/END----- lines).");
                Console.WriteLine("Press Ctrl+Z then Enter when done:");
                Console.WriteLine();

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gpg",
                        Arguments = "--import",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    }
                };

                process.Start();

                // Read from console until EOF
                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    await process.StandardInput.WriteLineAsync(line);
                }
                process.StandardInput.Close();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("✓ GPG key imported successfully!");
                    Console.WriteLine(error); // GPG outputs import info to stderr
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("✗ Failed to import GPG key.");
                    Console.WriteLine(error);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error importing GPG key: {ex.Message}");
        }
    }

    static async Task<List<string>> DetectAllGpgInstallationsAsync()
    {
        var installations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Use 'where' command to find all gpg.exe in PATH
            var whereGpg = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "gpg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            whereGpg.Start();
            var output = await whereGpg.StandardOutput.ReadToEndAsync();
            await whereGpg.WaitForExitAsync();

            if (whereGpg.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var matches = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var match in matches)
                {
                    if (File.Exists(match))
                    {
                        installations.Add(match);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        // Also check common installation paths
        var commonPaths = new[]
        {
            "C:\\Program Files\\GnuPG\\bin\\gpg.exe",
            "C:\\Program Files (x86)\\GnuPG\\bin\\gpg.exe",
            "C:\\Program Files\\Git\\usr\\bin\\gpg.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                installations.Add(path);
            }
        }
        
        return installations.OrderBy(p => p).ToList();
    }

    static async Task<string> DetectGpgExecutableAsync()
    {
        // Try to find which GPG executable Git will use
        try
        {
            // First check if git has a configured gpg.program
            var gitGpgCheck = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config --global gpg.program",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            gitGpgCheck.Start();
            var gitGpgPath = (await gitGpgCheck.StandardOutput.ReadToEndAsync()).Trim();
            await gitGpgCheck.WaitForExitAsync();

            if (!string.IsNullOrEmpty(gitGpgPath) && File.Exists(gitGpgPath.Replace("/", "\\")))
            {
                return gitGpgPath.Replace("/", "\\");
            }

            // If not configured, Git will use gpg from PATH
            // Try to find it using 'where' command
            var whereGpg = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "gpg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            whereGpg.Start();
            var output = await whereGpg.StandardOutput.ReadToEndAsync();
            await whereGpg.WaitForExitAsync();

            if (whereGpg.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // 'where' returns all matches, take the first one
                var firstMatch = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstMatch) && File.Exists(firstMatch))
                {
                    return firstMatch;
                }
            }

            // Fallback: check common GPG locations
            var commonPaths = new[]
            {
                "C:\\Program Files\\GnuPG\\bin\\gpg.exe",
                "C:\\Program Files (x86)\\GnuPG\\bin\\gpg.exe",
                "C:\\Program Files\\Git\\usr\\bin\\gpg.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // Ignore errors and fall through
        }

        return "gpg"; // Fallback to just "gpg" and hope it's in PATH
    }

    static async Task<string> GetGpgAgentConfPathAsync(string gpgExecutable)
    {
        try
        {
            // Determine gpgconf executable (usually in same directory as gpg)
            var gpgDir = Path.GetDirectoryName(gpgExecutable);
            var gpgconfExecutable = gpgDir != null 
                ? Path.Combine(gpgDir, "gpgconf.exe")
                : "gpgconf";

            if (!File.Exists(gpgconfExecutable))
            {
                gpgconfExecutable = "gpgconf"; // Fallback to PATH
            }

            // Run 'gpgconf --list-dirs' to get the configuration directory
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = gpgconfExecutable,
                    Arguments = "--list-dirs",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse output for homedir line
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("homedir:", StringComparison.OrdinalIgnoreCase))
                {
                    var homedir = line.Substring("homedir:".Length).Trim();
                    
                    // Handle URL-encoded characters
                    homedir = homedir.Replace("%3a", ":").Replace("%5c", "\\").Replace("%2f", "/");
                    
                    // Convert Unix-style paths (e.g., /c/Users/...) to Windows paths (C:\Users\...)
                    if (homedir.StartsWith("/") && homedir.Length > 2 && homedir[2] == '/')
                    {
                        // Format: /c/Users/... -> C:\Users\...
                        homedir = homedir[1].ToString().ToUpper() + ":" + homedir.Substring(2).Replace("/", "\\");
                    }
                    
                    return Path.Combine(homedir, "gpg-agent.conf");
                }
            }
        }
        catch
        {
            // Ignore and use fallback
        }

        // Fallback to default location
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gnupg",
            "gpg-agent.conf"
        );
    }

    static async Task CheckGitGpgConfigurationAsync(string selectedGpgExecutable)
    {
        Console.WriteLine("Git Configuration Check:");
        Console.WriteLine("========================");
        Console.WriteLine();

        try
        {
            // Check if git is installed
            var gitCheck = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            gitCheck.Start();
            await gitCheck.WaitForExitAsync();

            if (gitCheck.ExitCode != 0)
            {
                Console.WriteLine("Git is not installed. Skipping Git configuration check.");
                Console.WriteLine("GpgWindowsHello will work with GPG directly, but not with Git signing.");
                return;
            }

            // Check which GPG executable Git will use
            var gitGpgExecutable = await DetectGpgExecutableAsync();
            
            // Compare with the selected GPG
            bool gitUsesSelectedGpg = string.Equals(
                Path.GetFullPath(gitGpgExecutable).TrimEnd('\\'), 
                Path.GetFullPath(selectedGpgExecutable).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase
            );
            
            if (gitUsesSelectedGpg)
            {
                Console.WriteLine($"✓ Git is configured to use: {gitGpgExecutable}");
                Console.WriteLine("  This matches your selected GPG installation.");
            }
            else
            {
                Console.WriteLine($"⚠ Git is currently configured to use: {gitGpgExecutable}");
                Console.WriteLine($"  But you selected: {selectedGpgExecutable}");
                Console.WriteLine();
                Console.Write("Would you like to configure Git to use the selected GPG? (y/n): ");
                
                var response = Console.ReadLine()?.Trim().ToLower();
                
                if (response == "y" || response == "yes")
                {
                    await ConfigureGitGpgAsync(selectedGpgExecutable);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not check Git configuration: {ex.Message}");
        }
    }
    
    static async Task ConfigureGitGpgAsync(string gpgExecutable)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("Configuring Git...");
            
            var gitConfig = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"config --global gpg.program \"{gpgExecutable.Replace("\\", "/")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            gitConfig.Start();
            await gitConfig.WaitForExitAsync();

            if (gitConfig.ExitCode == 0)
            {
                Console.WriteLine($"✓ Git configured to use: {gpgExecutable}");
            }
            else
            {
                var error = await gitConfig.StandardError.ReadToEndAsync();
                Console.WriteLine($"✗ Failed to configure Git: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error configuring Git: {ex.Message}");
        }
    }
}
