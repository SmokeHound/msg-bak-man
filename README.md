# Message Backup Manager (MsgBakMan)

WPF desktop app for importing, merging, and exporting **SMS Backup & Restore** XML backups.

## Features

- Import SMS Backup & Restore XML into a local project database
- Browse conversations and messages
- Merge conversations / normalize phone numbers
- Export messages back to SMS Backup & Restore XML
- Export media (project media folder)

## Requirements

- Windows (WPF)
- .NET SDK 10

## Quick start

```powershell
# from repo root

dotnet restore .\MsgBakMan.sln

dotnet build .\MsgBakMan.sln -c Release

# Run the app (Debug)
dotnet run --project .\src\MsgBakMan.App\MsgBakMan.App.csproj
```

## Solution layout

- `MsgBakMan.sln` — solution
- `src/MsgBakMan.App` — WPF UI
- `src/MsgBakMan.Core` — domain/model logic
- `src/MsgBakMan.Data` — data access/repositories
- `src/MsgBakMan.ImportExport` — import/export implementation
- `tools/IconGen` — small tool for generating app icon assets

## CI

GitHub Actions workflows:

- `.github/workflows/dotnet-desktop.yml` — builds solution, verifies formatting, and creates MSI installer artifacts
- `.github/workflows/release.yml` — creates GitHub releases with MSI installer when tags are pushed (v*.*.*)
- `.github/workflows/nuget-pack.yml` — packs NuGet packages for the library projects (and can optionally publish to GitHub Packages)
- `.github/workflows/codeql.yml` — security analysis using CodeQL (runs on push/PR and weekly)
- `.github/workflows/dependency-review.yml` — scans for vulnerable dependencies in pull requests

## Contributing

- Keep commits focused (repo-hygiene vs feature changes)
- Prefer adding tests when you add non-trivial logic (if/when a test project exists)

## License

No license has been specified in this repository.
