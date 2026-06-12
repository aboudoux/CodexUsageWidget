param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "CodexUsageWidget\CodexUsageWidget.csproj"
$dist = Join-Path $root "dist"
$script = Join-Path $PSScriptRoot "CodexUsageWidget.iss"
$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$compiler = $compilerCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if (-not $compiler) {
    throw "Inno Setup 6 est introuvable. Installez JRSoftware.InnoSetup avec winget."
}

dotnet test (Join-Path $root "CodexUsageWidget.sln") -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Les tests ont echoue."
}

Get-Process CodexUsageWidget -ErrorAction SilentlyContinue | Stop-Process
Start-Sleep -Milliseconds 500

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $dist
if ($LASTEXITCODE -ne 0) {
    throw "La publication a echoue."
}

& $compiler $script
if ($LASTEXITCODE -ne 0) {
    throw "La creation de l'installateur a echoue."
}

Write-Host "Installateur cree dans $(Join-Path $root 'installer-output')"
