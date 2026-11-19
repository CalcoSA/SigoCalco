# Read-only verification after the approved migration.
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
$expected = @('acta_type','template','template_version','template_section','acta_status','acta','acta_section',
    'participant','acta_participant','meeting_status','meeting','meeting_reservation','meeting_calendar',
    'acta_center_permission','acta_user_permission','log','permission','meeting_notification')
$actual = @((Read-MeetingsMySql $settings 'SHOW TABLES;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
foreach ($name in $expected) {
    if ("sigocalco_meetings_$name" -notin $actual) { throw "Falta la tabla nueva $name." }
}
$expectedAll = @($expected | ForEach-Object { "sigocalco_meetings_$_" }) + @(Get-AuthenticationTableNames)
if ($actual.Count -ne 26 -or @(Compare-Object $actual $expectedAll).Count) { throw 'Se esperaban 8 tablas Authentication y 18 Meetings, incluido el calendario histórico conservado.' }
$types = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_meetings_acta_type WHERE code IN ('REUNION','ADMINISTRATIVA');").Trim()
$actaStates = [int](Read-MeetingsMySql $settings 'SELECT COUNT(*) FROM sigocalco_meetings_acta_status;').Trim()
$meetingStates = [int](Read-MeetingsMySql $settings 'SELECT COUNT(*) FROM sigocalco_meetings_meeting_status;').Trim()
$permissions = @((Read-MeetingsMySql $settings 'SELECT permission_key FROM sigocalco_meetings_permission;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
$expectedPermissions = @('PLANTILLAS_GLOBALES_GESTION','PLANTILLAS_GLOBALES_LECTURA','PLANTILLAS_CENTRO_GESTION','PLANTILLAS_CENTRO_LECTURA','ACTAS_GESTION','ACTAS_LECTURA')
if ($types -ne 2 -or $actaStates -ne 5 -or $meetingStates -ne 3 -or @(Compare-Object $permissions $expectedPermissions).Count) { throw 'Los catálogos/permisos no coinciden con el plan.' }
$externalFks = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_' AND REFERENCED_TABLE_NAME IS NOT NULL AND LEFT(REFERENCED_TABLE_NAME,19)<>'sigocalco_meetings_';").Trim()
if ($externalFks -ne 0) { throw 'Se detectaron FK externas.' }
Write-Output 'Verificado: 26 tablas (8 Authentication + 18 Meetings), dos tipos vigentes, seis permisos y ninguna FK externa; calendario histórico conservado.'

