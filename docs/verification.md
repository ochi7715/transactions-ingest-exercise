# Test results

Checked on November 3, 2026:

- Backend: 29 tests passed.
- Frontend: 5 tests passed.
- Both builds passed.
- `dotnet run` started the API on port 5080.
- `npm.cmd start` started Angular on port 4200. API requests worked.

Browser checks on November 1 covered imports, search, filters, and history. No console warnings or errors were found. The database model matched the migration.

Real source APIs and deployment have not been tested.

Run from the project folder:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```
