# GpgWindowsHello v0.1.0 - Public Alpha Release

## What is GpgWindowsHello?

GpgWindowsHello replaces traditional GPG passphrase entry with **Windows Hello biometric authentication**. Sign your Git commits with a fingerprint scan or facial recognition instead of typing passwords.

## Key Features

- **Windows Hello Integration** - Use fingerprint, face recognition, or PIN instead of typing passphrases
- **Encrypted Passphrase Storage** - Encrypted at rest using Windows protection APIs (DataProtectionProvider preferred; DPAPI fallback)
- **Automatic Installation** - Double-click to install, automatically detects and configures all GPG installations
- **Single-File Executable** - No dependencies, no installer packages, just one self-contained EXE
- **Smart GPG Detection** - Finds both standalone GPG and Git-bundled GPG, offers to fix Git configuration issues

## System Requirements

- **OS**: Windows 10 (build 19041+) or Windows 11
- **Windows Hello**: Fingerprint reader, IR camera, or PIN setup required
- **GPG**: Any version (standalone or Git-bundled)
- **Git**: Optional, for Git commit signing

## Installation

1. **Download** `GpgWindowsHello.exe` and `install.cmd` (from the GitHub release assets)
2. **Right-click** `install.cmd` and select **Run as administrator** (or just double-click it)
3. **Follow the prompts** to:
   - Select your GPG installation
   - If your GPG installation isn't detected (or you want to add another), use `m` to manually enter the full path to `gpg.exe` or `b` to browse for it
   - Configure GPG agent settings
   - Verify Git GPG configuration (if applicable)
4. **Done!** The app copies itself to `%LOCALAPPDATA%\Programs\GpgWindowsHello` and configures GPG.

## Usage

### First-Time Setup

When you first sign a commit or use GPG:

1. GPG will launch GpgWindowsHello
2. **Authenticate with Windows Hello** (fingerprint/face/PIN)
3. **Enter your GPG passphrase** in the dialog (one-time only)
4. Your passphrase is securely stored (DataProtectionProvider preferred; DPAPI fallback)

If you need to import a private key during setup, you can paste the key content or type `b` to browse for `private-key.asc`.

### Subsequent Usage

Every future GPG operation that needs your passphrase:

1. **Authenticate with Windows Hello** - that's it!
2. No more typing passphrases

### Git Commit Signing

```powershell
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

## Security Notes

- **Passphrase encryption (prioritized, not guaranteed TPM-backed)**: Passphrases are stored in `%APPDATA%\GpgWindowsHello\gpg-auth.bin` encrypted using Windows `DataProtectionProvider` when available, with a Windows DPAPI (`CurrentUser`) fallback.
- **No Network Access**: Application operates entirely offline
- **Per-user scope**: Encrypted passphrases are scoped to your Windows user profile and aren’t intended to be portable.
- **Windows Hello Required**: Every passphrase retrieval requires Windows Hello authentication, which can be biometric or PIN-based

## AV false positives

Some AV/EDR products (and VirusTotal) may flag this binary due to behaviors that resemble malware techniques, even though they are used here for legitimate installation and credential-gating purposes:

- Spawning `gpg` / `gpgconf` processes
- Encrypted credential storage on disk

### Sandbox false positives (CAPE, Zenbox, etc.)

Dynamic analysis sandboxes may report MITRE ATT&CK signatures such as:

| Signature | Reason | Explanation |
|-----------|--------|-------------|
| T1497 (Virtualization/Sandbox Evasion) | Guard pages, memory write watch | Normal .NET runtime memory management (garbage collector, JIT). Not evasion behavior. |
| T1106 (Native API) | Windows API calls | Required for Windows Hello authentication (`UserConsentVerifier`), data protection APIs. |
| T1082 (System Information Discovery) | Console redirection checks | The app checks `Console.IsInputRedirected` to detect if invoked by GPG agent vs interactively. |
| T1562 (Impair Defenses) | Cryptographic API usage | Windows `DataProtectionProvider` / DPAPI for secure passphrase storage. |

These behaviors are inherent to legitimate credential management applications and the .NET runtime. They do not indicate malicious activity.

### Mitigation

If Microsoft Defender (or another product) blocks the app, you can add an exclusion for the install folder:

1. Open **Windows Security** → **Virus & threat protection**
2. Go to **Manage settings** → **Exclusions** → **Add or remove exclusions**
3. Add a **Folder** exclusion for `%LOCALAPPDATA%\Programs\GpgWindowsHello`

Optional debug logging (no files written): set `GPGWINDOWSHELLO_DEBUG=1` and run the app from a console to see diagnostic messages on stderr.

## Create shortcuts (optional)

This build does not create Desktop or Start Menu shortcuts automatically.

Desktop shortcut (PowerShell one-liner):

```powershell
$exe="$env:LOCALAPPDATA\Programs\GpgWindowsHello\GpgWindowsHello.exe"; $w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut("$([Environment]::GetFolderPath('Desktop'))\GpgWindowsHello.lnk"); $s.TargetPath=$exe; $s.Arguments='--help'; $s.WorkingDirectory=(Split-Path $exe); $s.Description='GpgWindowsHello - Windows Hello for GPG'; $s.Save()
```

Start Menu shortcut (PowerShell one-liner):

```powershell
$exe="$env:LOCALAPPDATA\Programs\GpgWindowsHello\GpgWindowsHello.exe"; $dir=Join-Path ([Environment]::GetFolderPath('Programs')) 'GpgWindowsHello'; New-Item -ItemType Directory -Force -Path $dir | Out-Null; $w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut("$dir\GpgWindowsHello.lnk"); $s.TargetPath=$exe; $s.Arguments='--help'; $s.WorkingDirectory=(Split-Path $exe); $s.Description='GpgWindowsHello - Windows Hello for GPG'; $s.Save()
```

## Debug mode

To enable diagnostic output (written to stderr; no log files are created):

- PowerShell: `setx GPGWINDOWSHELLO_DEBUG 1` (new shells only)
- Cmd: `setx GPGWINDOWSHELLO_DEBUG 1` (new shells only)

What you’ll see:

- Basic lifecycle and command processing logs
- Storage protection notice during first-time setup (DataProtectionProvider preferred; DPAPI fallback when needed)

What you won’t see:

- GPG passphrases (never logged)

## Known Limitations (Alpha Release)

- **Single Passphrase**: Currently stores one passphrase; multiple GPG key support may be added later if requested
- **Windows Only**: No macOS or Linux support (Windows Hello is Windows-specific)
- **File Size**: Large(ish) due to self-contained .NET runtime; if size is a concern, please open an issue and we'll discuss whether to begin shipping a version without the self-contained .NET runtime.
- **No GUI Settings**: Configuration handled during installation; manual edits to `gpg-agent.conf` may be required for advanced users

## Known Issues

- **First Auth Delay**: Initial Windows Hello authentication may take 10-15 seconds
- **No Passphrase Update UI**: To change stored passphrase, delete `%APPDATA%\GpgWindowsHello\gpg-auth.bin` and re-authenticate

## Troubleshooting

### GPG Not Finding GpgWindowsHello

Run the installer again:

```powershell
& $env:LOCALAPPDATA\Programs\GpgWindowsHello\GpgWindowsHello.exe --setup
```

### Reset Stored Passphrase

Delete the encrypted storage file:

```powershell
Remove-Item "$env:APPDATA\GpgWindowsHello\gpg-auth.bin"
```

### Check GPG Configuration

Your `gpg-agent.conf` should contain:

```powershell
pinentry-program C:\Users\YourName\AppData\Local\Programs\GpgWindowsHello\GpgWindowsHello.exe
```

Restart GPG agent:

```powershell
gpgconf --kill gpg-agent
```

## What's Next?

Planned for future releases:

- Multiple passphrase support (different passphrases per key)
- GUI for managing stored passphrases
- Smaller file size (AOT compilation)
- Automatic updates
- Per-key security policies

## Feedback

This is an **alpha release** - your feedback is invaluable!

- Have a feature request, found a bug, or have a security concern? [Let us know](https://github.com/JamesDBartlett3/gpg-windows-hello/issues)

## License

Copyright © 2026 James D. Bartlett III

See [LICENSE](LICENSE) for details.

---

**Thank you for testing GpgWindowsHello!**
