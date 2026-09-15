# 배포 / 설치

## 최종 사용자용 — 두 가지 방법

### A. 무설치 (권장, 가장 간단)
1. `MyWorkQuickLauncher.exe` 하나만 복사해서 원하는 곳에 둔다.
2. 더블클릭하면 바로 실행된다. .NET 런타임 설치 불필요(자체 포함, x64).
3. 설정·바로가기·문구는 `%APPDATA%\MyWorkQuickLauncher\data.json` 에 저장된다.

### B. 설치 (바탕화면·시작 메뉴 바로가기 + 프로그램 추가/제거 등록)
- **정식 설치 exe**: `installer\Output\MyWorkQuickLauncher-Setup-2.0.0.exe` 실행
  (이 exe는 아래 "빌드 담당자용" 절차로 먼저 만들어야 함)
- **무설치 스크립트**(추가 도구 불필요): `INSTALL.cmd` 더블클릭
  → `%LOCALAPPDATA%\Programs\MyWorkQuickLauncher\` 로 복사 + 바로가기 생성 + 제거 항목 등록
  → 제거는 `UNINSTALL.cmd` 또는 Windows "프로그램 추가/제거"

두 방법 모두 관리자 권한이 필요 없고, 사용자 데이터는 제거해도 `%APPDATA%` 에 남는다.

---

## 빌드 담당자용

### 1) 배포 exe 만들기
프로젝트 폴더에서:
```
publish.cmd
```
→ `publish\MyWorkQuickLauncher.exe` (약 47 MB, 자체 포함 단일 파일)

### 2) 배포 zip 만들기 (무설치 + 스크립트 설치 겸용)
```
powershell -ExecutionPolicy Bypass -File installer\pack.ps1
```
→ `dist\MyWorkQuickLauncher-2.0.0-win-x64.zip`
   (exe + INSTALL.cmd + UNINSTALL.cmd + install.ps1 + uninstall.ps1 + app.ico + README)

### 3) 정식 설치 exe 만들기 (선택)
1. [Inno Setup 6](https://jrsoftware.org/isdl.php) 설치
2. `publish.cmd` 를 먼저 실행해 둔다
3. `iscc installer\MyWorkQuickLauncher.iss` (또는 Inno Setup Compiler에서 Build)
4. → `installer\Output\MyWorkQuickLauncher-Setup-2.0.0.exe`

## 파일

| 파일 | 용도 |
| --- | --- |
| `install.ps1` / `uninstall.ps1` | 무설치 스크립트 설치/제거 (도구 불필요) |
| `INSTALL.cmd` / `UNINSTALL.cmd` | 위 스크립트를 더블클릭으로 실행 |
| `MyWorkQuickLauncher.iss` | Inno Setup 스크립트(정식 setup.exe) |
| `pack.ps1` | 배포 zip 조립 |
