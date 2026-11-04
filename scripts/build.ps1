$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet build
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    Push-Location 'src/transactions-ui'
    try {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'Frontend install failed.' }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    } finally { Pop-Location }
    $webRoot = Join-Path $projectRoot 'src/Transactions.Api/wwwroot'
    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Copy-Item -Path 'src/transactions-ui/dist/transactions-ui/browser/*' -Destination $webRoot -Recurse -Force
    Write-Host 'Build complete. Start the API and open http://localhost:5080.'
} finally { Pop-Location }
