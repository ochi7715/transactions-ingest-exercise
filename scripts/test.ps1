$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet test
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    Push-Location 'src/transactions-ui'
    try {
        npm.cmd test -- --watch=false
        if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }
