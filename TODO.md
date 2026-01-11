# TODO

## Security clarity

- Clarify that passphrase encryption prioritizes Windows `DataProtectionProvider` but is not guaranteed to be TPM-backed; `DPAPI (CurrentUser)` fallback may be used.
- Add a user-visible notice _before_ first-time passphrase storage that indicates the expected protection method on this machine (and that a fallback may be used if the preferred method fails).
- Add a note in the README about this behavior and how users can verify/change their protection method.
- Audit the codebase for any other places where security guarantees may be unclear or misleading, and improve clarity as needed.
