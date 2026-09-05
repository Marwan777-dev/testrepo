# start-dev.ps1 — Co-launches the Nabadat frontend (Vite dev server) alongside the
# backend. Invoked by an MSBuild target in src/Nabadat.TenantAdmin/Nabadat.TenantAdmin.csproj
# whenever the backend is built inside Visual Studio (Debug). Idempotent: if the dev
# server is already listening it does nothing, so repeated builds don't spawn duplicates.

param(
    [int]$Port = 5173,
    # Defaults to this script's own folder (the frontend workspace root).
    [string]$FrontendDir = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

# 1. Already running? Bail out so we never start a second dev server.
try {
    $listening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listening) {
        Write-Host "[start-dev] Vite already listening on port $Port - skipping launch."
        exit 0
    }
} catch {
    # Get-NetTCPConnection unavailable for some reason; fall through and just launch.
}

# 2. Make sure dependencies exist (first checkout / fresh clone).
if (-not (Test-Path (Join-Path $FrontendDir 'node_modules'))) {
    Write-Host "[start-dev] node_modules missing - running 'npm install' first..."
    Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', 'npm install' `
        -WorkingDirectory $FrontendDir -NoNewWindow -Wait
}

# 3. Launch the dev server in its own console window so its logs stay visible and it
#    outlives the MSBuild process (Start-Process is non-blocking, so the build won't hang).
Write-Host "[start-dev] Starting Vite dev server (npm run dev) in $FrontendDir ..."
Start-Process -FilePath 'cmd.exe' `
    -ArgumentList '/k', 'title Nabadat Frontend (Vite) && npm run dev' `
    -WorkingDirectory $FrontendDir | Out-Null

exit 0
