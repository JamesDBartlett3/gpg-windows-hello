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
}
