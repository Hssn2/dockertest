#Requires -RunAsAdministrator
<#
  dockertest Agent - Windows sunucu kurulumu (repo gerektirmez)
  Docker + PostgreSQL + Agent container (port 8080)
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = "",
    [switch]$SkipDocker,
    [switch]$SkipPostgres,
    [switch]$SkipCatalog,
    [switch]$SkipAgent,
    [switch]$SkipAutoDeploy
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $ScriptDir "config.json"
}

if (-not (Test-Path $ConfigPath)) {
    throw "config.json bulunamadi: $ConfigPath"
}

$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json

$AgentImage = [string]$config.AgentImage
$AgentPort = [int]$config.AgentPort
$AppHostPort = [int]$config.AppHostPort
$AgentContainerName = [string]$config.AgentContainerName
$CatalogImage = [string]$config.CatalogImage
$CatalogPort = [int]$config.CatalogPort
$CatalogContainerName = [string]$config.CatalogContainerName
$CatalogVolumeName = [string]$config.CatalogVolumeName
$AppDatabaseAutoMigrate = [bool]$config.AppDatabaseAutoMigrate
$AgentCatalogUrl = [string]$config.AgentCatalogUrl
$AutoDeployLatestFromCatalog = [bool]$config.AutoDeployLatestFromCatalog
$PostgresUser = [string]$config.PostgresUser
$PostgresPassword = [string]$config.PostgresPassword
$PostgresDatabase = [string]$config.PostgresDatabase
$PostgresPort = [int]$config.PostgresPort

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Refresh-Path {
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

function Ensure-Winget {
    if (-not (Test-Command "winget")) {
        throw "winget bulunamadi. Windows App Installer gerekli."
    }
}

function Install-DockerDesktop {
    Refresh-Path
    if (Test-Command "docker") {
        Write-Host "Docker zaten kurulu: $(docker --version)"
        return
    }

    Ensure-Winget
    Write-Step "Docker Desktop kuruluyor..."
    winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements

    Write-Host ""
    Write-Host "Docker Desktop kuruldu." -ForegroundColor Yellow
    Write-Host "1) Bilgisayari yeniden baslat" -ForegroundColor Yellow
    Write-Host "2) Docker Desktop'i ac" -ForegroundColor Yellow
    Write-Host "3) Kur.bat'i tekrar calistir" -ForegroundColor Yellow
    exit 0
}

function Wait-DockerReady {
    Write-Step "Docker hazir mi kontrol ediliyor..."
    for ($i = 1; $i -le 30; $i++) {
        try {
            docker info *> $null
            if ($LASTEXITCODE -eq 0) { return }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Docker calismiyor. Docker Desktop acik mi?"
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [int]$Retries = 30,
        [int]$DelaySeconds = 2
    )

    for ($i = 1; $i -le $Retries; $i++) {
        try {
            $resp = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
            if ($null -ne $resp) { return $true }
        }
        catch { }
        Start-Sleep -Seconds $DelaySeconds
    }

    return $false
}

function Find-PsqlPath {
    if (Test-Command "psql") {
        return (Get-Command psql).Source
    }

    $psql = Get-ChildItem "C:\Program Files\PostgreSQL" -Recurse -Filter "psql.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if ($psql) {
        $env:Path = "$($psql.DirectoryName);$env:Path"
        return $psql.FullName
    }

    return $null
}

function Install-PostgreSql {
    $existing = Find-PsqlPath
    if ($existing) {
        Write-Host "PostgreSQL zaten kurulu."
        return $existing
    }

    Ensure-Winget
    Write-Step "PostgreSQL kuruluyor..."

    $packages = @(
        "PostgreSQL.PostgreSQL.18",
        "EnterpriseDB.PostgreSQL.18",
        "PostgreSQL.PostgreSQL.17",
        "PostgreSQL.PostgreSQL"
    )

    $installed = $false
    foreach ($package in $packages) {
        winget install -e --id $package --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -eq 0) {
            $installed = $true
            break
        }
        Write-Host "Paket yuklenemedi: $package" -ForegroundColor DarkYellow
    }

    if (-not $installed) {
        throw "PostgreSQL kurulamadi. config.json sonrasi elle kurup Kur.bat'i -SkipPostgres ile tekrar dene."
    }

    Refresh-Path
    Start-Sleep -Seconds 5
    $psqlPath = Find-PsqlPath
    if (-not $psqlPath) {
        throw "PostgreSQL kuruldu ama psql.exe bulunamadi."
    }
    return $psqlPath
}

function Initialize-PostgreSqlDatabase {
    param([string]$PsqlPath)

    Write-Step "Veritabani hazirlaniyor: $PostgresDatabase"

    $pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pgService -and $pgService.Status -ne "Running") {
        Start-Service $pgService.Name
        Start-Sleep -Seconds 4
    }

    $sql = @"
DO `$$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$PostgresUser') THEN
        CREATE ROLE $PostgresUser WITH LOGIN PASSWORD '$PostgresPassword' SUPERUSER CREATEDB;
    ELSE
        ALTER ROLE $PostgresUser WITH PASSWORD '$PostgresPassword';
    END IF;
END
`$$;

SELECT 'CREATE DATABASE $PostgresDatabase OWNER $PostgresUser'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$PostgresDatabase')\gexec
"@

    $tempSql = [System.IO.Path]::GetTempFileName() + ".sql"
    Set-Content -Path $tempSql -Value $sql -Encoding UTF8
    & $PsqlPath -U postgres -d postgres -v ON_ERROR_STOP=1 -f $tempSql
    Remove-Item $tempSql -Force -ErrorAction SilentlyContinue
}

function Install-AgentContainer {
    Write-Step "Agent image indiriliyor: $AgentImage"
    docker pull $AgentImage

    $existing = docker ps -a --filter "name=^/${AgentContainerName}$" --format "{{.Names}}"
    if ($existing) {
        Write-Host "Eski agent kaldiriliyor: $AgentContainerName"
        docker rm -f $AgentContainerName | Out-Null
    }

    $connectionString = "Host=host.docker.internal;Port=$PostgresPort;Database=$PostgresDatabase;Username=$PostgresUser;Password=$PostgresPassword"
    $catalogUrl = $AgentCatalogUrl
    if ([string]::IsNullOrWhiteSpace($catalogUrl)) {
        $catalogUrl = "http://host.docker.internal:$CatalogPort"
    }

    Write-Step "Agent baslatiliyor (port $AgentPort)..."
    docker run -d `
        --name $AgentContainerName `
        --restart unless-stopped `
        -p "${AgentPort}:8080" `
        -v /var/run/docker.sock:/var/run/docker.sock `
        -e "Agent__AppHostPort=$AppHostPort" `
        -e "Agent__CatalogUrl=$catalogUrl" `
        -e "Agent__AppDatabaseConnectionString=$connectionString" `
        -e "Agent__AppDatabaseAutoMigrate=$($AppDatabaseAutoMigrate.ToString().ToLowerInvariant())" `
        $AgentImage | Out-Null

    Start-Sleep -Seconds 3
    if (-not (docker ps --filter "name=$AgentContainerName" --format "{{.Names}}")) {
        Write-Host "Agent log:" -ForegroundColor Red
        docker logs $AgentContainerName
        throw "Agent baslatilamadi."
    }
}

function Install-CatalogContainer {
    if ([string]::IsNullOrWhiteSpace($CatalogImage)) {
        throw "CatalogImage bos olamaz."
    }

    Write-Step "Catalog image indiriliyor: $CatalogImage"
    docker pull $CatalogImage

    $existing = docker ps -a --filter "name=^/${CatalogContainerName}$" --format "{{.Names}}"
    if ($existing) {
        Write-Host "Eski catalog kaldiriliyor: $CatalogContainerName"
        docker rm -f $CatalogContainerName | Out-Null
    }

    Write-Step "Catalog baslatiliyor (port $CatalogPort)..."
    docker run -d `
        --name $CatalogContainerName `
        --restart unless-stopped `
        -p "${CatalogPort}:8090" `
        -v "${CatalogVolumeName}:/app/wwwroot/releases" `
        -e "Catalog__PublicBaseUrl=http://host.docker.internal:$CatalogPort" `
        $CatalogImage | Out-Null

    if (-not (Wait-HttpReady -Url "http://localhost:$CatalogPort/api/health" -Retries 40 -DelaySeconds 2)) {
        Write-Host "Catalog log:" -ForegroundColor Red
        docker logs $CatalogContainerName
        throw "Catalog baslatilamadi."
    }
}

function Get-LatestCatalogVersion {
    param([string]$BaseUrl)

    $uri = "$BaseUrl/api/releases"
    $response = Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec 15
    $items = @($response.items)
    if ($items.Count -eq 0) {
        return $null
    }

    $latest = $items |
        Where-Object { $_.version -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [Version]$_.version } -Descending |
        Select-Object -First 1

    return $latest.version
}

function Deploy-LatestFromCatalog {
    Write-Step "Catalog'daki son surum agent ile deploy ediliyor..."

    if (-not (Wait-HttpReady -Url "http://localhost:$AgentPort/api/status" -Retries 40 -DelaySeconds 2)) {
        throw "Agent API hazir degil: http://localhost:$AgentPort/api/status"
    }

    $catalogApi = "http://localhost:$CatalogPort"
    $latestVersion = Get-LatestCatalogVersion -BaseUrl $catalogApi
    if ([string]::IsNullOrWhiteSpace($latestVersion)) {
        Write-Host "Catalog'da surum bulunamadi. Deploy atlandi." -ForegroundColor Yellow
        return
    }

    Write-Host "Son surum bulundu: $latestVersion"
    Invoke-RestMethod -Method Post -Uri "http://localhost:$AgentPort/api/deploy/$latestVersion" -TimeoutSec 30 | Out-Null
    Write-Host "Deploy tetiklendi. Durum: http://localhost:$AgentPort"
}

Clear-Host
Write-Host "============================================" -ForegroundColor Green
Write-Host " dockertest Agent Kurulumu" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "Image: $AgentImage"

if (-not $SkipDocker) { Install-DockerDesktop }

Refresh-Path
Wait-DockerReady

if (-not $SkipPostgres) {
    $psqlPath = Install-PostgreSql
    Initialize-PostgreSqlDatabase -PsqlPath $psqlPath
}

if (-not $SkipCatalog) {
    Install-CatalogContainer
}

if (-not $SkipAgent) {
    Install-AgentContainer
}

if (-not $SkipAutoDeploy -and $AutoDeployLatestFromCatalog) {
    Deploy-LatestFromCatalog
}

$hostName = $env:COMPUTERNAME
Write-Host ""
Write-Host "Kurulum tamamlandi." -ForegroundColor Green
Write-Host ""
Write-Host "  Agent  : http://${hostName}:${AgentPort}  (veya http://localhost:${AgentPort})"
Write-Host "  Catalog: http://${hostName}:${CatalogPort}  (veya http://localhost:${CatalogPort})"
Write-Host "  Sonraki adim: Agent ekranindan deploy durumunu kontrol et"
Write-Host "  Uygulama portu: $AppHostPort (deploy sonrasi)"
Write-Host ""
