using Windows.Security.Credentials.UI;

namespace GpgWindowsHello;

/// <summary>
/// Provides Windows Hello authentication functionality
/// </summary>
public class WindowsHelloAuth
{
    /// <summary>
    /// Checks if Windows Hello is available on the system
    /// </summary>
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync();
            return availability == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Requests user authentication through Windows Hello
    /// </summary>
    /// <param name="message">Message to display to the user</param>
    /// <returns>True if authentication succeeded, false otherwise</returns>
    public static async Task<bool> AuthenticateAsync(string message = "Authenticate to unlock GPG key")
    {
        try
        {
            var result = await UserConsentVerifier.RequestVerificationAsync(message);
            return result == UserConsentVerificationResult.Verified;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Windows Hello authentication error: {ex.Message}");
            return false;
        }
    }
}
