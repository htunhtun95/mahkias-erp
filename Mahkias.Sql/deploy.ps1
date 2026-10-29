# Deploys functions, stored procedures, and lookup seed data from this SQL project.
# Run from anywhere:
#   .\Mahkias.Sql\deploy.ps1
# Tables, indexes, and types are defined in this project. Procedure scripts use CREATE,
# and this script applies them with CREATE OR ALTER so an existing database can be updated.
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

function Get-DeployText([string]$Path) {
    $text = Get-Content -Path $Path -Raw
    $text = [regex]::Replace($text, '(?i)CREATE\s+FUNCTION\b', 'CREATE OR ALTER FUNCTION')
    $text = [regex]::Replace($text, '(?i)CREATE\s+PROCEDURE\b', 'CREATE OR ALTER PROCEDURE')
    return $text
}

$scripts = @(Get-ChildItem (Join-Path $root "Programmability\Functions") -Filter *.sql | Sort-Object Name) `
  + @(Get-ChildItem (Join-Path $root "Programmability\StoredProcedures") -Filter *.sql | Sort-Object Name)

$env:SQLCMDPASSWORD = $Password
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mahkias-sql-deploy-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    foreach ($script in $scripts) {
        $relative = $script.FullName.Substring($root.Length).TrimStart('\')
        $tempPath = Join-Path $tempRoot $script.Name
        $utf8 = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($tempPath, (Get-DeployText $script.FullName), $utf8)
        Write-Host "Running $relative"
        & sqlcmd -S $Server -d $Database -U $User -I -b -l 30 -i $tempPath
        if ($LASTEXITCODE -ne 0) {
            throw "sqlcmd failed on $relative (exit $LASTEXITCODE)."
        }
    }

    $seed = Join-Path $root "Scripts\02_DefaultData.sql"
    Write-Host "Running Scripts\02_DefaultData.sql"
    & sqlcmd -S $Server -d $Database -U $User -I -b -l 30 -i $seed
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed on Scripts\02_DefaultData.sql (exit $LASTEXITCODE)."
    }
}
finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Deployment finished."
