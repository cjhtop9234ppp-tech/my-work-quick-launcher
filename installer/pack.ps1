<#
  배포 패키지(zip) 조립.
  publish\MyWorkQuickLauncher.exe + installer 스크립트 + 아이콘 + README 를 묶는다.
  결과물: dist\MyWorkQuickLauncher-1.1.0-win-x64.zip
  (압축 해제 후 exe를 바로 실행하면 무설치, INSTALL.cmd를 실행하면 설치)
#>
$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$version = '1.1.0'
$exe     = Join-Path $root 'publish\MyWorkQuickLauncher.exe'

if (-not (Test-Path $exe)) {
    throw "publish\MyWorkQuickLauncher.exe 가 없습니다. 먼저 상위 폴더에서 publish.cmd 를 실행하세요."
}

$stage = Join-Path $env:TEMP "mwql_pack_$version"
$distDir = Join-Path $root 'dist'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Copy-Item $exe (Join-Path $stage 'MyWorkQuickLauncher.exe')
Copy-Item (Join-Path $root 'assets\app.ico')          (Join-Path $stage 'app.ico')
Copy-Item (Join-Path $root 'README.md')               (Join-Path $stage 'README.md')
Copy-Item (Join-Path $root 'installer\INSTALL.cmd')   $stage
Copy-Item (Join-Path $root 'installer\UNINSTALL.cmd') $stage
Copy-Item (Join-Path $root 'installer\install.ps1')   $stage
Copy-Item (Join-Path $root 'installer\uninstall.ps1') $stage

$zip = Join-Path $distDir "MyWorkQuickLauncher-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "만들어짐: $zip  ($mb MB)" -ForegroundColor Green
