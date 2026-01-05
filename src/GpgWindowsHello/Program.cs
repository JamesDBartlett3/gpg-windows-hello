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

            // Check if running as pinentry replacement
            if (args.Length == 0 || args[0] == "--pinentry")
            {
                // Run in pinentry mode
                var passphraseProvider = new PassphraseProvider();
                var pinentry = new PinentryServer(passphraseProvider);
                await pinentry.RunAsync();
                return 0;
            }

            // Interactive mode
            if (args.Length > 0 && args[0] == "--setup")
            {
                await SetupAsync();
                return 0;
            }
            else if (args.Length > 0 && args[0] == "--test")
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

    static async Task SetupAsync()
    {
        Console.WriteLine("GpgWindowsHello Setup");
        Console.WriteLine("=====================");
        Console.WriteLine();

        // Check Windows Hello
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

        // Show configuration instructions
        Console.WriteLine();
        Console.WriteLine("Configuration Instructions:");
        Console.WriteLine("===========================");
        Console.WriteLine();
        Console.WriteLine("To use GpgWindowsHello as your pinentry program, add the following line");
        Console.WriteLine("to your gpg-agent.conf file:");
        Console.WriteLine();
        
        var exePath = Environment.ProcessPath ?? "GpgWindowsHello.exe";
        Console.WriteLine($"  pinentry-program {exePath}");
        Console.WriteLine();
        
        var gpgHome = Environment.GetEnvironmentVariable("GNUPGHOME") 
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gnupg");
        var configFile = Path.Combine(gpgHome, "gpg-agent.conf");
        
        Console.WriteLine($"The gpg-agent.conf file is typically located at:");
        Console.WriteLine($"  {configFile}");
        Console.WriteLine();
        Console.WriteLine("After updating the configuration, restart the GPG agent:");
        Console.WriteLine("  gpgconf --kill gpg-agent");
        Console.WriteLine();
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
        Console.WriteLine("  --setup      Run setup wizard and show configuration instructions");
        Console.WriteLine("  --test       Test Windows Hello authentication");
        Console.WriteLine("  --pinentry   Run in pinentry mode (used by GPG agent)");
        Console.WriteLine("  --help       Show this help message");
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
                    process.Kill(true);
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
}
