# Copilot Instructions

This file contains instructions for GitHub Copilot to help it serve as a better coding assistant. Please do not edit or delete this file unless you are familiar with how GitHub Copilot uses it.

## GpgWindowsHello

### Semantic Versioning

- Follow the latest [Semantic Versioning](https://semver.org/) spec for all version numbers in this project.
- Whenever the version number is incremented somewhere, make sure to increment it everywhere (assembly info, installer script, README, etc.) to keep them in sync.

### Documentation

- When modifying the code, ensure that all functionality and behaviors are clearly documented. When the change affects the user experience, update the README.md file accordingly.
- When adding new features, include usage examples in the README.md file.
- When working with markdown files, do not insert emojis, emoticons, or other unprofessional elements.

### Source Control

#### Commit Messages

- Dos:

  - Use clear and concise language that accurately describes the changes made in the staged files.
  - Follow the conventional commit format: `<type>(<scope>): <description>`, where `<type>` is one of `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, or `chore`.
  - When creating branches, use descriptive names that reflect the purpose of the branch, such as `feature/<feature-name>`, `bugfix/<bug-description>`, or `docs/<documentation-topic>`.

- Don'ts:

  - When composing a commit message, do NOT speculate as to the intent behind, or purpose of, the staged changes. Simply state _what_ was changed, and do NOT attempt to explain _why_ it was changed (E.g., do NOT include phrases like "to improve performance", "to fix a bug", "for improved clarity", "for enhanced UX", etc.). The reasoning behind changes can be discussed in code review comments, but should NOT be included in commit messages.
