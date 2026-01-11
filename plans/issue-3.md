## Plan: Reduce false positives from MITRE ATT&CK behavioral signatures (Issue #3)

Reduce AV/EDR false positives by removing suspicious behaviors (temp-file logging, forced process-tree killing, brittle COM automation), adding stronger app identity signals (manifest + metadata), and documenting expected runtime behavior and mitigations. Keep runtime behavior functionally equivalent for users.

### Steps

1. Add an application manifest (asInvoker + Windows 10/11 compatibility entries) and wire it into [src/GpgWindowsHello/GpgWindowsHello.csproj](../src/GpgWindowsHello/GpgWindowsHello.csproj) (e.g., `ApplicationManifest`).
2. Remove temp-directory logging in [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs) and [src/GpgWindowsHello/GpgAgentManager.cs](../src/GpgWindowsHello/GpgAgentManager.cs); if diagnostics are needed, add an explicit opt-in debug mode instead.
3. Update the GPG key import timeout behavior in [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs) to avoid forced process-tree termination; implement a graceful shutdown path first, with a documented last-resort fallback.
4. Harden shortcut creation in [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs) by adding explicit error handling and deterministic COM cleanup around `WScript.Shell` usage (ensure objects are released even on exceptions).
5. Add assembly/app metadata (publisher/product/security intent) via [src/GpgWindowsHello/GpgWindowsHello.csproj](../src/GpgWindowsHello/GpgWindowsHello.csproj) and/or a dedicated assembly info file, consistent with the issue guidance.
6. Update [README.md](../README.md) with an “AV false positives” section: what the app does, why it might trigger, and Defender exclusion guidance.

### Further Considerations

1. If removing temp logs hurts troubleshooting, choose: CLI `--debug` flag vs Windows Event Log vs no logging by default.
2. Confirm manifest/metadata changes don’t affect publish options (single-file, win-x64, net10).
3. Ensure that when debugging is enabled, sensitive info (like GPG passphrases) is never logged.

### Documentation Updates

1. In [README.md](../README.md), add a section that explains how to use the new debug mode, and how to interpret any logs it generates.

### Final Notes

- Verify that everything outlined in [the issue on GitHub](https://api.github.com/repos/JamesDBartlett3/gpg-windows-hello/issues?state=open&per_page=100) has been addressed.
