<#
  MY WORK QUICK LAUNCHER 제거 스크립트.
  설치 폴더/바로가기/레지스트리 항목을 지운다. 사용자 데이터(%APPDATA%)는 그대로 둔다.
#>
[CmdletBinding()]
param([switch]$Silent, [switch]$PurgeData)

$ErrorActionPreference = 'SilentlyContinue'
$AppName = 'MY WORK QUICK LAUNCHER'
$AppId   = 'MyWorkQuickLauncher'

$installDir = Join-Path $env:LOCALAPPDATA "Programs\$AppId"

Get-Process -Name $AppId -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# 바로가기
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk") -Force
Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName.lnk") -Force

# 레지스트리
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId" -Recurse -Force

# 사용자 데이터
if ($PurgeData) {
    Remove-Item (Join-Path $env:APPDATA 'MyWorkQuickLauncher') -Recurse -Force
    Write-Host "사용자 데이터(%APPDATA%\MyWorkQuickLauncher)도 삭제했습니다."
} else {
    Write-Host "사용자 데이터는 %APPDATA%\MyWorkQuickLauncher 에 보존됩니다."
}

# 설치 폴더 (자기 자신을 실행 중이므로 예약 삭제)
if (Test-Path $installDir) {
    $bat = Join-Path $env:TEMP "del_$AppId.cmd"
@"
@echo off
timeout /t 2 /nobreak >nul
rmdir /s /q "$installDir"
del "%~f0"
"@ | Set-Content -Path $bat -Encoding ASCII
    Start-Process -WindowStyle Hidden -FilePath cmd.exe -ArgumentList "/c `"$bat`""
}

Write-Host "$AppName 제거 완료." -ForegroundColor Green
if (-not $Silent) { Read-Host "엔터를 누르면 닫힙니다" }
