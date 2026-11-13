param(
    [switch]$Apply,
    [string]$ApprovedPlanSha256,
    [string]$BackupDirectory
)
. (Join-Path $PSScriptRoot 'MySqlTools.ps1')
$settings = Get-MeetingsMySqlSettings
$manifestPath = Join-Path $PSScriptRoot 'migration-manifest.json'
$planHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$plan = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$identity = (Read-MeetingsMySql $settings 'SELECT DATABASE(), @@hostname, @@port, @@server_uuid, VERSION(), @@default_storage_engine;').Trim().Split("`t")
if ($settings.Server -ne '127.0.0.1' -or $settings.Port -ne '3306' -or $identity[0] -ne $plan.Database -or
    $identity[1] -ne $plan.Hostname -or $identity[2] -ne $plan.Port -or $identity[3] -ne $plan.ServerUuid -or
    $identity[4] -ne $plan.Version -or $identity[5] -ne 'InnoDB') { throw 'La instancia no coincide con el plan de desarrollo revisado.' }
$actualTables = @((Read-MeetingsMySql $settings 'SHOW TABLES;').Trim().Split("`n") | ForEach-Object { $_.Trim() })
if (@(Compare-Object $actualTables @($plan.OriginalTables)).Count) { throw 'El conjunto de tablas cambió. Reauditar antes de continuar.' }
$oldNames = ($plan.Legacy.Table | ForEach-Object { "'$_'" }) -join ','
$references = Read-MeetingsMySql $settings "SELECT TABLE_SCHEMA,TABLE_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA='bdcalco_sigocalco' AND REFERENCED_TABLE_NAME IN ($oldNames) AND (TABLE_SCHEMA<>'bdcalco_sigocalco' OR TABLE_NAME NOT IN ($oldNames));"
if ($references.Trim()) { throw 'Hay dependencias FK externas: no se eliminará ninguna tabla.' }
$views = Read-MeetingsMySql $settings "SELECT VIEW_SCHEMA,VIEW_NAME FROM information_schema.VIEW_TABLE_USAGE WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND TABLE_NAME IN ($oldNames);"
if ($views.Trim()) { throw 'Hay vistas dependientes: resolverlas antes de continuar.' }
$triggers = Read-MeetingsMySql $settings "SELECT TRIGGER_SCHEMA,TRIGGER_NAME FROM information_schema.TRIGGERS WHERE (EVENT_OBJECT_SCHEMA='bdcalco_sigocalco' AND EVENT_OBJECT_TABLE IN ($oldNames)) OR ACTION_STATEMENT LIKE '%sigocalco_meetings_%';"
if ($triggers.Trim()) { throw 'Hay triggers relacionados con Meetings: resolverlos antes de continuar.' }
$routines = Read-MeetingsMySql $settings "SELECT ROUTINE_SCHEMA,ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_DEFINITION LIKE '%sigocalco_meetings_%'; SELECT EVENT_SCHEMA,EVENT_NAME FROM information_schema.EVENTS WHERE EVENT_DEFINITION LIKE '%sigocalco_meetings_%';"
if ($routines.Trim()) { throw 'Hay rutinas/eventos relacionados con Meetings: resolverlos antes de continuar.' }
$schemaPath = Join-Path $PSScriptRoot 'sql/02_create_model.sql'
if (!(Test-Path -LiteralPath $schemaPath) -or [regex]::Matches((Get-Content -LiteralPath $schemaPath -Raw),'(?m)^CREATE TABLE').Count -ne 17) {
    throw 'Falta el modelo definitivo de las 17 tablas.'
}
foreach ($table in $plan.Legacy) {
    if ([long](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM $($table.Table);").Trim() -ne 0) {
        throw "Aparecieron datos en $($table.Table): se requiere una migración de información revisada; no se ejecutó DDL."
    }
    $schema = (Read-MeetingsMySql $settings "SHOW CREATE TABLE $($table.Table);").Replace("`r`n","`n").Trim()
    $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($schema)))
    if ($hash -ne $table.SchemaSha256) { throw "El esquema cambió: $($table.Table)." }
}
$roleCount = [int](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM sigocalco_authentication_role WHERE UPPER(TRIM(nameRole))='ADMINISTRADOR' AND statusRole=1;").Trim()
if ($roleCount -ne 1) { throw 'No existe el rol activo Administrador acordado.' }
foreach ($script in $plan.Scripts) {
    $path = Join-Path $PSScriptRoot $script.File
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $script.Sha256) { throw "Cambió el SQL aprobado: $($script.File)." }
}
Write-Output "Servidor: $($plan.Hostname), $($settings.Server):$($plan.Port), $($plan.Database), MySQL $($plan.Version)"
Write-Output "Plan SHA256: $planHash"
Write-Output 'Operaciones autorizadas: 9 DROP TABLE vacías (hijas antes que padres); 17 CREATE TABLE; 30 CREATE INDEX; catálogos y 19 permisos. Authentication intacto.'
if (!$Apply) {
    Write-Output 'PREVISUALIZACIÓN: no se ejecutó ningún cambio. Revisar scripts/sql y aprobar este hash antes de -Apply.'
    exit 0
}
if ($ApprovedPlanSha256 -ne $planHash -or !$BackupDirectory) { throw 'Se requiere aprobación explícita del hash y un respaldo verificado.' }
$backupPath = Join-Path $BackupDirectory 'bdcalco_sigocalco.sql'
$backupManifest = Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw | ConvertFrom-Json
if ($backupManifest.ServerUuid -ne $plan.ServerUuid -or
    (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash -ne $backupManifest.FullBackupSha256) { throw 'Respaldo inválido.' }
if ([DateTime]::UtcNow - [DateTime]::Parse($backupManifest.CapturedAtUtc).ToUniversalTime() -gt [TimeSpan]::FromMinutes(30)) {
    throw 'El respaldo tiene más de 30 minutos; generar uno nuevo antes de aplicar.'
}
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
# Recheck emptiness immediately before the approved DDL, with the old backend stopped.
foreach ($table in $plan.Legacy) {
    if ([long](Read-MeetingsMySql $settings "SELECT COUNT(*) FROM $($table.Table);").Trim() -ne 0) { throw 'Una tabla antigua dejó de estar vacía; cancelado.' }
}
# Only after explicit approval: MySQL DDL commits implicitly; failures stop the run.
# Never retry all scripts automatically after partial DDL. Inspect and re-plan.
foreach ($script in $plan.Scripts) {
    $sql = Get-Content -LiteralPath (Join-Path $PSScriptRoot $script.File) -Raw
    # Batch mode stops at the first SQL error by default; never pass --force.
    Invoke-MeetingsMySql -Settings $settings -Arguments @("--database=$($settings.Database)",'--batch','--connect-timeout=12') -InputSql $sql
    Write-Output "Aplicado: $($script.File)"
}
& (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BackupDirectory $BackupDirectory
& (Join-Path $PSScriptRoot 'Verify-Meetings.ps1')

