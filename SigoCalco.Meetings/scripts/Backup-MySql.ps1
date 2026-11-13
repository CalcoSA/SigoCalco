param([string]$OutputDirectory)
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
$identity = (Read-MeetingsMySql $settings 'SELECT DATABASE(), @@hostname, @@port, @@server_uuid, VERSION();').Trim().Split("`t")
if ($settings.Server -notin @('127.0.0.1','localhost') -or $identity[1] -ne 'COMDEATECL33' -or
    $identity[3] -ne '6f5f45b3-9fc3-11f1-b008-b082e2815ab3') { throw 'No es la instancia de desarrollo revisada.' }
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot ('../artifacts/mysql-backup-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Export-MeetingsMySql $settings (Join-Path $OutputDirectory 'bdcalco_sigocalco.sql')
$checks = @()
foreach ($table in Get-AuthenticationTableNames) {
    $file = Join-Path $OutputDirectory "$table.sql"
    Export-MeetingsMySql $settings $file @($table)
    $checks += [ordered]@{ Table=$table; Rows=[long](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM $table;").Trim(); Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
}
$manifest = [ordered]@{
    CapturedAtUtc=[DateTime]::UtcNow.ToString('O'); Database=$identity[0]; Hostname=$identity[1]; Port=$identity[2]
    ServerUuid=$identity[3]; Version=$identity[4]; DevelopmentConfirmedByUser=$true
    FullBackupSha256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'bdcalco_sigocalco.sql') -Algorithm SHA256).Hash
    Authentication=$checks
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
Write-Output "Respaldo creado: $OutputDirectory"
Write-Output "SHA256: $($manifest.FullBackupSha256)"

