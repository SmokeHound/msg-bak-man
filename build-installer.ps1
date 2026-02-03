param(
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release',

  [string]$Version
)

$ErrorActionPreference = 'Stop'

Write-Host "Building MSI ($Configuration)..."

# Build the installer project. It publishes the WPF app to installer/MsgBakMan.Installer/publish automatically.
$versionArgs = @()
if ($Version) {
  $versionArgs += "-p:Version=$Version"
  $versionArgs += "-p:ProductVersion=$Version"
}

dotnet build "installer\MsgBakMan.Installer\MsgBakMan.Installer.wixproj" -c $Configuration -v minimal @versionArgs

Write-Host "Done. MSI is under installer/MsgBakMan.Installer/bin/$Configuration/x64/"
