param(
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

Write-Host "Building MSI ($Configuration)..."

# Build the installer project. It publishes the WPF app to installer/MsgBakMan.Installer/publish automatically.
dotnet build "installer\MsgBakMan.Installer\MsgBakMan.Installer.wixproj" -c $Configuration -v minimal

Write-Host "Done. MSI is under installer/MsgBakMan.Installer/bin/$Configuration/x64/"
