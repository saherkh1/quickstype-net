# Publish QuickSType for Windows x64 as a single AOT exe.
$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$Out = Join-Path $Root "publish\win-x64"

Write-Host "==> Publishing for win-x64 (AOT)"
Remove-Item -Recurse -Force $Out -ErrorAction SilentlyContinue

try {
    dotnet publish "$Root\src\QuickSType.UI" `
        -r win-x64 -c Release `
        -p:PublishAot=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $Out
}
catch {
    Write-Warning "AOT publish failed; retrying as self-contained without AOT"
    Remove-Item -Recurse -Force $Out -ErrorAction SilentlyContinue
    dotnet publish "$Root\src\QuickSType.UI" `
        -r win-x64 -c Release `
        --self-contained true `
        -p:PublishAot=false `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $Out
}

Write-Host "==> Done: $Out"
