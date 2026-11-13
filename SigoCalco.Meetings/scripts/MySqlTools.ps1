$ErrorActionPreference = 'Stop'
function Get-MeetingsMySqlSettings {
    $api = Join-Path $PSScriptRoot '../01.Meetings.Api/Meetings.Api'
    $connection = $null
    foreach ($file in @('appsettings.json','appsettings.Development.json','appsettings.Local.json')) {
        $path = Join-Path $api $file
        if (!(Test-Path -LiteralPath $path)) { continue }
        $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($settings.ConnectionStrings.MeetingsDatabase) { $connection = $settings.ConnectionStrings.MeetingsDatabase }
    }
    $secretFile = Join-Path $env:APPDATA 'Microsoft/UserSecrets/0346b708-8fec-40fd-8eb5-db1c98e774e6/secrets.json'
    if (Test-Path -LiteralPath $secretFile) {
        $secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
        if ($secrets.'ConnectionStrings:MeetingsDatabase') { $connection = $secrets.'ConnectionStrings:MeetingsDatabase' }
    }
    if ($env:ConnectionStrings__MeetingsDatabase) { $connection = $env:ConnectionStrings__MeetingsDatabase }
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.set_ConnectionString($connection)
    function Setting([string[]]$Names, [string]$Default) {
        foreach ($n in $Names) { if ($builder.ContainsKey($n)) { return [string]$builder[$n] } }
        return $Default
    }
    $result = @{
        Server = Setting @('server','host','data source') 'localhost'
        Port = Setting @('port') '3306'
        Database = Setting @('database','initial catalog') ''
        Username = Setting @('user','user id','uid','username') ''
        Password = Setting @('password','pwd') ''
    }
    if ($result.Database -ne 'bdcalco_sigocalco') { throw 'Base de datos inesperada.' }
    return $result
}
function Invoke-MeetingsMySql {
    param([hashtable]$Settings, [string[]]$Arguments, [string]$InputSql, [string]$OutputFile, [switch]$Dump)
    $binary = if ($Dump) { 'mysqldump.exe' } else { 'mysql.exe' }
    $p = [System.Diagnostics.ProcessStartInfo]::new((Join-Path 'C:/Program Files/MySQL/MySQL Server 8.0/bin' $binary))
    $p.UseShellExecute = $false
    $p.RedirectStandardOutput = $true
    $p.RedirectStandardError = $true
    $p.RedirectStandardInput = -not [string]::IsNullOrEmpty($InputSql)
    if ($p.RedirectStandardInput) { $p.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false) }
    $p.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $p.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $p.CreateNoWindow = $true
    $p.Environment['MYSQL_PWD'] = $Settings.Password
    foreach ($arg in @('--no-defaults','--protocol=TCP',"--host=$($Settings.Server)","--port=$($Settings.Port)","--user=$($Settings.Username)",'--default-character-set=utf8mb4') + $Arguments) {
        $p.ArgumentList.Add($arg)
    }
    $process = [System.Diagnostics.Process]::Start($p)
    $errorTask = $process.StandardError.ReadToEndAsync()
    $stream = $null
    if ($OutputFile) {
        $stream = [System.IO.File]::Create($OutputFile)
        $outputTask = $process.StandardOutput.BaseStream.CopyToAsync($stream)
    } else { $outputTask = $process.StandardOutput.ReadToEndAsync() }
    try {
        if ($InputSql) { $process.StandardInput.Write($InputSql); $process.StandardInput.Close() }
        $process.WaitForExit()
        if ($OutputFile) { $outputTask.GetAwaiter().GetResult() | Out-Null } else { $result = $outputTask.GetAwaiter().GetResult() }
        $errorText = $errorTask.GetAwaiter().GetResult()
        if ($Settings.Password) { $errorText = $errorText.Replace($Settings.Password,'[REDACTED]') }
        if ($process.ExitCode -ne 0) { throw "MySQL finalizó con error: $errorText" }
        if (!$OutputFile) { return $result }
    } finally { if ($stream) { $stream.Dispose() }; $process.Dispose() }
}
function Read-MeetingsMySql {
    param([hashtable]$Settings, [string]$Query)
    if (($Query -replace '(?i)SHOW\s+CREATE\s+TABLE','SHOW TABLE') -match '(?i)\b(CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|REPLACE|TRUNCATE|CALL|INTO|OUTFILE|LOAD|GRANT|REVOKE|SET|LOCK|UNLOCK)\b' -or
        $Query -match '(--|/\*|#)' -or
        @($Query.Split(';') | Where-Object { $_.Trim() -and $_ -notmatch '^\s*(SELECT|SHOW)\b' }).Count) {
        throw 'Solo se permiten SELECT y SHOW sin efectos secundarios.'
    }
    Invoke-MeetingsMySql -Settings $Settings -Arguments @("--database=$($Settings.Database)",'--connect-timeout=12','--batch','--raw','--skip-column-names',"--execute=$Query")
}
function Get-AuthenticationTableNames {
    @('log','menuoption','role','rolemenuoption','sessions','solution_center','userrole','users') |
        ForEach-Object { "sigocalco_authentication_$_" }
}
function Export-MeetingsMySql {
    param([hashtable]$Settings, [string]$File, [string[]]$Tables)
    $arguments = @('--single-transaction','--skip-lock-tables','--no-tablespaces','--set-gtid-purged=OFF',
        '--skip-comments','--hex-blob','--order-by-primary','--skip-add-drop-table','--skip-add-locks','--skip-disable-keys',
        $Settings.Database)
    if ($Tables) { $arguments += $Tables }
    Invoke-MeetingsMySql -Settings $Settings -Arguments $arguments -OutputFile $File -Dump
    if ((Get-Item -LiteralPath $File).Length -eq 0) { throw 'El respaldo está vacío.' }
}

