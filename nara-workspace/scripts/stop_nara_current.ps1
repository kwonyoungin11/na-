Get-Process |
  Where-Object { $_.ProcessName -like '*동국문화재연구원_낙찰분석프로그램*' -or ($_.ProcessName -eq 'dotnet' -and $_.MainWindowTitle -like '*낙찰분석프로그램*') } |
  Stop-Process -Force -ErrorAction SilentlyContinue
