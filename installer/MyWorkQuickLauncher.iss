; MY WORK QUICK LAUNCHER - Inno Setup 스크립트
; 정식 설치 exe(MyWorkQuickLauncher-Setup-1.0.0.exe)를 만든다.
;
; 사용법:
;   1) https://jrsoftware.org/isdl.php 에서 Inno Setup 6 설치
;   2) 먼저 상위 폴더에서  publish.cmd  실행 (publish\MyWorkQuickLauncher.exe 생성)
;   3) 이 파일을 Inno Setup Compiler로 열고 Build  (또는  iscc MyWorkQuickLauncher.iss)
;   4) 결과물: installer\Output\MyWorkQuickLauncher-Setup-1.0.0.exe

#define AppName "MY WORK QUICK LAUNCHER"
#define AppId "MyWorkQuickLauncher"
#define AppVersion "1.0.0"
#define AppPublisher "Kim"
#define AppExe "MyWorkQuickLauncher.exe"

[Setup]
AppId={{8F2C4A19-6C3D-4E7B-9A11-4B0D2C0F1E20}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppId}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=Output
OutputBaseFilename=MyWorkQuickLauncher-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\app.ico

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; 사용자 데이터(%APPDATA%\MyWorkQuickLauncher)는 제거해도 남긴다.
[UninstallDelete]
Type: dirifempty; Name: "{app}"
