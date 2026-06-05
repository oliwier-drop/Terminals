#The ultimate build script to build release version of Terminals
#.\InstallPrerequisities.ps1;
param(
    [switch]$Sign
)

$logFile = "Output\build.log";

if(Test-Path .\Output) {
    Remove-Item .\Output\* -Recurse -ErrorAction Stop | Tee-Object $logFile;
}

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

$msbuild = Get-MsBuildPath
# Compile the solution - the distributionrelease configuration contains installer, which is not normal configurations
& $msbuild "..\Source\Terminals.sln" /m /p:configuration=DistributionRelease "/p:Platform=Any CPU" /toolsversion:4.0 /t:rebuild | Tee-Object $logFile -Append;
 
if ($Sign) {
    .\PackOutput.ps1 -Sign | Tee-Object $logFile -Append;
}
else {
    .\PackOutput.ps1 | Tee-Object $logFile -Append;
}


exit $LastExitCode;