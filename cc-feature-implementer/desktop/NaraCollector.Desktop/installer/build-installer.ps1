$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectDir = 'A:\c\cc-feature-implementer\desktop\NaraCollector.Desktop'
$installerDir = Join-Path $projectDir 'installer'
$publishDir = Join-Path $projectDir 'bin\Release\publish_installer'
$outputDir = Join-Path $installerDir 'output'
$wxsPath = Join-Path $installerDir 'DonggukBidAnalyzer.wxs'
$msiPath = Join-Path $outputDir 'DonggukBidAnalyzer_Setup.msi'
$wixExe = 'A:\c\.tools\.store\wix\6.0.2\wix\6.0.2\tools\net6.0\any\wix.exe'

if (!(Test-Path $wixExe)) {
    throw "wix.exe not found: $wixExe"
}

if (!(Test-Path $wxsPath)) {
    throw "installer source(.wxs) not found: $wxsPath"
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Write-Host "1) .NET publish (self-contained win-x64) ..."
dotnet publish (Join-Path $projectDir 'NaraCollector.Desktop.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed. exit=$LASTEXITCODE"
}

$mainExe = Join-Path $publishDir '동국문화재연구원_낙찰분석프로그램.exe'
if (!(Test-Path $mainExe)) {
    throw "Published exe not found: $mainExe"
}

Write-Host "2) Build MSI via WiX ..."
& $wixExe build `
    $wxsPath `
    -d PublishDir="$publishDir" `
    -arch x64 `
    -o $msiPath
if ($LASTEXITCODE -ne 0) {
    throw "wix build failed. exit=$LASTEXITCODE"
}

if (!(Test-Path $msiPath)) {
    throw "MSI not generated: $msiPath"
}

$msiInfo = Get-Item $msiPath
Write-Host "DONE: $($msiInfo.FullName)"
Write-Host "SIZE: $([math]::Round($msiInfo.Length / 1MB, 2)) MB"
