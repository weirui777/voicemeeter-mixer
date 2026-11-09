$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$dist = Join-Path $root 'dist'
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

$src = @(Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName })
Write-Output "compiling $($src.Count) source files"

$refs = @(
  '/reference:System.dll',
  '/reference:System.Core.dll',
  '/reference:System.Drawing.dll',
  '/reference:System.Windows.Forms.dll'
)

$icon = Join-Path $dist 'vmixer.ico'
$iconArg = @()
if (Test-Path $icon) { $iconArg = @("/win32icon:$icon") }

& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /warn:2 `
    "/out:$dist\VMixer.exe" @refs @iconArg $src
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit code $LASTEXITCODE)" }

Write-Output "OK -> $dist\VMixer.exe"
Get-Item "$dist\VMixer.exe" | Select-Object Name, Length, LastWriteTime | Format-List
