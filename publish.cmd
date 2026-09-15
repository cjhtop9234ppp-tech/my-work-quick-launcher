@echo off
REM 배포용 단일 실행 파일 생성 (.NET 런타임 설치 불필요)
setlocal
cd /d "%~dp0"

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o "publish"

echo.
echo 결과물: "%~dp0publish\MyWorkQuickLauncher.exe"
pause
