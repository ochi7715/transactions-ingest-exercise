# Transaction Import

This app imports sample transactions from Store Uptown, Store Downtown, and Online. It uses ASP.NET Core 10, Angular, and SQLite, and keeps a history of changes.

## Requirements

- .NET SDK 10.0.403 or a compatible patch
- Node.js compatible with Angular 22
- npm

Tested with Node 26.7.0 and npm 11.6.2.

## Build and run

From the project folder:

```powershell
.\scripts\build.ps1
cd src/Transactions.Api
dotnet run
```

Open **http://localhost:5080**. Stop with **Ctrl+C**.

## Tests

From the project folder, after building:

```powershell
.\scripts\test.ps1
```

## Demo

Start with a fresh database. Select a scenario and click **Import now**.

Click **View** to open a transaction's history in a modal.

| Scenario | Result |
|---|---|
| baseline | Adds 9 transactions. |
| baseline again | No changes or duplicate history. |
| corrected | Adds 3 transactions and updates 3 amounts. |
| missing | Revokes Uptown 100002; the older missing record stays active. |
| missing again | No duplicate revocation entry. |
| restored | Makes Uptown 100002 active again. |
| failure | Online fails; other sources can still import. |
| incomplete | Existing records stay unchanged. |

Run the recent-record demo within 24 hours of the first import. For a fresh demo, stop the app and change the database filename in `appsettings.json`.

## Settings

File: `src/Transactions.Api/appsettings.json`

- `ConnectionStrings:Transactions`: database filename
- `Import:RevocationHours`: revocation window, default 24 hours

## API

| Method | Address |
|---|---|
| POST | `/api/import` |
| GET | `/api/transactions` |
| GET | `/api/transactions/{id}/audit` |
| GET | `/api/import/runs` |
| GET | `/api/import/scenarios` |
| GET | `/api/health` |

## Notes

- [Design decisions and assumptions](docs/design-decisions.md)
- [Test results](docs/verification.md)
- [Work hours](docs/time-log.md)

Local demo only. Uses sample sources, has no sign-in, and supports one backend instance per database.

## AI usage

AI helped with documentation, logging, application code, and tests. See [AI usage notes](docs/ai-usage.md).
