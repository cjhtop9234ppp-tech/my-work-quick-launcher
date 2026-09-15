@echo off
REM MY WORK QUICK LAUNCHER 제거. (설치 후에는 "프로그램 추가/제거"에서도 가능)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
