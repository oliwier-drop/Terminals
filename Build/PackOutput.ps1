param(
    [switch]$Sign
)

[String[]]$packageRelativePaths = .\PackageFiles.ps1

$outputDir = ".\Output\"
$binOutput = Join-Path $outputDir "Release"
$commonAssembly = "..\Source\Terminals\Properties\Common.AssemblyInfo.cs"
$setupPath = Join-Path $binOutput "TerminalsSetup.msi"

function Get-MsBuildPath {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $fromVswhere = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($fromVswhere -and (Test-Path $fromVswhere)) {
            return $fromVswhere
        }
    }

    $buildTools = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path $buildTools) {
        return $buildTools
    }

    return "c:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"
}

$null = Get-MsBuildPath

$versionLine = (Select-String -path $commonAssembly "AssemblyVersion").Line
$versionStart = $versionLine.IndexOf("(""")
$versionEnd = $versionLine.IndexOf(".*")
$version = $versionLine.SubString($versionStart + 2, $versionEnd - $versionStart - 2)

$installerName = "TerminalsSetup_$version.msi"
$zipName = "Terminals_v$version.zip"
$zipPath = Join-Path $outputDir $zipName
$installerTargetPath = Join-Path $outputDir $installerName

if (!(Test-Path $outputDir)) {
    New-Item $outputDir -ItemType Directory | Out-Null
}

Get-ChildItem $outputDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

if (!(Test-Path $setupPath)) {
    throw "MSI not found at $setupPath. Build DistributionRelease first."
}

Move-Item $setupPath $installerTargetPath -Force

$packageFullPaths = @()
foreach ($relativePath in $packageRelativePaths) {
    $packageFullPaths += Join-Path $binOutput $relativePath
}

if (Get-Command Write-Zip -ErrorAction SilentlyContinue) {
    Get-ChildItem $packageFullPaths -Recurse | Write-Zip -IncludeEmptyDirectories -EntryPathRoot $binOutput -OutputPath $zipPath
}
else {
    $staging = Join-Path $outputDir "_zipstage"
    if (Test-Path $staging) {
        Remove-Item $staging -Recurse -Force
    }

    New-Item $staging -ItemType Directory | Out-Null
    foreach ($relativePath in $packageRelativePaths) {
        $source = Join-Path $binOutput $relativePath
        if (!(Test-Path $source)) {
            Write-Warning "Skipping missing ZIP file: $relativePath"
            continue
        }

        $target = Join-Path $staging $relativePath
        $targetDir = Split-Path $target -Parent
        if (!(Test-Path $targetDir)) {
            New-Item $targetDir -ItemType Directory -Force | Out-Null
        }

        Copy-Item $source $target -Force
    }

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
    Remove-Item $staging -Recurse -Force
}

if ($Sign -or $env:CODE_SIGN_PFX_PATH -or $env:CODE_SIGN_CERT_THUMBPRINT) {
    $exePath = Join-Path $binOutput "Terminals.exe"
    .\SignRelease.ps1 -MsiPath $installerTargetPath -ExePath $exePath
}

Write-Host "Release artifacts:"
Write-Host "  $installerTargetPath"
Write-Host "  $zipPath"
