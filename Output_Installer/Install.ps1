<#
.SYNOPSIS
    Automated installer script for MP3 Metadata Corrector.
#>
param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\Mp3MetadataCorrector",
    [switch]$CreateDesktopShortcut = $true,
    [switch]$CreateStartMenuShortcut = $true,
    [switch]$Launch = $true
)

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Installing MP3 Metadata Corrector..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$sourceZip = Join-Path $PSScriptRoot "..\installer\app_payload.zip"
if (-not (Test-Path $sourceZip)) {
    $sourceZip = Join-Path $PSScriptRoot "app_payload.zip"
}

if (-not (Test-Path $sourceZip)) {
    # If zip is not directly next to script, check publish directory
    $publishDir = Join-Path $PSScriptRoot "..\Mp3MetadataCorrector\publish"
    if (Test-Path $publishDir) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Copy-Item -Path "$publishDir\*" -Destination $InstallPath -Recurse -Force
    } else {
        Write-Error "Could not find payload archive or publish folder."
        exit 1
    }
} else {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($sourceZip, $InstallPath, $true)
}

$exePath = Join-Path $InstallPath "Mp3MetadataCorrector.exe"
$shell = New-Object -ComObject WScript.Shell

if ($CreateStartMenuShortcut) {
    $startMenuDir = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\MP3 Metadata Corrector"
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
    $shortcut = $shell.CreateShortcut((Join-Path $startMenuDir "MP3 Metadata Corrector.lnk"))
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "MP3 Metadata Corrector & YouTube MP3 Downloader"
    $shortcut.Save()
    Write-Host "  [OK] Start menu shortcut created" -ForegroundColor Green
}

if ($CreateDesktopShortcut) {
    $desktopDir = [Environment]::GetFolderPath("DesktopDirectory")
    $shortcut = $shell.CreateShortcut((Join-Path $desktopDir "MP3 Metadata Corrector.lnk"))
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "MP3 Metadata Corrector & YouTube MP3 Downloader"
    $shortcut.Save()
    Write-Host "  [OK] Desktop shortcut created" -ForegroundColor Green
}

# Register in Add/Remove Programs
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mp3MetadataCorrector"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "MP3 Metadata Corrector"
Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Bhushan Kamble"
Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $InstallPath
Set-ItemProperty -Path $regPath -Name "DisplayIcon" -Value $exePath
Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\Uninstall.ps1`""

# Write uninstaller script
$uninstallerScript = @"
`$installPath = "$InstallPath"
`$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "MP3 Metadata Corrector.lnk"
if (Test-Path `$desktopShortcut) { Remove-Item `$desktopShortcut -Force }
`$startMenuDir = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\MP3 Metadata Corrector"
if (Test-Path `$startMenuDir) { Remove-Item `$startMenuDir -Recurse -Force }
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Mp3MetadataCorrector" -Recurse -Force -ErrorAction SilentlyContinue
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 & rmdir /s /q `"`$installPath`"" -WindowStyle Hidden
Write-Host "MP3 Metadata Corrector uninstalled successfully."
"@
Set-Content -Path (Join-Path $InstallPath "Uninstall.ps1") -Value $uninstallerScript -Force

Write-Host "==============================================" -ForegroundColor Green
Write-Host " Installation completed successfully!" -ForegroundColor Green
Write-Host " Installed to: $InstallPath" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green

if ($Launch -and (Test-Path $exePath)) {
    Start-Process -FilePath $exePath -WorkingDirectory $InstallPath
}
