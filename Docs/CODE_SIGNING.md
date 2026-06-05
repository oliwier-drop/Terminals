# Code signing (MSI / EXE)

Windows shows **Unknown publisher** when the MSI is not signed with a **trusted Authenticode code-signing certificate**.

Changing the WiX `Manufacturer` string only affects Add/Remove Programs metadata. **SmartScreen and the UAC installer prompt require a valid signature.**

## What you need

1. **Code signing certificate** from a public CA (DigiCert, Sectigo, SSL.com, etc.)
   - **OV** (Organization Validation) — standard; may need reputation before SmartScreen stops warning
   - **EV** (Extended Validation) — faster SmartScreen trust; often requires hardware token
2. Certificate exported as `.pfx` **or** installed in `Cert:\CurrentUser\My` with private key
3. **signtool.exe** from [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) or Visual Studio Build Tools

Self-signed or `Terminals_TemporaryKey.pfx` (legacy ClickOnce) **do not** remove the warning for end users.

## Sign a release build

From `Build\` after `Build.ps1` (or with `-Sign`):

```powershell
$env:CODE_SIGN_PFX_PATH = "C:\certs\olivier-drop-codesign.pfx"
$env:CODE_SIGN_PFX_PASSWORD = "your-password"
.\Build.ps1 -Sign
```

Or with a certificate already in the user store:

```powershell
$env:CODE_SIGN_CERT_THUMBPRINT = "THUMBPRINT_WITHOUT_SPACES"
.\Build.ps1 -Sign
```

Verify:

```powershell
signtool verify /pa .\Output\TerminalsSetup_1.0.1.msi
```

## Publisher name shown to users

The trusted publisher label comes from the **certificate Subject** (e.g. `CN=Oliwier Drop`). Align it with:

- `AssemblyCompany` in `Common.AssemblyInfo.cs` → `Oliwier Drop`
- WiX `Manufacturer` in `Product.wxs` → `Oliwier Drop`

## CI / GitHub Actions (optional)

Store the PFX as a secret (`CODE_SIGN_PFX_BASE64`, `CODE_SIGN_PFX_PASSWORD`) and run `SignRelease.ps1` after `PackOutput.ps1` on release tags.
