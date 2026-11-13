# Read-only verification after the approved migration.
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
$expected = @('acta_type','template','template_version','template_section','acta_status','acta','acta_section',
    'participant','acta_participant','meeting_status','meeting','meeting_reservation','meeting_calendar',
    'acta_center_permission','acta_user_permission','log','permission')
$actual = @((Read-MeetingsMySql $settings 'SHOW TABLES;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
foreach ($name in $expected) {
    if ("sigocalco_meetings_$name" -notin $actual) { throw "Falta la tabla nueva $name." }
}
$expectedAll = @($expected | ForEach-Object { "sigocalco_meetings_$_" }) + @(Get-AuthenticationTableNames)
if ($actual.Count -ne 25 -or @(Compare-Object $actual $expectedAll).Count) { throw 'Se esperaban exactamente 8 tablas Authentication y 17 Meetings, sin tablas antiguas.' }
$types = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_meetings_acta_type WHERE code IN ('REUNION','DISCIPLINARIA','ADMINISTRATIVA');").Trim()
$actaStates = [int](Read-MeetingsMySql $settings 'SELECT COUNT(*) FROM sigocalco_meetings_acta_status;').Trim()
$meetingStates = [int](Read-MeetingsMySql $settings 'SELECT COUNT(*) FROM sigocalco_meetings_meeting_status;').Trim()
$grants = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_meetings_permission WHERE permission_value='Administrador';").Trim()
$denied = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_meetings_permission WHERE permission_value='';").Trim()
if ($types -ne 3 -or $actaStates -ne 5 -or $meetingStates -ne 3 -or $grants -ne 9 -or $denied -ne 10) { throw 'Los catálogos/permisos no coinciden con el plan.' }
$externalFks = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_' AND REFERENCED_TABLE_NAME IS NOT NULL AND LEFT(REFERENCED_TABLE_NAME,19)<>'sigocalco_meetings_';").Trim()
if ($externalFks -ne 0) { throw 'Se detectaron FK externas.' }
Write-Output 'Verificado: exactamente 25 tablas (8 Authentication + 17 Meetings), sin tablas antiguas; catálogos y permisos correctos, sin FK externas.'

