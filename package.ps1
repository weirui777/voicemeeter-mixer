$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
#  VMixer 打包脚本
#
#  做三件事：
#    1. 编译 dist\VMixer.exe
#    2. 组装 release\（exe + ico + 使用说明 + 界面截图）
#    3. 压成 dist\VMixer.zip
#
#  用法：powershell -ExecutionPolicy Bypass -File .\package.ps1
#
#  注意：这个脚本里不写任何中文字面量 —— PowerShell 5.1 读 .ps1 会按 ANSI(GBK)
#  解释，中文会变成乱码。需要中文的地方一律用 [char]0xXXXX 拼。
# ---------------------------------------------------------------------------

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$release = Join-Path $root 'release'
$docs = Join-Path $root 'docs'
$exe = Join-Path $dist 'VMixer.exe'
$ico = Join-Path $dist 'vmixer.ico'

# 「使用说明.md」——用码点拼出来，避免 GBK 乱码
$docName = ([string][char]0x4F7F) + [char]0x7528 + [char]0x8BF4 + [char]0x660E + '.md'

function Build-Exe {
    Write-Output '==> compiling'
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "compile failed" }
}

# 托盘/窗口图标要先有 exe 才能生成；生成的 ico 再嵌回 exe 里，所以要编译两轮
function Make-Icon {
    Write-Output '==> generating icon'
    & $exe --icon $ico
    if (-not (Test-Path $ico)) { throw "icon not generated: $ico" }
}

# ---------------------------------------------------------------------------

if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
if (-not (Test-Path $release)) { New-Item -ItemType Directory -Path $release | Out-Null }

# 1) 先编译一次（还没有 ico 的时候也能编）
Build-Exe

# 2) 生成 ico，再编译一次把它嵌进 exe
Make-Icon
Build-Exe

# 3) 组装 release
Write-Output '==> assembling release'

# 正在运行的实例会锁住 release\VMixer.exe，先停掉
Get-Process VMixer -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output ("    stopping running VMixer (pid " + $_.Id + ")")
    Stop-Process -Id $_.Id -Force
    Start-Sleep -Milliseconds 600
}

Copy-Item $exe (Join-Path $release 'VMixer.exe') -Force
Copy-Item $ico (Join-Path $release 'vmixer.ico') -Force

if (Test-Path $docs) {
    Get-ChildItem $docs -Filter *.png | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $release $_.Name) -Force
    }
}

# 仓库里的 README.md 引用的是 docs/xxx.png；发布包里图片就在旁边，去掉前缀
$readme = Join-Path $root 'README.md'
if (Test-Path $readme) {
    $md = [IO.File]::ReadAllText($readme, [Text.Encoding]::UTF8)
    $md = $md.Replace('](docs/', '](')
    [IO.File]::WriteAllText((Join-Path $release $docName), $md, (New-Object Text.UTF8Encoding($false)))
}

# 4) 打包
Write-Output '==> zipping'
$zip = Join-Path $dist 'VMixer.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($release, $zip,
    [IO.Compression.CompressionLevel]::Optimal, $false)

Get-Item $zip | Select-Object Name, Length, LastWriteTime | Format-List
Write-Output ("OK -> " + $zip)
