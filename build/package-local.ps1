param(
    [string] $Version = "0.99.0",
    [string] $Channel = "canary",
    [string] $Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$Vpk = Get-Command vpk -ErrorAction SilentlyContinue

if (-not $Vpk) {
    Write-Error "Velopack CLI 'vpk' was not found. Install it first: dotnet tool install -g vpk --version 0.0.1298"
}

$env:DOTNET_ROLL_FORWARD = if ($env:DOTNET_ROLL_FORWARD) { $env:DOTNET_ROLL_FORWARD } else { "Major" }

$PublishDir = Join-Path $RootDir "artifacts/publish/$Runtime/$Version"
$OutputDir = Join-Path $RootDir "artifacts/velopack/$Runtime/$Version"
$Project = Join-Path $RootDir "src/QuickSType.UI/QuickSType.UI.csproj"
$MainExe = if ($Runtime.StartsWith("win-")) { "QuickSType.exe" } else { "QuickSType" }
$PublishAot = if ($env:QUICKSTYPE_PACKAGE_AOT) { $env:QUICKSTYPE_PACKAGE_AOT } else { "true" }

Remove-Item -Recurse -Force $PublishDir, $OutputDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $PublishDir, $OutputDir | Out-Null

dotnet publish $Project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishAot=$PublishAot `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -o $PublishDir `
    --nologo

vpk pack `
    --packId QuickSType `
    --packTitle QuickSType `
    --packVersion $Version `
    --channel $Channel `
    --runtime $Runtime `
    --packDir $PublishDir `
    --mainExe $MainExe `
    --outputDir $OutputDir

Write-Host "Velopack artifacts written to $OutputDir"
