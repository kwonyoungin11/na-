$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$dll = 'A:\c\review-build\동국문화재연구원_낙찰분석프로그램.dll'
if (-not (Test-Path $dotnet)) { throw "dotnet.exe를 찾지 못했습니다: $dotnet" }
if (-not (Test-Path $dll)) { throw "review-build DLL을 찾지 못했습니다: $dll" }
$proc = Start-Process -FilePath $dotnet -ArgumentList @($dll) -WorkingDirectory 'A:\c\review-build' -PassThru
Start-Sleep -Seconds 4
Get-Process -Id $proc.Id | Select-Object Id, ProcessName, MainWindowTitle, Path
