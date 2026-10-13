# Deploys Mahkias ERP schema objects to Azure SQL.
# Run from anywhere:
#   .\Database\Scripts\deploy.ps1
# The password is prompted for and is not stored in this file.

param(
    [string]$Server = "mahkiaserpdb-server.database.windows.net",
    [string]$Database = "mahkias",
    [string]$User = "mahkias_admin",
    [string]$Password
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found. Install SQL Server command-line tools, then run this script again."
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $secure = Read-Host "Password for $User" -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

$scripts = @(
    "Tables\01_CreateTables.sql"
    "UserDefinedTypes\CreateUserDefinedTypes.sql"
) + @(Get-ChildItem (Join-Path $root "Functions") -Filter *.sql | Sort-Object Name | ForEach-Object { "Functions\$($_.Name)" }) `
  + @(Get-ChildItem (Join-Path $root "StoredProcedures") -Filter *.sql | Sort-Object Name | ForEach-Object { "StoredProcedures\$($_.Name)" }) `
  + @("Tables\02_DefaultData.sql")

$env:SQLCMDPASSWORD = $Password
try {
    foreach ($relative in $scripts) {
        $path = Join-Path $root $relative
        if (-not (Test-Path $path)) {
            throw "Script not found: $path"
        }

        Write-Host "Running $relative"
        & sqlcmd -S $Server -d $Database -U $User -I -b -l 30 -i $path
        if ($LASTEXITCODE -ne 0) {
            throw "sqlcmd failed on $relative (exit $LASTEXITCODE)."
        }
    }
}
finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "Deployment finished."
