namespace GpgWindowsHello;

internal static class PathUtils
{
    internal static string GetGnuPgHomeDirectory()
    {
        var envGnuPgHome = Environment.GetEnvironmentVariable("GNUPGHOME");
        if (TryNormalizeWindowsPath(envGnuPgHome, out var normalizedEnvHome))
        {
            return normalizedEnvHome;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gnupg");
    }

    internal static bool TryNormalizeWindowsPath(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim().Trim('"');
        value = Environment.ExpandEnvironmentVariables(value);

        // Handle URL-encoded characters commonly returned by gpgconf.
        value = value.Replace("%3a", ":").Replace("%3A", ":")
                     .Replace("%5c", "\\").Replace("%5C", "\\")
                     .Replace("%2f", "/").Replace("%2F", "/");

        // Convert Unix-style roots commonly emitted by Git/MSYS/Cygwin to a Windows path.
        // Examples:
        //   /c/Users/... -> C:\Users\...
        //   /cygdrive/c/Users/... -> C:\Users\...
        if (value.StartsWith("/cygdrive/", StringComparison.OrdinalIgnoreCase) && value.Length >= 12 && char.IsLetter(value[10]) && value[11] == '/')
        {
            var drive = char.ToUpperInvariant(value[10]);
            value = $"{drive}:{value.Substring(11)}";
        }

        if (value.Length >= 4 && value[0] == '/' && char.IsLetter(value[1]) && value[2] == '/')
        {
            var drive = char.ToUpperInvariant(value[1]);
            value = $"{drive}:{value.Substring(2)}";
        }

        // If it's still a POSIX absolute path (e.g., /home/James/.gnupg), don't try to use it on Windows.
        if (value.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = value.Replace('/', '\\');

        var hasDrive = value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
        var isUnc = value.StartsWith("\\\\", StringComparison.Ordinal);

        // Reject non-drive, non-UNC paths (e.g., \home\James...) which can happen when a POSIX path is naively slash-replaced.
        if (!hasDrive && !isUnc)
        {
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(value).TrimEnd('\\');
            return true;
        }
        catch
        {
            return false;
        }
    }
}
