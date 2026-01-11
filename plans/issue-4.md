## Plan: Add SSH key support (Issue #4)

Add first-class SSH auth support by enabling GnuPG’s SSH agent mode during setup, surfacing the correct `agent-ssh-socket` path, and documenting how to point `SSH_AUTH_SOCK` at it (e.g., for GitHub). This should preserve the existing “Windows Hello gates key use” model while making `ssh`/Git-over-SSH work reliably.

### Steps

1. Add an “Enable SSH support” option to the setup flow in [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs) (reusing the existing setup wizard pattern).
2. Update config management in [src/GpgWindowsHello/GpgAgentManager.cs](../src/GpgWindowsHello/GpgAgentManager.cs) to write `enable-ssh-support` into the managed `gpg-agent.conf` alongside existing settings.
3. Extend agent introspection in [src/GpgWindowsHello/GpgAgentManager.cs](../src/GpgWindowsHello/GpgAgentManager.cs) to run `gpgconf --list-dirs agent-ssh-socket` and capture the returned socket path.
4. In [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs), show copy/paste instructions for PowerShell and CMD to set `SSH_AUTH_SOCK` and restart the agent (`gpgconf --kill gpg-agent`).
5. Add a `--test-ssh` (or extend existing `--test`) path in [src/GpgWindowsHello/Program.cs](../src/GpgWindowsHello/Program.cs) to validate `SSH_AUTH_SOCK` and print actionable remediation steps if misconfigured.
6. Update [README.md](../README.md) with a “GitHub SSH” section: enablement steps, verification (`ssh -T git@github.com`), and conflict guidance with Windows OpenSSH `ssh-agent`.

### Further Considerations

1. Decide how to handle conflicts with OpenSSH `ssh-agent` (warn + link to docs vs detect + offer choice).
2. Clarify support scope: “GPG keys exposed as SSH keys” vs “standard OpenSSH keys” (and document accordingly).
