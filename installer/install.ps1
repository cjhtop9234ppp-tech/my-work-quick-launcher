<#
  MY WORK QUICK LAUNCHER - 무설치(관리자 권한 불필요) 설치 스크립트
  같은 폴더의 MyWorkQuickLauncher.exe 를 사용자 프로그램 폴더로 복사하고
  바탕화면 / 시작 메뉴 바로가기와 "프로그램 추가/제거" 항목을 만든다.
#>
[CmdletBinding()]
param(
    [switch]$NoDesktopShortcut,
    [switch]$Silent
)

$ErrorActionPreference = 'Stop'
$AppName    = 'MY WORK QUICK LAUNCHER'
$AppId      = 'MyWorkQuickLauncher'
$Version    = '1.0.0'
$Publisher  = 'Kim'
$ExeName    = 'MyWorkQuickLauncher.exe'
$IconName   = 'app.ico'

$here      = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sourceExe = Join-Path $here $ExeName
$sourceIco = Join-Path $here $IconName

if (-not (Test-Path $sourceExe)) {
    throw "$ExeName 을(를) 이 스크립트와 같은 폴더에서 찾을 수 없습니다: $here"
}

$installDir = Join-Path $env:LOCALAPPDATA "Programs\$AppId"
$targetExe  = Join-Path $installDir $ExeName
$targetIco  = Join-Path $installDir $IconName
$targetUninstall = Join-Path $installDir 'uninstall.ps1'

Write-Host "[1/5] 설치 폴더 준비: $installDir"
# 실행 중이면 종료
Get-Process -Name $AppId -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "      실행 중인 $AppId 종료..."
    $_ | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Write-Host "[2/5] 파일 복사"
Copy-Item $sourceExe $targetExe -Force
if (Test-Path $sourceIco) { Copy-Item $sourceIco $targetIco -Force }
$uninstallSource = Join-Path $here 'uninstall.ps1'
if (Test-Path $uninstallSource) { Copy-Item $uninstallSource $targetUninstall -Force }

$iconRef = if (Test-Path $targetIco) { $targetIco } else { $targetExe }

Write-Host "[3/5] 시작 메뉴 바로가기"
$wsh = New-Object -ComObject WScript.Shell
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$startLnk = Join-Path $startMenuDir "$AppName.lnk"
$sc = $wsh.CreateShortcut($startLnk)
$sc.TargetPath = $targetExe
$sc.WorkingDirectory = $installDir
$sc.IconLocation = $iconRef
$sc.Description = $AppName
$sc.Save()

if (-not $NoDesktopShortcut) {
    Write-Host "[4/5] 바탕화면 바로가기"
    $desktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"
    $dc = $wsh.CreateShortcut($desktopLnk)
    $dc.TargetPath = $targetExe
    $dc.WorkingDirectory = $installDir
    $dc.IconLocation = $iconRef
    $dc.Description = $AppName
    $dc.Save()
} else {
    Write-Host "[4/5] 바탕화면 바로가기 건너뜀"
}

Write-Host "[5/5] 프로그램 추가/제거 등록"
$uninstKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
New-Item -Path $uninstKey -Force | Out-Null
$sizeKb = [int]((Get-Item $targetExe).Length / 1024)
$cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$targetUninstall`""
Set-ItemProperty $uninstKey DisplayName     $AppName
Set-ItemProperty $uninstKey DisplayVersion  $Version
Set-ItemProperty $uninstKey Publisher       $Publisher
Set-ItemProperty $uninstKey DisplayIcon     $iconRef
Set-ItemProperty $uninstKey InstallLocation $installDir
Set-ItemProperty $uninstKey UninstallString $cmd
Set-ItemProperty $uninstKey QuietUninstallString "$cmd -Silent"
Set-ItemProperty $uninstKey EstimatedSize   $sizeKb -Type DWord
Set-ItemProperty $uninstKey NoModify        1 -Type DWord
Set-ItemProperty $uninstKey NoRepair        1 -Type DWord

Write-Host ""
Write-Host "설치 완료: $targetExe" -ForegroundColor Green
Write-Host "사용자 데이터는 %APPDATA%\MyWorkQuickLauncher 에 별도로 저장됩니다(제거해도 유지)."

if (-not $Silent) {
    $answer = Read-Host "지금 실행할까요? (Y/N)"
    if ($answer -match '^[Yy]') { Start-Process $targetExe }
}
