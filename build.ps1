param (
    [string]$GameDir = "",
    [switch]$InstallToGame
)

$ErrorActionPreference = "Stop"

# Auto-detect SimplePlanes 2 directory if not specified
if (-not $GameDir) {
    $possiblePaths = @(
        "E:\SteamLibrary\steamapps\common\SimplePlanes 2",
        "D:\SteamLibrary\steamapps\common\SimplePlanes 2",
        "C:\Program Files (x86)\Steam\steamapps\common\SimplePlanes 2",
        "D:\Steam\steamapps\common\SimplePlanes 2",
        "E:\Steam\steamapps\common\SimplePlanes 2"
    )
    foreach ($p in $possiblePaths) {
        if (Test-Path (Join-Path $p "SimplePlanes 2.exe")) {
            $GameDir = $p
            break
        }
    }
}

if (-not $GameDir -or -not (Test-Path $GameDir)) {
    Write-Error "SimplePlanes 2 directory not found. Please specify -GameDir <path>"
    exit 1
}

Write-Host "Target Game Directory: $GameDir"

$managed = Join-Path $GameDir "SimplePlanes 2_Data\Managed"
$bepCore = Join-Path $GameDir "BepInEx\core"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $PSScriptRoot "LandingRateMonitor.cs"
$outDir = Join-Path $PSScriptRoot "artifacts"
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$outputDll = Join-Path $outDir "LandingRateMonitor.dll"

$refMscorlib = Join-Path $managed "mscorlib.dll"
$refNetstandard = Join-Path $managed "netstandard.dll"
$refSystem = Join-Path $managed "System.dll"
$refCore = Join-Path $managed "System.Core.dll"
$refGame = Join-Path $managed "Game.dll"
$refBep = Join-Path $bepCore "BepInEx.dll"
$refUnity = Join-Path $managed "UnityEngine.dll"
$refUnityCore = Join-Path $managed "UnityEngine.CoreModule.dll"
$refUnityIMGUI = Join-Path $managed "UnityEngine.IMGUIModule.dll"
$refUnityPhysics = Join-Path $managed "UnityEngine.PhysicsModule.dll"
$refUnityInput = Join-Path $managed "UnityEngine.InputLegacyModule.dll"
$refUnityText = Join-Path $managed "UnityEngine.TextRenderingModule.dll"

$cmdArgs = @(
    "/noconfig",
    "/nostdlib+",
    "/target:library",
    "/out:$outputDll",
    "/optimize+",
    "/nowarn:0618",
    "/platform:x64",
    "/reference:$refMscorlib",
    "/reference:$refNetstandard",
    "/reference:$refSystem",
    "/reference:$refCore",
    "/reference:$refGame",
    "/reference:$refBep",
    "/reference:$refUnity",
    "/reference:$refUnityCore",
    "/reference:$refUnityIMGUI",
    "/reference:$refUnityPhysics",
    "/reference:$refUnityInput",
    "/reference:$refUnityText",
    $source
)

Write-Host "Compiling LandingRateMonitor.dll (v4.0.0 Clean Architecture)..."
& $csc $cmdArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compilation failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "[SUCCESS] Compiled to $outputDll"

if ($InstallToGame) {
    $pluginDir = Join-Path $GameDir "BepInEx\plugins\LandingRateMonitor"
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }
    Copy-Item -Path $outputDll -Destination (Join-Path $pluginDir "LandingRateMonitor.dll") -Force
    Write-Host "[SUCCESS] Installed to game plugin directory: $pluginDir"
}
