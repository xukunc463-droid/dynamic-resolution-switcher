param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "Version 必须是语义化版本，例如 1.0.0 或 1.0.0-ci.1。当前值：$Version"
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $projectRoot "src\ResolutionSwitcher.cs"
$iconPath = Join-Path $projectRoot "assets\app.ico"
$releaseDirectory = Join-Path $projectRoot "release"
$exePath = Join-Path $releaseDirectory "DynamicResolutionSwitcher.exe"
$zipPath = Join-Path $releaseDirectory ("DynamicResolutionSwitcher-v{0}-win.zip" -f $Version)
$checksumPath = Join-Path $releaseDirectory "SHA256SUMS.txt"

$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "找不到 Windows 自带的 C# 编译器。请在 Windows 10/11 上运行此脚本。"
}

if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
    throw "找不到应用图标：$iconPath"
}

$packageFiles = @(
    $exePath,
    (Join-Path $projectRoot "README.md"),
    (Join-Path $projectRoot "README.en.md"),
    (Join-Path $projectRoot "LICENSE"),
    (Join-Path $projectRoot "assets"),
    (Join-Path $projectRoot "docs")
)

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

$compilerArguments = @(
    "/nologo",
    "/warn:4",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/reference:System.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    ("/win32icon:{0}" -f $iconPath),
    ("/out:{0}" -f $exePath),
    $sourcePath
)
& $compiler $compilerArguments

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

foreach ($packageFile in $packageFiles) {
    if (-not (Test-Path -LiteralPath $packageFile)) {
        throw "发布包缺少必需文件：$packageFile"
    }
}

Compress-Archive -LiteralPath $packageFiles -DestinationPath $zipPath -Force

$hashes = Get-FileHash -Algorithm SHA256 -LiteralPath @($exePath, $zipPath)
$lines = $hashes | ForEach-Object {
    "{0}  {1}" -f $_.Hash.ToLowerInvariant(), (Split-Path -Leaf $_.Path)
}
Set-Content -LiteralPath $checksumPath -Value $lines -Encoding ASCII

Write-Host ""
Write-Host "构建完成：" -ForegroundColor Green
Write-Host "  EXE: $exePath"
Write-Host "  ZIP: $zipPath"
Write-Host "  SHA: $checksumPath"
