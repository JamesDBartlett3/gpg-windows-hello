# gpg-windows-hello

Windows Hello integration for easy GPG agent authentication. This application replaces the GPG passphrase prompt with Windows Hello authentication (fingerprint, facial recognition, or PIN), providing a more convenient and secure way to use GPG for signing commits, emails, and other cryptographic operations.

## Features

- 🔐 **Windows Hello Authentication**: Use biometrics or PIN instead of typing your GPG passphrase
- 🛡️ **TPM-Backed Security**: Passphrases are encrypted and stored securely using the hardware Trusted Platform Module (TPM)
- 🔄 **Pinentry Replacement**: Seamlessly integrates with GPG agent as a drop-in replacement for pinentry
- ⚡ **Caching**: Authenticated passphrases are cached for the session to avoid repeated prompts
- 🪟 **Windows Native**: Built with .NET for optimal Windows integration

## Requirements

- Windows 10 (version 2004 or later) or Windows 11
- Windows Hello configured (fingerprint, face recognition, or PIN)
- GPG (GnuPG) installed
- .NET 10.0 Runtime or later

## Installation

### From Release

1. Download the latest release from the [Releases](https://github.com/JamesDBartlett3/gpg-windows-hello/releases) page
2. Extract the files to a location of your choice (e.g., `C:\Program Files\GpgWindowsHello`)
3. Run the setup wizard: `GpgWindowsHello.exe --setup`

### From Source

```bash
git clone https://github.com/JamesDBartlett3/gpg-windows-hello.git
cd gpg-windows-hello
dotnet build
```

## Setup

1. **Run the setup wizard** to verify your system configuration:
   ```
   GpgWindowsHello.exe --setup
   ```

2. **Configure GPG agent** to use GpgWindowsHello as the pinentry program:
   
   Add the following line to your `gpg-agent.conf` file:
   ```
   pinentry-program C:\path\to\GpgWindowsHello.exe
   ```
   
   The `gpg-agent.conf` file is typically located at:
   - `%APPDATA%\gnupg\gpg-agent.conf`

3. **Restart the GPG agent**:
   ```
   gpgconf --kill gpg-agent
   ```

4. **Test the integration**:
   ```
   GpgWindowsHello.exe --test
   ```

## Usage

Once configured, GpgWindowsHello works automatically in the background. Whenever GPG needs your passphrase (e.g., for signing a commit), you'll be prompted for Windows Hello authentication instead of typing your passphrase.

### First-Time Use

The first time you use a GPG key with GpgWindowsHello, you'll be asked to:
1. Authenticate with Windows Hello
2. Enter your GPG passphrase (one time only)

Your passphrase is then encrypted using TPM and stored securely. Future operations only require Windows Hello authentication.

### Example: Signing Git Commits

```bash
# Configure Git to sign commits
git config --global commit.gpgsign true
git config --global user.signingkey YOUR_KEY_ID

# Make a commit - you'll be prompted for Windows Hello instead of a passphrase
git commit -m "My signed commit"
```

## Security

- **TPM Encryption**: Passphrases are encrypted using Windows Data Protection API with TPM backing, ensuring hardware-level security
- **Windows Hello**: Authentication requires biometric verification or secure PIN
- **Local Storage**: Encrypted passphrases are stored locally and never transmitted
- **DPAPI Fallback**: If TPM is unavailable, the application falls back to DPAPI with CurrentUser scope

## Command-Line Options

```
GpgWindowsHello [options]

Options:
  --setup      Run setup wizard and show configuration instructions
  --test       Test Windows Hello authentication
  --pinentry   Run in pinentry mode (used by GPG agent)
  --help       Show help message
```

## Troubleshooting

### Windows Hello authentication fails
- Ensure Windows Hello is configured in Windows Settings > Accounts > Sign-in options
- Try the test command: `GpgWindowsHello.exe --test`

### GPG still asks for passphrase
- Verify the `pinentry-program` line in `gpg-agent.conf` points to the correct path
- Restart the GPG agent: `gpgconf --kill gpg-agent`
- Check for typos in the configuration file

### "TPM not available" message
- The application will fall back to DPAPI, which is still secure
- This is normal on some systems or virtual machines without TPM

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by similar projects like [wsl-ssh-pageant](https://github.com/benpye/wsl-ssh-pageant)
- Uses Windows Hello APIs for biometric authentication
- Built with .NET and Windows Runtime APIs

