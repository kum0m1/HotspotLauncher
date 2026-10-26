# 编译 HotspotLauncher.exe —— 单文件、免安装、双击即用
# 依赖：系统自带的 .NET Framework 4.x（Windows 10/11 默认已装）
# 注意：本文件必须保存为「UTF-8 带 BOM」。若存成无 BOM 的 UTF-8，
#       Windows PowerShell 5.1 会按 GBK 解析中文注释，导致语法错误。

$ErrorActionPreference = 'Stop'

$root   = $PSScriptRoot
$source = Join-Path $root 'src\HotspotLauncher.cs'
$outDir = Join-Path $root 'dist'
$outExe = Join-Path $outDir 'HotspotLauncher.exe'

if (-not (Test-Path $source)) { throw "找不到源码：$source" }

$fw  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) {
    $fw  = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $csc = Join-Path $fw 'csc.exe'
}
if (-not (Test-Path $csc)) { throw '找不到 csc.exe：请确认已安装 .NET Framework 4.x' }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 系统自带 csc 只支持 C# 5：源码里不能用局部函数、字符串插值等较新语法
$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/warn:4'
    "/out:$outExe"
    "/reference:$(Join-Path $fw 'System.dll')"
    $source
)

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败（csc 退出码 $LASTEXITCODE）" }

$item = Get-Item $outExe
"编译完成 -> {0}" -f $item.FullName
"大小     -> {0:N1} KB" -f ($item.Length / 1KB)

if (-not (Test-Path (Join-Path $outDir 'hotspot.ps1'))) {
    Write-Warning 'dist\hotspot.ps1 不存在 —— 请把脚本放到 exe 旁边才能运行。'
}
