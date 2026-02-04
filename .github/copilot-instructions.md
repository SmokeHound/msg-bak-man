# Copilot instructions (MsgBakMan)

## Overview
- Windows-only WPF app to import/merge/export SyncTech **SMS Backup & Restore** XML into a local SQLite “project folder”.
- Projects: `src/MsgBakMan.App` (WPF + CommunityToolkit.Mvvm + ModernWpfUI), `src/MsgBakMan.Core` (models/normalization/fingerprinting), `src/MsgBakMan.Data` (SQLite+Dapper+migrations), `src/MsgBakMan.ImportExport` (streaming XML + media), `installer/MsgBakMan.Installer` (WiX MSI).

## Entry points
- UI wiring: `src/MsgBakMan.App/MainWindow.xaml` + `MainWindow.xaml.cs` (sets `DataContext = new MainViewModel()`).
- Commands/state: `src/MsgBakMan.App/ViewModels/MainViewModel.cs` uses `[ObservableProperty]` + `[RelayCommand]`.
- Theme/resources: `src/MsgBakMan.App/App.xaml`, `App.xaml.cs`, `Styles/AppStyles.xaml`.

## Project folder & DB lifecycle
- `ProjectPaths` layout: `db/messages.sqlite`, `media/blobs/<sha256>`, `media/temp/`.
- Typical flow (see `MainViewModel`): `EnsureProjectInitialized()` → `new SqliteConnectionFactory(paths.DbPath).Open()` (PRAGMA `foreign_keys=ON`, WAL) → `new MigrationRunner().ApplyAll(conn)` → repositories/import/export.

## Data layer conventions
- Dapper raw SQL repos live in `src/MsgBakMan.Data/Repositories/*.cs` (no EF).
- Migrations are embedded SQL: `src/MsgBakMan.Data/Migrations/000*_*.sql`; `MigrationRunner` applies in lexical order and records `schema_migrations`.
- Import dedupe uses `MessageFingerprint.Version` + `ON CONFLICT(transport, fingerprint_version, fingerprint)` (see `ImportRepository.UpsertSms/UpsertMms`).

## Import/export & media
- Import is streaming + cancellation-aware: `SmsBackupRestoreXmlImporter` uses `XmlReader` over `Sha256HashingReadStream` and stores raw XML attrs as JSON (`raw_attrs_json`).
- Large imports use one long transaction: `ImportRepository.BeginImportSession()`.
- Media ingest: `MediaStore.IngestBase64AttributeAsync` streams base64 → temp file → SHA256 → moves to `media/blobs/<sha256>` (no extension); exporter re-streams blob bytes into `mms/part` `data` base64.

## Normalization & maintenance
- `PhoneNormalization.NormalizeAddress` is intentionally heuristic and affects conversation keys + dedupe.
- If normalization/fingerprint inputs change: bump `MessageFingerprint.Version` and review `PhoneNormalizationMaintenance`.

## Workflows
- Build/run: `dotnet restore .\MsgBakMan.sln`; `dotnet build .\MsgBakMan.sln -c Release`; `dotnet run --project .\src\MsgBakMan.App\MsgBakMan.App.csproj`.
- CI enforces whitespace: `dotnet format whitespace .\MsgBakMan.sln --verify-no-changes --no-restore`.
- Installer: `./build-installer.ps1 -Configuration Release [-Version 1.2.3]` (or build `installer\MsgBakMan.Installer\MsgBakMan.Installer.wixproj`).

## Repo note
- `tests/` is currently empty; don’t invent a test harness.
