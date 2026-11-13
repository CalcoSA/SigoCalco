param([Parameter(Mandatory)][string]$Query)
$ErrorActionPreference = 'Stop'
# Deliberately limited to inspection. Never accepts a migration script.
if (($Query -replace '(?i)SHOW\s+CREATE\s+TABLE','SHOW TABLE') -match '(?i)\b(CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|REPLACE|TRUNCATE|CALL|INTO|OUTFILE|LOAD|GRANT|REVOKE|SET|LOCK|UNLOCK)\b' -or
    $Query -match '(--|/\*|#)' -or
    @($Query.Split(';') | Where-Object { $_.Trim() -and $_ -notmatch '^\s*(SELECT|SHOW)\b' }).Count) {
    throw 'Solo se permiten consultas SELECT y SHOW sin efectos secundarios.'
}
$api = Join-Path $PSScriptRoot '../01.Meetings.Api/Meetings.Api'
$connection = $null
foreach ($file in @('appsettings.json','appsettings.Development.json','appsettings.Local.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $api $file))) { continue }
    $settings = Get-Content -LiteralPath (Join-Path $api $file) -Raw | ConvertFrom-Json
    if ($settings.ConnectionStrings.MeetingsDatabase) { $connection = $settings.ConnectionStrings.MeetingsDatabase }
}
$secretFile = Join-Path $env:APPDATA 'Microsoft/UserSecrets/0346b708-8fec-40fd-8eb5-db1c98e774e6/secrets.json'
if (Test-Path -LiteralPath $secretFile) {
    $secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
    if ($secrets.'ConnectionStrings:MeetingsDatabase') { $connection = $secrets.'ConnectionStrings:MeetingsDatabase' }
}
if ($env:ConnectionStrings__MeetingsDatabase) { $connection = $env:ConnectionStrings__MeetingsDatabase }
$b = [System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString($connection)
function Read-Setting([string[]]$Names, [string]$Default) {
    foreach ($n in $Names) { if ($b.ContainsKey($n)) { return [string]$b[$n] } }
    return $Default
}
$server = Read-Setting @('server','host','data source') 'localhost'
$port = Read-Setting @('port') '3306'
$database = Read-Setting @('database','initial catalog') ''
$username = Read-Setting @('user','user id','uid','username') ''
$password = Read-Setting @('password','pwd') ''
if ($database -ne 'bdcalco_sigocalco') { throw 'Base de datos inesperada.' }
$mysql = 'C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe'
$p = [System.Diagnostics.ProcessStartInfo]::new($mysql)
$p.UseShellExecute = $false
$p.RedirectStandardOutput = $true
$p.RedirectStandardError = $true
$p.CreateNoWindow = $true
$p.Environment['MYSQL_PWD'] = $password
foreach ($arg in @('--no-defaults','--protocol=TCP',"--host=$server","--port=$port","--user=$username","--database=$database",'--default-character-set=utf8mb4','--connect-timeout=12','--batch',"--execute=$Query")) { $p.ArgumentList.Add($arg) }
$process = [System.Diagnostics.Process]::Start($p)
$stdout = $process.StandardOutput.ReadToEndAsync()
$stderr = $process.StandardError.ReadToEndAsync()
$process.WaitForExit()
$stdout.GetAwaiter().GetResult()
$message = $stderr.GetAwaiter().GetResult()
if ($password) { $message = $message.Replace($password,'[REDACTED]') }
if ($process.ExitCode -ne 0) { throw $message }
if ($message) { Write-Warning $message }
