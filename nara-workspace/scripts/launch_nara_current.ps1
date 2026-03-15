$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    throw "dotnet.exe를 찾지 못했습니다: $dotnet"
}

$dllCandidates = @(
    "A:\c\review-build\동국문화재연구원_낙찰분석프로그램.dll",
    "A:\c\배포\동국문화재연구원_낙찰분석프로그램_현재본\동국문화재연구원_낙찰분석프로그램.dll"
)

$targetDll = $dllCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $targetDll) {
    throw "실행 가능한 DLL을 찾지 못했습니다. 먼저 review-build를 생성하세요."
}

$proc = Start-Process -FilePath $dotnet -ArgumentList @($targetDll) -WorkingDirectory (Split-Path $targetDll -Parent) -PassThru
Start-Sleep -Seconds 4
Get-Process -Id $proc.Id | Select-Object Id, ProcessName, MainWindowTitle, Path
