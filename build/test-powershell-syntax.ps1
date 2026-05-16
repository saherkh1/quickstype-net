$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$files = @(
    "build/package-local.ps1",
    "build/publish-windows.ps1"
)

foreach ($file in $files) {
    $path = Join-Path $RootDir $file
    $tokens = $null
    $errors = $null

    [System.Management.Automation.Language.Parser]::ParseFile(
        (Resolve-Path $path),
        [ref] $tokens,
        [ref] $errors
    ) | Out-Null

    if ($errors.Count -gt 0) {
        foreach ($errorRecord in $errors) {
            Write-Error ("{0}:{1}: {2}" -f $file, $errorRecord.Extent.StartLineNumber, $errorRecord.Message)
        }
        exit 1
    }
}

Write-Host "PowerShell release helper syntax test passed."
