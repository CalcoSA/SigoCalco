param([Parameter(Mandatory)][string]$BackupDirectory)
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
if ($settings.Server -ne '127.0.0.1' -or $settings.Port -ne '3306' -or $settings.Database -ne 'bdcalco_sigocalco') {
    throw 'Solo se permite bdcalco_sigocalco en 127.0.0.1:3306.'
}
$identity = (Read-MeetingsMySql $settings 'SELECT @@hostname,@@server_uuid;').Trim().Split("`t")
if ($identity[0] -ne 'COMDEATECL33' -or $identity[1] -ne '6f5f45b3-9fc3-11f1-b008-b082e2815ab3') { throw 'Instancia no autorizada.' }
$BackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.ServerUuid -ne $identity[1] -or $manifest.Database -ne $settings.Database) { throw 'Respaldo de otra instancia.' }
if ((Get-FileHash -LiteralPath (Join-Path $BackupDirectory 'bdcalco_sigocalco.sql') -Algorithm SHA256).Hash -ne $manifest.FullBackupSha256) { throw 'El respaldo fue alterado.' }
$newKeys = @('PLANTILLAS_GLOBALES_GESTION','PLANTILLAS_GLOBALES_LECTURA','PLANTILLAS_CENTRO_GESTION','PLANTILLAS_CENTRO_LECTURA','ACTAS_GESTION','ACTAS_LECTURA')
$oldKeys = @('REUNION','DISCIPLINARIA','ADMINISTRATIVA') | ForEach-Object { $code = $_; @('PLANTILLA_CREACION','PLANTILLA_LECTURA','VERSION_CREACION','CREACION','LECTURA','EDICION') | ForEach-Object { "ACTA_${code}_$_" } }
$oldKeys += 'VIEW_ALL_SOLUTION_CENTERS'
$keys = @((Read-MeetingsMySql $settings 'SELECT permission_key FROM sigocalco_meetings_permission ORDER BY permission_key;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
$notificationExists = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND TABLE_NAME='sigocalco_meetings_meeting_notification';").Trim()
if ($notificationExists -eq 1 -and !(Compare-Object $keys $newKeys)) { Write-Output 'Entrega ya aplicada. No se repite ninguna operación.'; exit 0 }
if ($notificationExists -ne 0 -or $keys.Count -ne 19 -or @(Compare-Object $keys $oldKeys).Count) {
    throw 'Estado distinto al origen revisado o migración parcial. Inspeccione el esquema y recupere solo las operaciones pendientes; no reejecute DDL.'
}
$references = [int](Read-MeetingsMySql $settings @'
SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA='bdcalco_sigocalco'
AND REFERENCED_TABLE_NAME='sigocalco_meetings_permission';
'@).Trim()
if ($references) { throw 'Existen referencias a los permisos antiguos. Resolver antes de migrar.' }
$disciplinary = (Read-MeetingsMySql $settings @'
SELECT 'template', COUNT(*) FROM sigocalco_meetings_template WHERE acta_type_id IN (SELECT acta_type_id FROM sigocalco_meetings_acta_type WHERE code='DISCIPLINARIA')
UNION ALL SELECT 'template_version', COUNT(*) FROM sigocalco_meetings_template_version WHERE acta_type_id IN (SELECT acta_type_id FROM sigocalco_meetings_acta_type WHERE code='DISCIPLINARIA')
UNION ALL SELECT 'acta', COUNT(*) FROM sigocalco_meetings_acta WHERE acta_type_id IN (SELECT acta_type_id FROM sigocalco_meetings_acta_type WHERE code='DISCIPLINARIA');
'@).Trim()
Write-Output $disciplinary
if ($disciplinary.Split("`n") | Where-Object { [long]($_.Trim().Split("`t")[1]) -ne 0 }) {
    Write-Warning 'El registro disciplinario se conservará por referencias históricas; requiere una decisión específica. Las demás operaciones son independientes.'
}
# Compare a fresh complete export immediately before DDL. This validates backup content without restoring into another database.
$verified = Join-Path $BackupDirectory 'verified-before-apply.sql'
Export-MeetingsMySql $settings $verified
if ((Get-FileHash -LiteralPath $verified -Algorithm SHA256).Hash -ne $manifest.FullBackupSha256) { throw 'La base cambió: genere y verifique un respaldo actualizado.' }
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
$catalogBefore = Read-MeetingsMySql $settings "SELECT acta_type_id,code FROM sigocalco_meetings_acta_type WHERE code IN ('REUNION','ADMINISTRATIVA') ORDER BY acta_type_id;"
# DDL commits implicitly. If the next operation fails, stop and inspect; no blanket rollback or reconstruction is attempted.
Invoke-MeetingsMySql -Settings $settings -Arguments @('--database=bdcalco_sigocalco') -InputSql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/04_add_meeting_notifications.sql')))
Invoke-MeetingsMySql -Settings $settings -Arguments @('--database=bdcalco_sigocalco') -InputSql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'sql/05_six_permissions_and_active_types.sql')))
$keysAfter = @((Read-MeetingsMySql $settings 'SELECT permission_key FROM sigocalco_meetings_permission ORDER BY permission_key;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
if (@(Compare-Object $keysAfter $newKeys).Count) { throw 'No quedó el conjunto exacto de seis permisos.' }
if ((Read-MeetingsMySql $settings "SELECT acta_type_id,code FROM sigocalco_meetings_acta_type WHERE code IN ('REUNION','ADMINISTRATIVA') ORDER BY acta_type_id;") -ne $catalogBefore) { throw 'Cambió la identidad de los tipos vigentes.' }
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
Write-Output 'Migración incremental completada: notificaciones creadas, seis permisos configurados y tipos vigentes conservados.'
