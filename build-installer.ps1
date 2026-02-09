param(
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release',

  [string]$Version
)

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  MsgBakMan Installer Build" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# If no version is supplied, auto-generate a valid Windows Installer ProductVersion.
# MSI ProductVersion format is Major.Minor.Build where:
# - Major/Minor are typically <= 255
# - Build is <= 65535
# This generates: 1.(YY).(DayOfYear*100 + QuarterHourOfDay)
if (-not $Version) {
  $now = Get-Date
  $yy = $now.Year - 2000
  if ($yy -lt 0) { $yy = 0 }
  if ($yy -gt 255) { $yy = 255 }

  $quarterHour = [int][math]::Floor((($now.Hour * 60) + $now.Minute) / 15)
  $build = ($now.DayOfYear * 100) + $quarterHour
  if ($build -gt 65535) { $build = 65535 }

  $Version = "1.$yy.$build"
  Write-Host "Version:       $Version (auto)" -ForegroundColor Yellow
} else {
  Write-Host "Version:       $Version" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Building MSI installer..." -ForegroundColor Green

# Build the installer project. It publishes the WPF app to installer/MsgBakMan.Installer/publish automatically.
$versionArgs = @()
if ($Version) {
  $versionArgs += "-p:Version=$Version"
  $versionArgs += "-p:ProductVersion=$Version"
}

dotnet build "installer\MsgBakMan.Installer\MsgBakMan.Installer.wixproj" -c $Configuration -v minimal @versionArgs

if ($LASTEXITCODE -eq 0) {
  Write-Host ""
  Write-Host "==================================================" -ForegroundColor Green
  Write-Host "  Build completed successfully!" -ForegroundColor Green
  Write-Host "==================================================" -ForegroundColor Green
  Write-Host "MSI Location: installer\MsgBakMan.Installer\bin\x64\$Configuration\" -ForegroundColor Cyan
  Write-Host ""
  
  # List the MSI files
  $msiPath = "installer\MsgBakMan.Installer\bin\x64\$Configuration"
  if (Test-Path $msiPath) {
    Get-ChildItem -Path $msiPath -Filter "*.msi" | ForEach-Object {
      Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor White
    }
  }
  Write-Host ""
} else {
  Write-Host ""
  Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
  exit $LASTEXITCODE
}
