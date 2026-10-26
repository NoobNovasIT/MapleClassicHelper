# Đóng gói bản phát hành bằng Velopack: Setup.exe + gói cập nhật trong Releases/.
#
#   powershell -ExecutionPolicy Bypass -File build/publish.ps1 -Version 0.2.0
#   powershell -ExecutionPolicy Bypass -File build/publish.ps1 -Version 0.2.0 -Upload -Repo https://github.com/owner/MapleClassicHelper -Token <github token>
#
# -Upload đẩy Releases/ lên GitHub Releases của repo, app đã cài sẽ tự thấy bản mới.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Upload,
    [string]$Repo,
    [string]$Token
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$publishDir = Join-Path $root 'publish'
$releaseDir = Join-Path $root 'Releases'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore thất bại' }

# Self-contained: người dùng không cần cài .NET
dotnet publish src/MapleHelper.App/MapleHelper.App.csproj -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:PublishReadyToRun=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish thất bại' }

# Lấy bản cũ từ GitHub để vpk tạo được gói delta (cập nhật nhỏ hơn)
if ($Upload -and $Repo) {
    dotnet vpk download github --repoUrl $Repo --outputDir $releaseDir
}

dotnet vpk pack --packId MapleClassicHelper --packVersion $Version --packDir $publishDir `
    --mainExe MapleClassicHelper.exe --packTitle 'Maple Classic Helper' `
    --icon Assets/Icons/app.ico --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { throw 'vpk pack thất bại' }

if ($Upload) {
    if (-not $Repo -or -not $Token) { throw '-Upload cần -Repo và -Token' }
    dotnet vpk upload github --repoUrl $Repo --token $Token --outputDir $releaseDir `
        --publish --releaseName "Maple Classic Helper $Version" --tag "v$Version"
    if ($LASTEXITCODE -ne 0) { throw 'vpk upload thất bại' }
}

Write-Host "Xong. Bộ cài: $releaseDir\MapleClassicHelper-win-Setup.exe"
