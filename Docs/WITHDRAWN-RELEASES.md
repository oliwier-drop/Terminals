# Withdrawn releases (do not download)

These GitHub releases are **broken for SSH** and must not be used. Install **[v1.0.7](RELEASE-v1.0.7-notes.md)** or newer instead.

| Version | Problem | Status |
|---------|---------|--------|
| **v1.0.5** | SkiaSharp runtime not included in MSI/ZIP → SSH terminal blank (gray square) | Withdrawn |
| **v1.0.6** | Skia natives in wrong path (`runtimes\...` instead of `x64\`) → SSH still blank | Withdrawn |

## GitHub housekeeping

On [GitHub Releases](https://github.com/oliwier-drop/Terminals/releases):

1. Edit **v1.0.5** and **v1.0.6** — check **Set as a pre-release**, add a warning at the top of the description linking here.
2. **Delete** `TerminalsSetup_*.msi` and `Terminals_v*.zip` assets from those releases (optional but recommended).
3. Publish **v1.0.7** as the **latest** stable release with MSI + ZIP attached.

The in-app update checker skips `prerelease` entries on the GitHub API, so marking withdrawn builds as pre-release stops the app from suggesting them.
