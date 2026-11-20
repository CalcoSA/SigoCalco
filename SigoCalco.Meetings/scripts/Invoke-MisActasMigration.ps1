param([Parameter(Mandatory)][string]$BackupDirectory)
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
if ($settings.Server -ne '127.0.0.1' -or $settings.Port -ne '3306' -or $settings.Database -ne 'bdcalco_sigocalco') { throw 'Solo se permite la instancia local autorizada.' }
$identity = (Read-MeetingsMySql $settings 'SELECT @@hostname,@@server_uuid;').Trim().Split("`t")
if ($identity[0] -ne 'COMDEATECL33' -or $identity[1] -ne '6f5f45b3-9fc3-11f1-b008-b082e2815ab3') { throw 'Instancia no autorizada.' }
$shape = [int](Read-MeetingsMySql $settings @'
SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='bdcalco_sigocalco'
AND ((TABLE_NAME='sigocalco_meetings_acta_participant' AND COLUMN_NAME='attended')
OR (TABLE_NAME='sigocalco_meetings_meeting_notification' AND COLUMN_NAME='acta_id'));
'@).Trim()
$indexes = [int](Read-MeetingsMySql $settings "SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND INDEX_NAME IN ('IX_meetings_acta_owner_id','IX_meetings_notification_acta_recipient');").Trim()
$fk = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA='bdcalco_sigocalco' AND CONSTRAINT_NAME='FK_meetings_notification_acta';").Trim()
$types = (Read-MeetingsMySql $settings "SELECT GROUP_CONCAT(name ORDER BY name) FROM sigocalco_meetings_acta_status;").Trim()
if ($shape -eq 2 -and $indexes -eq 2 -and $fk -eq 1 -and $types -eq 'ANULADA,BORRADOR,PUBLICADA') {
    Write-Output 'Migración Mis actas ya aplicada. No se repiten escrituras.'; exit 0
}
if ($shape -ne 0 -or $indexes -ne 0 -or $fk -ne 0) { throw 'Estado parcial: inspeccionar y recuperar las operaciones pendientes; no repetir el script completo.' }
$external = [int](Read-MeetingsMySql $settings @'
SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA='bdcalco_sigocalco'
AND REFERENCED_TABLE_NAME='sigocalco_meetings_acta_status' AND TABLE_NAME<>'sigocalco_meetings_acta';
'@).Trim()
if ($external) { throw 'El catálogo de estados tiene dependencias externas no revisadas.' }
$references = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_meetings_acta a JOIN sigocalco_meetings_acta_status s ON s.acta_status_id=a.acta_status_id WHERE s.name NOT IN ('BORRADOR','PUBLICADA','ANULADA');").Trim()
if ($references) { Write-Warning 'Hay actas históricas con estados retirados. Sus estados referenciados se conservarán y requerirán una decisión específica.' }
$BackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.ServerUuid -ne $identity[1] -or $manifest.Database -ne $settings.Database) { throw 'Respaldo de otra instancia.' }
$backup = Join-Path $BackupDirectory 'bdcalco_sigocalco.sql'
if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $manifest.FullBackupSha256) { throw 'El respaldo no coincide con su manifiesto.' }
$verified = Join-Path $BackupDirectory 'verified-before-apply.sql'
Export-MeetingsMySql $settings $verified
if ((Get-FileHash -LiteralPath $verified -Algorithm SHA256).Hash -ne $manifest.FullBackupSha256) { throw 'La base cambió desde el respaldo. Preparar un respaldo actualizado antes de migrar.' }
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
$permissions = Read-MeetingsMySql $settings 'SELECT permission_id,permission_key,permission_value FROM sigocalco_meetings_permission ORDER BY permission_id;'
$preservedIds = Read-MeetingsMySql $settings "SELECT acta_status_id,name FROM sigocalco_meetings_acta_status WHERE name IN ('BORRADOR','ANULADA') ORDER BY acta_status_id;"
Write-Output 'Operaciones: asistencia nullable; índice por creador; notificaciones con acta_id y meeting_id nullable, backfill, FK e índice único; PUBLICADA y retiro exclusivamente de estados antiguos sin referencias.'
Invoke-MeetingsMySql -Settings $settings -Arguments @('--database=bdcalco_sigocalco') -InputSql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/06_mis_actas_incremental.sql')))
if ((Read-MeetingsMySql $settings 'SELECT permission_id,permission_key,permission_value FROM sigocalco_meetings_permission ORDER BY permission_id;') -cne $permissions) { throw 'Las asignaciones de permisos cambiaron; revisar actividad externa.' }
if ((Read-MeetingsMySql $settings "SELECT acta_status_id,name FROM sigocalco_meetings_acta_status WHERE name IN ('BORRADOR','ANULADA') ORDER BY acta_status_id;") -cne $preservedIds) { throw 'Se alteraron los IDs de estados preservados.' }
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
Write-Output 'Migración incremental aplicada. Verificar esquema completo contra EF y datos conservados; no repetir DDL.'
