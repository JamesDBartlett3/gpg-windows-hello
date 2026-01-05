# GpgWindowsHello v0.1.0 - Public Alpha Release

## 🎉 What is GpgWindowsHello?

GpgWindowsHello replaces traditional GPG passphrase entry with **Windows Hello biometric authentication**. Sign your Git commits with a fingerprint scan or facial recognition instead of typing passwords.

## ✨ Key Features

- **🔐 Windows Hello Integration** - Use fingerprint, face recognition, or PIN instead of typing passphrases
- **🛡️ TPM-Backed Security** - Passphrases encrypted with hardware-backed protection (CMS/PKCS#7)
- **🎯 Automatic Installation** - Double-click to install, automatically detects and configures all GPG installations
- **📦 Single-File Executable** - No dependencies, no installer packages, just one 135 MB EXE
- **🔍 Smart GPG Detection** - Finds both standalone GPG and Git-bundled GPG, offers to fix Git configuration issues
- **⚡ Desktop & Start Menu Shortcuts** - Automatically created during installation

## 📋 System Requirements

- **OS**: Windows 10 (build 19041+) or Windows 11
- **Windows Hello**: Fingerprint reader, IR camera, or PIN setup required
- **GPG**: Any version (standalone or Git-bundled)
- **Git**: Optional, for Git commit signing

## 🚀 Installation

1. **Download** `GpgWindowsHello.exe`
2. **Double-click** the executable
3. **Follow the prompts** to:
   - Select your GPG installation
   - Configure GPG agent settings
   - Verify Git GPG configuration (if applicable)
4. **Done!** The app installs to `%LOCALAPPDATA%\Programs\GpgWindowsHello` and adds itself to PATH

## 📖 Usage

### First-Time Setup

When you first sign a commit or use GPG:

1. GPG will launch GpgWindowsHello
2. **Authenticate with Windows Hello** (fingerprint/face/PIN)
3. **Enter your GPG passphrase** in the dialog (one-time only)
4. Your passphrase is securely stored with TPM encryption

### Subsequent Usage

Every future GPG operation that needs your passphrase:

1. **Authenticate with Windows Hello** - that's it!
2. No more typing passphrases

### Git Commit Signing

```bash
# Configure Git to sign commits
git config --global commit.gpgsign true
git config --global user.signingkey YOUR_KEY_ID

# Make a signed commit
git commit -m "Your message"
# → Windows Hello prompt appears
# → Authenticate and done!

# Verify the signature
git verify-commit HEAD
```

## 🔒 Security Notes

- **TPM Encryption**: Passphrases stored in `%APPDATA%\GpgWindowsHello\passphrases.dat` using Microsoft CMS/PKCS#7 with TPM backing
- **No Network Access**: Application operates entirely offline
- **Per-Machine Storage**: Encrypted passphrases are tied to your specific hardware
- **Windows Hello Required**: Every passphrase retrieval requires biometric authentication

## ⚠️ Known Limitations (Alpha Release)

- **Single Passphrase**: Currently stores one passphrase; multiple GPG key support may be added later if requested
- **Windows Only**: No macOS or Linux support (Windows Hello is Windows-specific)
- **File Size**: 135 MB due to self-contained .NET runtime; if size is a concern, please open an issue and we'll discuss whether to begin shipping a version without the self-contained .NET runtime.
- **No GUI Settings**: Configuration handled during installation; manual edits to `gpg-agent.conf` may be required for advanced users

## 🐛 Known Issues

- **First Auth Delay**: Initial Windows Hello authentication may take 10-15 seconds
- **No Passphrase Update UI**: To change stored passphrase, delete `%APPDATA%\GpgWindowsHello\passphrases.dat` and re-authenticate

## 🛠️ Troubleshooting

### GPG Not Finding GpgWindowsHello

Run the installer again:

```bash
GpgWindowsHello.exe
```

### Reset Stored Passphrase

Delete the encrypted storage file:

```bash
Remove-Item "$env:APPDATA\GpgWindowsHello\passphrases.dat"
```

### Check GPG Configuration

Your `gpg-agent.conf` should contain:

```
pinentry-program C:\Users\YourName\AppData\Local\Programs\GpgWindowsHello\GpgWindowsHello.exe
```

Restart GPG agent:

```bash
gpgconf --kill gpg-agent
```

## 📝 What's Next?

Planned for future releases:

- Multiple passphrase support (different passphrases per key)
- GUI for managing stored passphrases
- Smaller file size (AOT compilation)
- Automatic updates
- Per-key security policies

## 💬 Feedback

This is an **alpha release** - your feedback is invaluable!

- Found a bug? [Report it](https://github.com/JamesDBartlett3/gpg-windows-hello/issues)
- Have a feature request? [Let us know](https://github.com/JamesDBartlett3/gpg-windows-hello/issues)
- Security concern? [Contact us privately](https://github.com/JamesDBartlett3/gpg-windows-hello/security)

## 📜 License

Copyright © 2026 James D. Bartlett III

See [LICENSE](LICENSE) for details.

---

**Thank you for testing GpgWindowsHello!** 🙏
