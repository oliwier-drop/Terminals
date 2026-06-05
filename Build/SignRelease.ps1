# Authenticode-sign release artifacts (MSI and main EXE) when a code-signing cert is configured.
# Requires Windows SDK signtool.exe (Visual Studio Build Tools or Windows SDK).
#
# Configure ONE of:
#   $env:CODE_SIGN_PFX_PATH + $env:CODE_SIGN_PFX_PASSWORD
#   $env:CODE_SIGN_CERT_THUMBPRINT  (cert in CurrentUser\My with private key)
#
# Optional:
#   $env:CODE_SIGN_TIMESTAMP_URL (default: http://timestamp.digicert.com)

param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,

    [string]$ExePath
)

function Get-SignToolPath {
    $candidates = @()
    if ($env:WindowsSdkDir) {
        $candidates += Join-Path $env:WindowsSdkDir "bin\x64\signtool.exe"
        $candidates += Join-Path $env:WindowsSdkDir "App Certification Kit\signtool.exe"
    }

    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $latest = Get-ChildItem $kitsRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d' } |
            Sort-Object { [version]$_.Name } -Descending |
            Select-Object -First 1
        if ($latest) {
            $candidates += Join-Path $latest.FullName "x64\signtool.exe"
            $candidates += Join-Path $latest.FullName "x86\signtool.exe"
        }
    }

    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "signtool.exe not found. Install Windows SDK or Visual Studio Build Tools."
}

function Invoke-SignFile {
    param(
        [string]$SignTool,
        [string]$FilePath,
        [string]$TimestampUrl
    )

    if (!(Test-Path $FilePath)) {
        throw "File to sign not found: $FilePath"
    }

    $args = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256", "/v")

    if ($env:CODE_SIGN_CERT_THUMBPRINT) {
        $args += @("/sha1", $env:CODE_SIGN_CERT_THUMBPRINT)
    }
    elseif ($env:CODE_SIGN_PFX_PATH) {
        if (!(Test-Path $env:CODE_SIGN_PFX_PATH)) {
            throw "CODE_SIGN_PFX_PATH not found: $($env:CODE_SIGN_PFX_PATH)"
        }
        $args += @("/f", $env:CODE_SIGN_PFX_PATH)
        if ($env:CODE_SIGN_PFX_PASSWORD) {
            $args += @("/p", $env:CODE_SIGN_PFX_PASSWORD)
        }
    }
    else {
        throw "Set CODE_SIGN_PFX_PATH or CODE_SIGN_CERT_THUMBPRINT before signing."
    }

    $args += $FilePath
    Write-Host "Signing $FilePath"
    & $SignTool @args
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath (exit $LASTEXITCODE)"
    }

    & $SignTool verify /pa $FilePath
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed for $FilePath"
    }
}

$timestampUrl = if ($env:CODE_SIGN_TIMESTAMP_URL) { $env:CODE_SIGN_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }
$signTool = Get-SignToolPath

Invoke-SignFile -SignTool $signTool -FilePath $MsiPath -TimestampUrl $timestampUrl
if ($ExePath -and (Test-Path $ExePath)) {
    Invoke-SignFile -SignTool $signTool -FilePath $ExePath -TimestampUrl $timestampUrl
}

Write-Host "Release signing completed."
