param([Parameter(Mandatory)][string]$BackupDirectory)
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
$manifest = Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw | ConvertFrom-Json
$identity = (Read-MeetingsMySql $settings 'SELECT DATABASE(), @@hostname, @@port, @@server_uuid;').Trim().Split("`t")
if ($identity[0] -ne $manifest.Database -or $identity[3] -ne $manifest.ServerUuid) { throw 'El respaldo pertenece a otra instancia.' }
$tables = @((Read-MeetingsMySql $settings 'SHOW TABLES;').Trim().Split("`n") | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('sigocalco_authentication_') })
if ($tables.Count -ne 8 -or @(Compare-Object $tables @(Get-AuthenticationTableNames)).Count) { throw 'Cambió el conjunto de tablas Authentication.' }
$verification = Join-Path $BackupDirectory 'verification'
New-Item -ItemType Directory -Path $verification -Force | Out-Null
foreach ($table in $manifest.Authentication) {
    $file = Join-Path $verification "$($table.Table).sql"
    Export-MeetingsMySql $settings $file @($table.Table)
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    if ($hash -ne $table.Sha256) { throw "Authentication cambió desde el respaldo: $($table.Table). No continuar." }
}
Write-Output 'Authentication: las ocho tablas conservan exactamente el esquema y los datos del respaldo (SHA256).'

