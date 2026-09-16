# MY WORK QUICK LAUNCHER — 프로젝트 재현 가이드

> 이 문서 하나만 보고 새 PC에서 그대로 재현할 수 있도록 작성했습니다.
> 소스코드는 요약 없이 전문을 포함합니다(§6).
> 문서 기준 버전: **v2.1.0(내부) / 공개 v1.1.0 / BUILD 20260916**

---

## 1. 프로젝트 개요

### 목적
Windows 개인 업무용 **퀵 런처**. 자주 쓰는 프로그램·웹사이트·폴더·파일 바로가기, 업무 문구
즉시 복사, 바탕화면 파일 빠른 검색, 그리고 견적 시트(gomail) 두 개의 작업항목 비교를
**한 창**에서 처리합니다. (원래 Codex가 만든 1차 버전을 같은 명세로 새로 작성한 것으로,
`%APPDATA%\MyWorkQuickLauncher\data.json` 스키마가 호환되어 기존 데이터가 그대로 이어집니다.)

### 사용 기술 스택
| 항목 | 값 |
| --- | --- |
| 언어 | C# 13 (`<LangVersion>latest</LangVersion>`) |
| UI 프레임워크 | Windows Forms (`<UseWindowsForms>true</UseWindowsForms>`) |
| 타깃 프레임워크 | `net10.0-windows` |
| SDK | .NET SDK **10.0.302** (개발 PC 기준) |
| 런타임 | 배포본은 self-contained(런타임 내장) → 대상 PC에 .NET 설치 불필요 |
| 외부 NuGet 패키지 | **없음** (BCL + WinForms만 사용) |
| 플랫폼 | Windows 10/11 x64 |
| 빌드 산출물 | 단일 실행 파일 `MyWorkQuickLauncher.exe` (약 47 MB, PublishSingleFile) |

### 주요 기능 요약
1. **바로가기** — 프로그램 / 웹사이트 / 폴더·자료 3묶음을 한 박스로. 카드 클릭 = 실행,
   우클릭 = 실행/편집/삭제, 드래그로 같은 영역 내 순서 변경, Explorer에서 드래그앤드롭 등록.
   최초 등록 시 아이콘을 base64로 캐시(`IconData`) → 이름을 바꿔도 아이콘 유지.
2. **시트 분석 TOOL** — A시트(공임비교분석) URL과 B시트(업로드 로그) URL을 각각 1개 입력 →
   두 HTML 표의 **2번째 열(작업항목)**을 비교 → A시트 전체 견적을 팝업으로 보여주고
   **B시트에 없는 작업항목 행만 노란색 음영**. 노란색 행 클릭 시 클립보드로 복사(업무 메모와 동일).
3. **업무 메모 / 자주 쓰는 문구** — 그룹 필터 + 타일. 좌클릭 = 즉시 복사(마지막 선택 파란색),
   더블클릭/우클릭 = 편집. 클립보드 실패 시 4회 재시도.
4. **바탕화면 파일검색** — 바탕화면(및 OneDrive\Desktop) **최상위 파일만**(하위 폴더 제외).
   확장자 필터 8종 + 파일명 검색, 최근 수정순, 이미지 썸네일(백그라운드 로딩),
   1클릭 = Explorer에서 선택, 더블클릭 = 실행. 검색은 Generation 번호로 오래된 결과 폐기.
5. **파일자동읽기 폴더지정** (v1.1.0 추가) — 지정한 폴더(기본 힌트: 다운로드)를 `FileSystemWatcher`로
   감시하다가 이미지가 포함된 `.zip`이 도착하면, 등록된 바로가기 중 선택한 프로그램을 그 zip 경로를
   인자로 붙여 자동 실행. 뒤이어 압축 프로그램이 띄우는 "압축풀기" 류 확인 창은 제목에 포함된
   문자열로 감지해 `WM_CLOSE`로 자동으로 닫는다(최대 10초 동안 폴링).
6. 공통 — "항상 위"(세션 한정, 저장 안 함 — 시작 시 항상 꺼짐), 미니모드, 창 위치/크기 저장·복원(다중 모니터 보정),
   `%APPDATA%` 원자적 저장(임시파일 → 교체), `launcher.log` 이벤트 로그,
   커서 위치 기준 휠 스크롤(`IMessageFilter`).

---

## 2. 폴더 / 파일 구조

```
6. MyWorkQuickLauncher/
├── MyWorkQuickLauncher.csproj      프로젝트 정의(타깃 프레임워크, WinForms, 앱 아이콘)
├── Program.cs                      진입점 Main() + WheelRedirector(IMessageFilter)
├── Model.cs                        데이터 모델(ShortcutItem/NoteItem/AppSettings/AppData/
│                                   DesktopFileItem) + AppStore(로드/원자적 저장/로그)
├── Native.cs                       Win32 상호운용(SHGetFileInfo 등) + IconResolver(아이콘 결정/캐시)
├── Theme.cs                        색상·폰트 상수 + 버튼 팩토리 + Ellipsis 헬퍼
├── MainForm.cs                     메인 창: 셸/헤더/상태바, 레이아웃(반응형 폭 계산),
│                                   1.바로가기 섹션, 3.업무 메모 섹션, 실행/드래그드롭/
│                                   지오메트리/미니모드/클립보드
├── MainForm.DesktopSearch.cs       partial MainForm: 4.바탕화면 파일검색(스캔/필터/렌더/썸네일)
├── MainForm.SheetAnalysis.cs       partial MainForm: 2.시트 분석 TOOL 섹션 + 실행 로직
├── MainForm.FolderWatch.cs         partial MainForm: 5.파일자동읽기 폴더지정 (v1.1.0 추가)
├── SheetAnalysis.cs                SheetRow(record) + SheetSource(HTTP fetch + HTML 표 파서)
├── SheetAnalysisDialog.cs          결과 팝업(DataGridView, 노란색 음영, 클릭 복사)
├── Dialogs.cs                      ShortcutDialog(바로가기 편집) + NoteDialog(문구 편집)
├── README.md                       사용/변경 요약
├── REPRODUCTION_GUIDE.md           (이 문서)
├── publish.cmd                     배포용 단일 exe 생성 스크립트
├── assets/
│   └── app.ico                     앱 아이콘(256px, 번개 문양)
├── installer/
│   ├── install.ps1                 무설치(관리자 불필요) 설치 스크립트
│   ├── uninstall.ps1               제거 스크립트
│   ├── INSTALL.cmd / UNINSTALL.cmd 위 스크립트를 더블클릭 실행
│   ├── MyWorkQuickLauncher.iss     Inno Setup 스크립트(정식 setup.exe)
│   ├── pack.ps1                    배포 zip 조립
│   └── README.md                   배포/설치 안내
├── bin/  obj/                      빌드 중간 산출물(재현 시 자동 생성, 커밋 불필요)
├── publish/                        publish.cmd 결과물(MyWorkQuickLauncher.exe)
└── dist/                           pack.ps1 결과물(MyWorkQuickLauncher-2.0.0-win-x64.zip)
```

### 핵심 파일 한 줄 설명
| 파일 | 역할 |
| --- | --- |
| `Program.cs` | STA 진입점. HighDPI/VisualStyles 설정, 휠 리다이렉트 메시지 필터 등록, 최상위 예외 처리 |
| `Model.cs` | 직렬화 대상 클래스들과 `AppStore`(JSON 로드·**원자적** 저장·append 로그) |
| `Native.cs` | `SHGetFileInfo`로 Explorer와 동일한 아이콘 추출, `SetForegroundWindow`; `IconResolver`는 지정 아이콘→IconData→셸 추출 순으로 결정 |
| `Theme.cs` | 앱 전역 색/폰트, `FlatButton`·`ActionButton` 팩토리, 말줄임 헬퍼 |
| `MainForm.cs` | 창 골격, `RebuildStack()`(데이터 기준 UI 재생성), `ApplyResponsiveWidths()`(FlowLayoutPanel 폭 고정), 바로가기 CRUD, 문구 CRUD/복사, 드래그드롭/순서변경, 창 지오메트리/미니모드 |
| `MainForm.DesktopSearch.cs` | 바탕화면 최상위 스캔(비재귀), 확장자 필터 정의/적용, 결과 30개 렌더, 썸네일 백그라운드 로딩+캐시, 단일/더블클릭 분기 |
| `MainForm.SheetAnalysis.cs` | 시트 분석 섹션 UI, `RunSheetAnalysis()`(두 URL 동시 fetch → 작업항목 집합 차집합 → 팝업) |
| `MainForm.FolderWatch.cs` | 5.파일자동읽기 폴더지정 UI, `FileSystemWatcher`로 zip 감지 → 이미지 포함 확인 → 등록된 프로그램 실행 → 압축 확인 창 자동 닫기 |
| `SheetAnalysis.cs` | `HttpClient`(정적 재사용, gzip 자동 해제), 정규식으로 `<tr class='tableRow'>` 행에서 6칸 파싱, `ItemKey`(공백 제거 비교키) |
| `SheetAnalysisDialog.cs` | `DataGridView` 6열, 누락 행 노란색, 셀 클릭→복사, 우클릭 메뉴, "누락만 보기"/"전체 복사" |
| `Dialogs.cs` | 절대좌표 배치의 고정 크기 모달 2개(바로가기/문구) |

---

## 3. 의존성 및 버전

### 외부 패키지 파일 — 없음
NuGet `PackageReference`가 **하나도 없습니다.** BCL과 Windows Desktop(WinForms/GDI+)만 사용합니다.
따라서 `packages.lock.json` / `requirements.txt` / `pyproject.toml` 같은 파일이 존재하지 않습니다.
의존성 목록은 아래 `.csproj` 전문이 전부입니다(§6-A).

### 필요한 런타임 / 도구 버전

| 도구 | 버전 | 확인 명령 | 이 프로젝트에서 확인된 값 |
| --- | --- | --- | --- |
| .NET SDK | 10.0.x (10.0.302 검증) | `dotnet --version` | `10.0.302` |
| .NET SDK 목록 | — | `dotnet --list-sdks` | `10.0.302 [C:\Program Files\dotnet\sdk]` |
| Windows | 10 (19045) 이상 / 11 | `winver` | Windows 10 Pro 10.0.19045 |
| (선택) Inno Setup | 6.x | — | 정식 setup.exe를 만들 때만 필요 |

> **왜 net10인가**: 개발 PC에 설치된 SDK가 10.0.302뿐이며 1차 버전도 `net10.0-windows`였습니다.
> net8/net9 SDK로 재현하려면 `.csproj`의 `<TargetFramework>`를 `net8.0-windows`로 낮추면
> 그대로 빌드됩니다(코드는 net8 호환. `Application.SetHighDpiMode`, `record`, 파일 범위 namespace,
> `ArgumentNullException` 등 모두 net8 지원).

### 개발/빌드 PC에 필요한 것
- **빌드만**: .NET SDK 10 (`winget install Microsoft.DotNet.SDK.10` 또는 https://dotnet.microsoft.com/download)
- **실행만(배포본)**: 아무것도 필요 없음. `MyWorkQuickLauncher.exe`는 self-contained.
- Visual Studio는 필요 없음. `dotnet` CLI로 충분.

---

## 4. 환경 설정

### 환경변수 — 없음
`.env` / `appsettings.json` 을 사용하지 않습니다. 비밀값도 코드/설정에 없습니다.
시트 분석 TOOL이 접속하는 gomail 주소는 **사용자가 실행 중 직접 입력**하며
마지막 입력값만 `data.json`에 평문 저장됩니다(사내 URL, 비밀값 아님).

`.env.example` 에 해당하는 항목이 있다면 아래가 전부입니다(모두 런타임에 자동 생성/입력):

```dotenv
# (환경변수 없음)
# APPDATA               Windows 기본 제공. 데이터 저장 위치의 부모 폴더.
# LOCALAPPDATA          Windows 기본 제공. installer가 설치 경로로 사용.
```

### 설정 파일 = `%APPDATA%\MyWorkQuickLauncher\data.json`
프로그램이 첫 실행 시 자동 생성합니다. 스키마와 최소 예시:

```jsonc
{
  "Shortcuts": [
    {
      "Id": "11111111-1111-1111-1111-111111111111", // GUID 문자열, 자동 생성
      "Kind": "App",                                 // "App" | "Web" | "Path" (JsonStringEnum)
      "Name": "카카오톡",                             // 카드에 표시되는 이름
      "Path": "C:\\Program Files (x86)\\Kakao\\KakaoTalk\\KakaoTalk.exe", // 실행 경로 또는 URL
      "Arguments": "",                               // App일 때만 사용
      "Icon": "",                                    // 사용자가 지정한 .ico/.png 경로(선택)
      "IconData": "iVBORw0KGgoAAAANSUhEUg...",       // 최초 캡처 아이콘(base64 PNG). 비어 있으면 첫 실행 시 채움
      "Order": 0                                     // 같은 Kind 안에서의 표시 순서(0부터)
    }
  ],
  "Notes": [
    {
      "Id": "22222222-2222-2222-2222-222222222222",
      "Title": "예시문구",       // 타일에 표시. 본문이 비면 이 값이 복사됨
      "Text": "",               // 복사되는 본문(있으면 우선)
      "Group": "일반"           // 그룹 필터용
    }
  ],
  "Settings": {
    // "AlwaysOnTop" 은 더 이상 저장하지 않는다("항상 위"는 세션 한정). 옛 값이 있어도 무시됨.
    "MiniMode": false,
    "Geometry": "1000x740+80+60",       // "WxH+X+Y" (일반 모드 창)
    "MiniGeometry": "320x260+80+60",    // 미니 모드 창
    "LeftSheetUrl": "",                 // 시트 분석 A시트 마지막 입력 URL
    "RightSheetUrl": "",                // 시트 분석 B시트 마지막 입력 URL
    "WatchFolder": "",                  // 감시 폴더 (v1.1.0, 비우면 미사용)
    "WatchEnabled": false,              // 자동 감시 사용 여부 (v1.1.0)
    "WatchProgramShortcutId": "",       // zip 도착 시 실행할 바로가기 Id (v1.1.0)
    "WatchCloseWindowTitleContains": "압축풀기"  // 자동으로 닫을 창 제목(포함 문자열) (v1.1.0)
  }
}
```

**완전 새로 시작하려면**: 위 파일을 삭제하고 실행하면 아래 기본값으로 생성됩니다.
```json
{
  "Shortcuts": [],
  "Notes": [ { "Id": "<GUID>", "Title": "여기에 자주 쓰는 문구를 추가하세요", "Text": "", "Group": "일반" } ],
  "Settings": { "MiniMode": false, "Geometry": "1000x740+80+60", "MiniGeometry": "320x260+80+60", "LeftSheetUrl": "", "RightSheetUrl": "", "WatchFolder": "", "WatchEnabled": false, "WatchProgramShortcutId": "", "WatchCloseWindowTitleContains": "압축풀기" }
}
```

### `.csproj` 외 별도 config 파일 없음
`launchSettings.json`, `app.config`, `nuget.config` 등을 사용하지 않습니다.

---

## 5. 설치 및 실행 순서

### A. 소스에서 빌드·실행 (개발 재현)

```bat
:: 1) 소스 폴더로 이동
cd "6. MyWorkQuickLauncher"

:: 2) 복원 + 빌드 (복원은 build에 포함, 외부 패키지가 없어 오프라인도 가능)
dotnet build -c Release

:: 3) 실행 (둘 중 하나)
dotnet run -c Release
:: 또는
".\bin\Release\net10.0-windows\MyWorkQuickLauncher.exe"
```

### B. 배포용 단일 exe 생성

```bat
cd "6. MyWorkQuickLauncher"
publish.cmd
:: == 내부적으로 실행되는 명령 ==
:: dotnet publish -c Release -r win-x64 --self-contained true ^
::   -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
::   -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "publish"
::
:: 결과물: publish\MyWorkQuickLauncher.exe  (약 47 MB, .NET 설치 불필요)
```

### C. 배포 zip 만들기 (무설치 + 설치 스크립트 동봉)

```bat
cd "6. MyWorkQuickLauncher"
publish.cmd
powershell -ExecutionPolicy Bypass -File installer\pack.ps1
:: 결과물: dist\MyWorkQuickLauncher-2.0.0-win-x64.zip  (약 41.5 MB)
```

### D. 최종 사용자 설치 (대상 PC)

- **무설치**: zip 풀고 `MyWorkQuickLauncher.exe` 더블클릭. 끝.
- **설치(바로가기+제거항목)**: zip 풀고 `INSTALL.cmd` 더블클릭
  → `%LOCALAPPDATA%\Programs\MyWorkQuickLauncher\`에 복사, 시작 메뉴/바탕화면 바로가기,
    "프로그램 추가/제거" 등록. 제거는 `UNINSTALL.cmd` 또는 제어판.
- **정식 setup.exe**: `installer\MyWorkQuickLauncher.iss`를 Inno Setup 6로 Build
  → `installer\Output\MyWorkQuickLauncher-Setup-2.0.0.exe`

> 관리자 권한 불필요. 사용자 데이터(`%APPDATA%\MyWorkQuickLauncher`)는 제거해도 남습니다.

---

## 6. 핵심 소스코드 전체

> 아래는 v1.1.0(내부 v2.1.0) 최종본의 **전문**입니다. 순서: A) csproj → B) Program.cs → C) Model.cs →
> D) Native.cs → E) Theme.cs → F) MainForm.cs → G) MainForm.DesktopSearch.cs →
> H) MainForm.SheetAnalysis.cs → I) SheetAnalysis.cs → J) SheetAnalysisDialog.cs →
> K) Dialogs.cs → L) publish.cmd → M) installer 스크립트 → N) MainForm.FolderWatch.cs(v1.1.0 추가)

### A. `MyWorkQuickLauncher.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AssemblyName>MyWorkQuickLauncher</AssemblyName>
    <RootNamespace>MyWorkQuickLauncher</RootNamespace>
    <Version>2.0.0</Version>
    <Product>MY WORK QUICK LAUNCHER</Product>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    <ApplicationIcon>assets\app.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <None Include="assets\app.ico" />
  </ItemGroup>

</Project>
```

### B. `Program.cs`

```csharp
using System.Runtime.InteropServices;

namespace MyWorkQuickLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 마우스 커서 아래의 스크롤 영역으로 휠 이벤트를 넘겨준다(포커스 없어도 스크롤).
        Application.AddMessageFilter(new WheelRedirector());

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            AppStore.Log("[FATAL] unhandled", ex);
            MessageBox.Show(
                $"예기치 못한 오류로 종료되었습니다.\n\n{ex.Message}\n\n로그: {AppStore.LogFile}",
                "MY WORK QUICK LAUNCHER",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// WinForms는 휠 메시지를 포커스 컨트롤로만 보낸다. 이 필터는 커서 위치의
/// 스크롤 가능한 컨트롤을 찾아 휠 메시지를 다시 전달한다.
/// </summary>
internal sealed class WheelRedirector : IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL) return false;

        int x = unchecked((short)(long)m.LParam);
        int y = unchecked((short)((long)m.LParam >> 16));

        var control = Control.FromChildHandle(WindowFromPoint(new Point(x, y)));
        for (var current = control; current != null; current = current.Parent)
        {
            if (current is ScrollableControl scrollable
                && scrollable.AutoScroll
                && scrollable.VerticalScroll.Visible
                && current.IsHandleCreated)
            {
                SendMessage(current.Handle, WM_MOUSEWHEEL, m.WParam, m.LParam);
                return true;
            }
        }

        return false;
    }
}
```

### C. `Model.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyWorkQuickLauncher;

public enum ShortcutKind { App, Web, Path }

/// <summary>프로그램 / 웹사이트 / 폴더·파일 바로가기 한 개.</summary>
public sealed class ShortcutItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ShortcutKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";

    /// <summary>사용자가 직접 지정한 아이콘 파일 경로(선택).</summary>
    public string Icon { get; set; } = "";

    /// <summary>최초 등록 시점에 캡처한 아이콘(base64 PNG). 이름을 바꿔도 이 값은 유지된다.</summary>
    public string IconData { get; set; } = "";

    public int Order { get; set; }
}

/// <summary>자주 쓰는 업무 문구.</summary>
public sealed class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Group { get; set; } = "일반";
}

public sealed class AppSettings
{
    // AlwaysOnTop 은 더 이상 저장/복원하지 않는다(다른 창을 가리는 상태로 갇히는 문제).
    // 옛 data.json 에 이 값이 있어도 System.Text.Json 이 무시하므로 문제없다.
    public bool MiniMode { get; set; }
    public string Geometry { get; set; } = "1000x740+80+60";
    public string MiniGeometry { get; set; } = "320x260+80+60";

    /// <summary>시트 분석 Tool에서 마지막으로 사용한 URL(다음 실행 시 복원).</summary>
    public string LeftSheetUrl { get; set; } = "";
    public string RightSheetUrl { get; set; } = "";

    /// <summary>파일자동읽기 폴더지정: 이 폴더에 새 zip이 도착하면 감시한다(예: 다운로드 폴더).</summary>
    public string WatchFolder { get; set; } = "";
    public bool WatchEnabled { get; set; }

    /// <summary>zip 도착 시 실행할 바로가기(App 종류)의 Id. Shortcuts 목록에서 선택한다.</summary>
    public string WatchProgramShortcutId { get; set; } = "";

    /// <summary>이 문자열을 제목에 포함한 창(예: 알집의 "압축풀기")이 뜨면 자동으로 닫는다. 비우면 끔.</summary>
    public string WatchCloseWindowTitleContains { get; set; } = "압축풀기";
}

public sealed class AppData
{
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public List<NoteItem> Notes { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

/// <summary>바탕화면 검색 결과 한 건.</summary>
public sealed class DesktopFileItem
{
    public string FullPath = "";
    public string Name = "";
    public string Folder = "";
    public string Ext = "";
    public DateTime Modified;
}

/// <summary>data.json 로드/저장과 로그. 저장은 임시 파일 → 교체 방식으로 원자적으로 처리한다.</summary>
public static class AppStore
{
    static readonly string Dir =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyWorkQuickLauncher");

    public static readonly string DataFile = System.IO.Path.Combine(Dir, "data.json");
    public static readonly string LogFile = System.IO.Path.Combine(Dir, "launcher.log");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppData Load()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                var loaded = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataFile), JsonOptions);
                if (loaded != null)
                {
                    loaded.Shortcuts ??= new();
                    loaded.Notes ??= new();
                    loaded.Settings ??= new();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log("[DATA] load failed - starting with defaults", ex);
        }

        return new AppData
        {
            Notes =
            {
                new NoteItem { Title = "여기에 자주 쓰는 문구를 추가하세요", Text = "", Group = "일반" },
            },
        };
    }

    public static void Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var tmp = DataFile + ".tmp";
            File.WriteAllText(tmp, json);
            File.Copy(tmp, DataFile, overwrite: true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Log("[DATA] save failed", ex);
        }
    }

    public static void Log(string message, Exception? error = null)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var suffix = error == null ? "" : $" | {error.GetType().Name}: {error.Message}";
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{suffix}{Environment.NewLine}");
        }
        catch
        {
            // 로그 실패가 프로그램을 멈추게 해서는 안 된다.
        }
    }
}
```

### D. `Native.cs`

```csharp
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MyWorkQuickLauncher;

/// <summary>Windows 셸(Explorer)과 동일한 아이콘을 얻기 위한 얇은 상호운용 계층.</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    private const uint WM_CLOSE = 0x0010;

    /// <summary>보이는 최상위 창 중 제목에 <paramref name="titleContains"/>가 포함된 것을 모두 찾는다. (v1.1.0)</summary>
    internal static List<IntPtr> FindVisibleWindowsByTitle(string titleContains)
    {
        var found = new List<IntPtr>();
        if (string.IsNullOrWhiteSpace(titleContains)) return found;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(titleContains, StringComparison.OrdinalIgnoreCase))
                    found.Add(hWnd);
            }
            catch
            {
                // 개별 창 조회 실패는 전체 열거를 멈추지 않는다.
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>창에 닫기(WM_CLOSE)를 보낸다. 강제 종료가 아니라 창 자신의 닫기 처리를 그대로 따른다. (v1.1.0)</summary>
    internal static void RequestCloseWindow(IntPtr hWnd) => PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    /// <summary>경로(파일/폴더/.lnk/.url/존재하지 않는 경로)에 대해 Explorer가 쓰는 아이콘을 비트맵으로 돌려준다.</summary>
    internal static Bitmap? ShellIcon(string path, bool large = true)
    {
        try
        {
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);

            bool onDisk = File.Exists(path) || Directory.Exists(path);
            uint attr;
            if (!onDisk)
            {
                // 경로가 없어도 확장자만으로 연결 아이콘을 얻는다.
                flags |= SHGFI_USEFILEATTRIBUTES;
                attr = FILE_ATTRIBUTE_NORMAL;
            }
            else
            {
                attr = Directory.Exists(path) ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            }

            var result = SHGetFileInfo(path, attr, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(info.hIcon);
                return new Bitmap(icon.ToBitmap());
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>바로가기 아이콘을 결정한다. 우선순위: 지정 아이콘 파일 → 저장된 IconData → 셸 추출.</summary>
internal static class IconResolver
{
    public static Image Placeholder(ShortcutKind kind)
        => (kind == ShortcutKind.Web ? SystemIcons.Information : SystemIcons.Application).ToBitmap();

    public static Image? FromBase64(string base64)
    {
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(base64));
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    public static string ToBase64(Image? image)
    {
        try
        {
            if (image == null) return "";
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
        catch
        {
            return "";
        }
    }

    public static Image? Resolve(ShortcutItem item)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(item.Icon) && File.Exists(item.Icon))
            {
                if (string.Equals(Path.GetExtension(item.Icon), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var ico = new Icon(item.Icon);
                    return ico.ToBitmap();
                }

                using var stream = new MemoryStream(File.ReadAllBytes(item.Icon));
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
        }
        catch
        {
            // 지정 아이콘이 깨졌으면 다음 후보로 넘어간다.
        }

        if (!string.IsNullOrWhiteSpace(item.IconData))
        {
            var fromData = FromBase64(item.IconData);
            if (fromData != null) return fromData;
        }

        return Extract(item.Path, item.Kind);
    }

    /// <summary>경로에서 아이콘을 직접 추출한다(느릴 수 있으므로 최초 1회만 호출하고 IconData로 캐시).</summary>
    public static Image? Extract(string path, ShortcutKind kind)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var shell = Native.ShellIcon(path, large: true);
                if (shell != null) return shell;

                if (File.Exists(path))
                {
                    var associated = Icon.ExtractAssociatedIcon(path);
                    if (associated != null) return associated.ToBitmap();
                }
            }
        }
        catch
        {
            // 무시하고 기본 아이콘 사용
        }

        return Placeholder(kind);
    }
}
```

### E. `Theme.cs`

```csharp
namespace MyWorkQuickLauncher;

/// <summary>색상, 폰트, 자주 쓰는 컨트롤 팩토리를 한곳에 모은다.</summary>
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(244, 246, 248);
    public static readonly Color Card = Color.White;
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color Border = Color.FromArgb(210, 218, 227);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);
    public static readonly Color Field = Color.FromArgb(251, 253, 255);

    public static readonly Font Font9 = new("Malgun Gothic", 9F);
    public static readonly Font Font9B = new("Malgun Gothic", 9F, FontStyle.Bold);
    public static readonly Font Font11B = new("Malgun Gothic", 11F, FontStyle.Bold);
    public static readonly Font Font8 = new("Malgun Gothic", 8.25F);

    public static Button FlatButton(string text, Color back, Color fore)
    {
        var button = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = Font9,
            Padding = new Padding(12, 6, 12, 6),
            Cursor = Cursors.Hand,
            TabStop = true,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    public static Button ActionButton(string text)
    {
        var button = new Button
        {
            Text = text,
            BackColor = Card,
            ForeColor = Accent,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Font = Font8,
            Padding = new Padding(10, 4, 10, 4),
            Cursor = Cursors.Hand,
            TabStop = true,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Accent;
        return button;
    }

    public static string Ellipsis(string value, int max)
        => string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max] + "…";
}
```

### F. `MainForm.cs`

```csharp
using System.Diagnostics;

namespace MyWorkQuickLauncher;

public sealed partial class MainForm : Form
{
    private const string BuildLabel = "20260916";

    private readonly AppData _data;
    private bool _miniMode;
    private bool _applyingGeometry;
    private bool _iconDataDirty;

    // 아이콘 원본을 Id 기준으로 캐시하고, 카드에는 복제본을 넘긴다(카드 Dispose 시 원본 보호).
    private readonly Dictionary<string, Image> _iconMasters = new();

    private Panel _scroll = null!;
    private TableLayoutPanel _stack = null!;
    private Panel _header = null!;
    private Label _status = null!;
    private CheckBox _topCheck = null!;
    private Button _miniButton = null!;
    private System.Windows.Forms.Timer? _statusTimer;

    private FlowLayoutPanel _phraseFlow = null!;
    private ComboBox _groupCombo = null!;
    private string? _selectedPhraseId;

    public MainForm()
    {
        _data = AppStore.Load();
        NormalizeOrder();
        _miniMode = _data.Settings.MiniMode;
        AppStore.Log($"[APP START] BUILD={BuildLabel} EXE={Environment.ProcessPath}");

        SuspendLayout();
        Text = $"MY WORK QUICK LAUNCHER  ·  BUILD {BuildLabel}";
        Font = Theme.Font9;
        BackColor = Theme.Bg;
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.Sizable;
        AllowDrop = true;
        KeyPreview = true;
        DoubleBuffered = true;

        BuildBody();
        BuildHeader();
        BuildStatusBar();

        ApplyGeometry(_miniMode ? _data.Settings.MiniGeometry : _data.Settings.Geometry);
        // 항상 위(TopMost)는 시작할 때 항상 꺼진 상태로 둔다. 저장/복원하지 않으므로
        // 다른 창(브라우저, 카톡 등)을 가리는 상태로 갇히지 않는다. 필요하면 헤더의
        // "항상 위" 체크박스로 현재 세션에만 켤 수 있다.
        TopMost = false;
        ResumeLayout(true);

        DragEnter += OnAnyDragEnter;
        DragDrop += OnAnyDragDrop;
        KeyDown += OnGlobalKeyDown;

        Shown += (_, _) =>
        {
            ApplyMode();
            if (_iconDataDirty)
            {
                Persist();
                _iconDataDirty = false;
            }
            RunDesktopScan();
            ApplyWatchState();
        };
        ResizeEnd += (_, _) => SaveGeometry();
    }

    // ---------------------------------------------------------------- shell

    private void BuildHeader()
    {
        _header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.HeaderBg };

        var brand = new Label
        {
            Text = "MY WORK QUICK LAUNCHER",
            ForeColor = Color.White,
            Font = Theme.Font11B,
            AutoSize = true,
            Location = new Point(16, 12),
        };
        _header.Controls.Add(brand);

        _topCheck = new CheckBox
        {
            Text = "항상 위",
            Checked = false,
            ForeColor = Color.White,
            BackColor = Theme.HeaderBg,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        // 세션 한정: 저장하지 않는다. 다음 실행 때는 다시 꺼진 상태로 시작한다.
        _topCheck.CheckedChanged += (_, _) =>
        {
            TopMost = _topCheck.Checked;
            Flash(_topCheck.Checked
                ? "항상 위: 켜짐 (이 창이 다른 창 위에 표시됩니다 · 종료하면 해제)"
                : "항상 위: 꺼짐");
        };

        _miniButton = Theme.FlatButton("미니모드", Color.FromArgb(51, 65, 85), Color.White);
        _miniButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _miniButton.Click += (_, _) => ToggleMini();

        _header.Controls.Add(_topCheck);
        _header.Controls.Add(_miniButton);

        void LayoutHeader()
        {
            _miniButton.Location = new Point(
                _header.ClientSize.Width - _miniButton.Width - 12,
                (_header.Height - _miniButton.Height) / 2);
            _topCheck.Location = new Point(
                _miniButton.Left - _topCheck.Width - 16,
                (_header.Height - _topCheck.Height) / 2);
        }

        _header.Resize += (_, _) => LayoutHeader();
        _header.HandleCreated += (_, _) => LayoutHeader();

        Controls.Add(_header);
        LayoutHeader();
    }

    private void BuildBody()
    {
        _scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Bg,
            Padding = new Padding(16, 12, 16, 16),
        };

        _stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Bg,
            Margin = new Padding(0),
        };
        _stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _scroll.Controls.Add(_stack);
        Controls.Add(_scroll);

        _scroll.Resize += (_, _) => ApplyResponsiveWidths();
        _scroll.HandleCreated += (_, _) => ApplyResponsiveWidths();
        ApplyResponsiveWidths();

        RebuildStack();
    }

    /// <summary>
    /// 스택과 카드 영역의 폭을 뷰포트 폭에 고정한다. FlowLayoutPanel은 폭이 확정돼야
    /// 줄바꿈 높이를 정확히 계산하므로, 폭을 명시해 세로 공백이 생기는 것을 막는다.
    /// </summary>
    private void ApplyResponsiveWidths()
    {
        if (_scroll == null || _stack == null) return;

        int width = _scroll.ClientSize.Width - _scroll.Padding.Horizontal;
        if (width < 220) width = 220;

        _stack.MinimumSize = new Size(width, 0);
        _stack.MaximumSize = new Size(width, 0);

        // 부모(박스)의 실제 폭을 알아야 카드 FlowLayoutPanel의 줄바꿈이 정확하다.
        _stack.PerformLayout();

        foreach (var control in Descendants(_stack))
        {
            if (control is not FlowLayoutPanel flow) continue;
            // 이 세 패널은 고정 크기 + 내부 스크롤/앵커를 쓰므로 건드리지 않는다.
            if (ReferenceEquals(flow, _phraseFlow) ||
                ReferenceEquals(flow, _resultsFlow) ||
                ReferenceEquals(flow, _filterFlow))
                continue;

            var parent = flow.Parent;
            if (parent == null) continue;

            int available = parent.ClientSize.Width
                            - parent.Padding.Horizontal
                            - flow.Margin.Horizontal;
            if (available < 80) available = 80;

            flow.MinimumSize = new Size(available, 0);
            flow.MaximumSize = new Size(available, 0);
        }

        _stack.PerformLayout();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var grandChild in Descendants(child))
                yield return grandChild;
        }
    }

    private void BuildStatusBar()
    {
        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Padding = new Padding(14, 5, 8, 0),
            Text = "준비됨 · Explorer에서 파일을 이 창으로 끌어오면 바로 등록됩니다 · F5 새로고침",
        };
        Controls.Add(_status);
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            RunDesktopScan();
            Flash("바탕화면을 다시 검색합니다...");
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.M)
        {
            ToggleMini();
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- stack

    private void RebuildStack()
    {
        var previous = _stack.Controls.Cast<Control>().ToArray();

        _stack.SuspendLayout();
        _stack.Controls.Clear();
        _stack.RowStyles.Clear();
        _stack.RowCount = 0;

        AddRow(BuildShortcutGroupSection());

        if (!_miniMode)
        {
            AddRow(BuildSheetAnalysisSection());
            AddRow(BuildNotesSection());
            AddRow(BuildDesktopSection());
            AddRow(BuildFolderWatchSection());
        }

        _stack.ResumeLayout(true);

        foreach (var control in previous)
            control.Dispose();

        ApplyResponsiveWidths();
        RefreshPhraseGroups();
        RefreshFilterButtons();
        ApplyFilter();
    }

    private void AddRow(Control section)
    {
        section.Dock = DockStyle.Fill;
        section.Margin = new Padding(0, 0, 0, 12);
        _stack.RowCount++;
        _stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _stack.Controls.Add(section, 0, _stack.RowCount - 1);
    }

    /// <summary>제목 + "추가" 버튼 헤더와 본문을 담은 세로 2행 컨테이너.</summary>
    private static TableLayoutPanel SectionShell(string title, string? addText, EventHandler? onAdd, Control body)
    {
        var shell = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            BackColor = Theme.Bg,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var head = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Bg,
            Margin = new Padding(0, 0, 0, 4),
        };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        head.Controls.Add(new Label
        {
            Text = title,
            Font = Theme.Font11B,
            ForeColor = Theme.Text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 6, 0, 4),
        }, 0, 0);

        if (addText != null && onAdd != null)
        {
            var add = Theme.ActionButton(addText);
            add.Anchor = AnchorStyles.Right;
            add.Margin = new Padding(0, 2, 2, 2);
            add.Click += onAdd;
            head.Controls.Add(add, 1, 0);
        }

        shell.Controls.Add(head, 0, 0);
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        body.Dock = DockStyle.Fill;
        body.Margin = new Padding(0);
        shell.Controls.Add(body, 0, 1);
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        return shell;
    }

    // ---------------------------------------------------------------- shortcuts

    /// <summary>"1. 바로가기" - 프로그램 / 웹사이트 / 폴더·자료 세 묶음을 한 박스로 감싼다.</summary>
    private Control BuildShortcutGroupSection()
    {
        var box = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 10, 12, 12),
            Margin = new Padding(0),
        };
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AddShortcutSub(box, ShortcutKind.App, "프로그램", "+ 프로그램 추가", first: true);
        AddShortcutSub(box, ShortcutKind.Web, "웹사이트", "+ 웹사이트 추가", first: false);
        AddShortcutSub(box, ShortcutKind.Path, "폴더 / 자료", "+ 폴더/자료 추가", first: false);

        return SectionShell("1. 바로가기", null, null, box);
    }

    private void AddShortcutSub(TableLayoutPanel box, ShortcutKind kind, string title, string addText, bool first)
    {
        var list = _data.Shortcuts
            .Where(x => x.Kind == kind)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var head = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Margin = new Padding(0, first ? 0 : 12, 0, 4),
        };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        head.Controls.Add(new Label
        {
            Text = $"{title}  ({list.Count})",
            Font = Theme.Font9B,
            ForeColor = Theme.Text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 5, 0, 2),
        }, 0, 0);

        var add = Theme.ActionButton(addText);
        add.Anchor = AnchorStyles.Right;
        add.Margin = new Padding(0, 1, 0, 1);
        add.Click += (_, _) => AddShortcut(kind);
        head.Controls.Add(add, 1, 0);

        box.RowCount++;
        box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        box.Controls.Add(head, 0, box.RowCount - 1);

        Control body;
        if (list.Count == 0)
        {
            body = new Label
            {
                Text = "등록된 항목이 없습니다 · 위 + 추가 또는 이 영역으로 드래그앤드롭",
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 243, 247),
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Margin = new Padding(0),
            };
            AttachDropTarget(body, kind);
        }
        else
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Theme.Card,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            foreach (var item in list)
                flow.Controls.Add(CreateCard(item));
            AttachDropTarget(flow, kind);
            body = flow;
        }

        box.RowCount++;
        box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        box.Controls.Add(body, 0, box.RowCount - 1);
    }

    private Control CreateCard(ShortcutItem item)
    {
        bool missing = !PathLooksValid(item);

        var card = new Panel
        {
            Width = 100,
            Height = 76,
            Margin = new Padding(0, 3, 8, 3),
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand,
        };

        var picture = new PictureBox
        {
            Size = new Size(34, 34),
            Location = new Point((card.Width - 34) / 2, 8),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Theme.Card,
            Image = IconFor(item),
        };
        card.Controls.Add(picture);

        var name = new Label
        {
            Text = item.Name,
            ForeColor = missing ? Theme.Danger : Theme.Text,
            Font = Theme.Font8,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(3, 44),
            Size = new Size(card.Width - 6, 28),
        };
        card.Controls.Add(name);

        if (missing)
            card.BorderStyle = BorderStyle.FixedSingle;

        var tip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350 };
        var tipText = missing
            ? $"{item.Name}\n{item.Path}\n\n경로를 찾을 수 없습니다."
            : $"{item.Name}\n{item.Path}";
        tip.SetToolTip(card, tipText);
        tip.SetToolTip(picture, tipText);
        tip.SetToolTip(name, tipText);

        var menu = new ContextMenuStrip();
        menu.Items.Add("실행", null, (_, _) => Launch(item));
        menu.Items.Add("편집", null, (_, _) => EditShortcut(item));
        menu.Items.Add("삭제", null, (_, _) => DeleteShortcut(item));
        card.ContextMenuStrip = menu;

        var dragOrigin = Point.Empty;

        void OnDown(object? _, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                dragOrigin = e.Location;
        }

        void OnMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragOrigin == Point.Empty) return;
            var moved = new Size(Math.Abs(e.X - dragOrigin.X), Math.Abs(e.Y - dragOrigin.Y));
            if (moved.Width < SystemInformation.DragSize.Width && moved.Height < SystemInformation.DragSize.Height)
                return;
            dragOrigin = Point.Empty;
            card.DoDragDrop(item, DragDropEffects.Move);
        }

        void OnUp(object? _, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragOrigin != Point.Empty)
                Launch(item);
            dragOrigin = Point.Empty;
        }

        foreach (var control in new Control[] { card, picture, name })
        {
            control.MouseDown += OnDown;
            control.MouseMove += OnMove;
            control.MouseUp += OnUp;
        }

        return card;
    }

    private static bool PathLooksValid(ShortcutItem item)
        => item.Kind == ShortcutKind.Web
           || File.Exists(item.Path)
           || Directory.Exists(item.Path);

    /// <summary>아이콘 원본을 캐시하고 복제본을 넘긴다. IconData가 비어 있으면 채우고 저장 표시.</summary>
    private Image IconFor(ShortcutItem item)
    {
        if (!_iconMasters.TryGetValue(item.Id, out var master))
        {
            master = IconResolver.Resolve(item) ?? IconResolver.Placeholder(item.Kind);
            _iconMasters[item.Id] = master;

            if (string.IsNullOrWhiteSpace(item.IconData))
            {
                var encoded = IconResolver.ToBase64(master);
                if (!string.IsNullOrWhiteSpace(encoded))
                {
                    item.IconData = encoded;
                    _iconDataDirty = true;
                }
            }
        }

        try
        {
            return (Image)master.Clone();
        }
        catch
        {
            return IconResolver.Placeholder(item.Kind);
        }
    }

    private void ForgetIcon(string id)
    {
        if (_iconMasters.Remove(id, out var master))
            master.Dispose();
    }

    private int NextOrder(ShortcutKind kind)
        => _data.Shortcuts.Where(x => x.Kind == kind).Select(x => x.Order).DefaultIfEmpty(-1).Max() + 1;

    private void NormalizeOrder()
    {
        foreach (var kind in Enum.GetValues<ShortcutKind>())
        {
            var list = _data.Shortcuts.Where(x => x.Kind == kind).ToList();
            bool alreadyOrdered =
                list.Count > 0 &&
                list.Select(x => x.Order).Distinct().Count() == list.Count &&
                list.Any(x => x.Order != 0);
            if (alreadyOrdered) continue;
            for (int i = 0; i < list.Count; i++)
                list[i].Order = i;
        }
    }

    private void AddShortcut(ShortcutKind kind, string? droppedPath = null)
    {
        AppStore.Log($"[CLICK] add_shortcut kind={kind}");

        var seed = new ShortcutItem
        {
            Kind = kind,
            Name = droppedPath == null ? "" : Path.GetFileNameWithoutExtension(droppedPath),
            Path = droppedPath ?? "",
        };

        using var dialog = new ShortcutDialog(seed);
        if (ShowOwned(dialog) != DialogResult.OK) return;

        var item = dialog.Result;
        item.Order = NextOrder(item.Kind);
        _data.Shortcuts.Add(item);

        Persist();
        RebuildStack();
        Flash($"'{item.Name}' 바로가기를 추가했습니다.");
    }

    private void EditShortcut(ShortcutItem item)
    {
        AppStore.Log($"[CLICK] edit_shortcut {item.Name}");

        using var dialog = new ShortcutDialog(item);
        if (ShowOwned(dialog) != DialogResult.OK) return;

        int index = _data.Shortcuts.FindIndex(x => x.Id == item.Id);
        if (index < 0) return;

        var edited = dialog.Result;
        bool pathChanged = !string.Equals(edited.Path.Trim(), item.Path.Trim(), StringComparison.OrdinalIgnoreCase);
        bool iconChanged = !string.Equals(edited.Icon.Trim(), item.Icon.Trim(), StringComparison.OrdinalIgnoreCase);

        // 이름만 바꾼 경우: 최초 아이콘을 그대로 유지한다(RULE-06).
        // 경로나 지정 아이콘을 바꾼 경우에만 아이콘을 다시 계산한다.
        if (pathChanged || iconChanged)
        {
            edited.IconData = "";
            ForgetIcon(item.Id);
        }
        else
        {
            edited.Icon = item.Icon;
            edited.IconData = item.IconData;
        }

        edited.Order = edited.Kind == item.Kind ? item.Order : NextOrder(edited.Kind);
        _data.Shortcuts[index] = edited;

        Persist();
        RebuildStack();
        Flash($"'{edited.Name}' 바로가기를 수정했습니다.");
    }

    private void DeleteShortcut(ShortcutItem item)
    {
        if (MessageBox.Show($"'{item.Name}' 바로가기를 삭제할까요?", "바로가기 삭제",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _data.Shortcuts.RemoveAll(x => x.Id == item.Id);
        ForgetIcon(item.Id);
        Persist();
        RebuildStack();
        Flash("바로가기를 삭제했습니다.");
    }

    private void Launch(ShortcutItem item)
    {
        try
        {
            AppStore.Log($"[LAUNCH] {item.Kind} {item.Name}");

            var start = new ProcessStartInfo { UseShellExecute = true };
            if (item.Kind == ShortcutKind.App)
            {
                start.FileName = item.Path;
                if (!string.IsNullOrWhiteSpace(item.Arguments))
                    start.Arguments = item.Arguments;
            }
            else
            {
                start.FileName = item.Path;
            }

            Process.Start(start);
            Flash($"실행: {item.Name}");
        }
        catch (Exception ex)
        {
            AppStore.Log("[LAUNCH] failed", ex);
            MessageBox.Show(
                $"실행할 수 없습니다.\n\n{item.Path}\n\n{ex.Message}",
                "실행 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // ---------------------------------------------------------------- drag & drop

    private void AttachDropTarget(Control target, ShortcutKind kind)
    {
        target.AllowDrop = true;
        target.Tag = kind;
        target.DragEnter += OnAnyDragEnter;
        target.DragOver += OnAnyDragEnter;
        target.DragDrop += OnAnyDragDrop;
    }

    private void OnAnyDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(ShortcutItem)) == true)
            e.Effect = DragDropEffects.Move;
        else if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    private void OnAnyDragDrop(object? sender, DragEventArgs e)
    {
        var targetKind = (sender as Control)?.Tag as ShortcutKind?;

        if (e.Data?.GetData(typeof(ShortcutItem)) is ShortcutItem moved)
        {
            ReorderShortcut(moved, targetKind, sender as FlowLayoutPanel, e.X, e.Y);
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        foreach (var file in files)
        {
            var item = CreateShortcutFromDrop(file, targetKind);
            _data.Shortcuts.Add(item);
        }

        Persist();
        RebuildStack();
        Flash($"드래그앤드롭으로 {files.Length}개 항목을 등록했습니다.");
    }

    private ShortcutItem CreateShortcutFromDrop(string file, ShortcutKind? targetKind)
    {
        string path = file;
        string extension = Path.GetExtension(file);

        bool isUrl =
            path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".url", StringComparison.OrdinalIgnoreCase);

        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            path = ReadUrlFile(file) ?? file;

        ShortcutKind kind;
        if (isUrl)
            kind = ShortcutKind.Web;
        else if (Directory.Exists(file))
            kind = ShortcutKind.Path;
        else if (new[] { ".exe", ".lnk", ".bat", ".cmd", ".com", ".msi", ".py", ".pyw" }
                     .Contains(extension, StringComparer.OrdinalIgnoreCase))
            kind = ShortcutKind.App;
        else
            kind = ShortcutKind.Path;

        // 드롭 위치가 특정 영역이면 그 영역을 우선한다(단, URL은 항상 웹).
        if (targetKind is { } forced && !isUrl)
            kind = forced;

        return new ShortcutItem
        {
            Kind = kind,
            Name = Path.GetFileNameWithoutExtension(file),
            Path = path,
            Order = NextOrder(kind),
        };
    }

    private void ReorderShortcut(ShortcutItem item, ShortcutKind? targetKind, FlowLayoutPanel? flow, int screenX, int screenY)
    {
        var kind = targetKind ?? item.Kind;
        if (kind != item.Kind)
        {
            Flash("같은 영역 안에서만 순서를 바꿀 수 있습니다.");
            return;
        }

        var list = _data.Shortcuts.Where(x => x.Kind == kind).OrderBy(x => x.Order).ToList();
        int oldIndex = list.FindIndex(x => x.Id == item.Id);
        if (oldIndex < 0) return;

        int targetIndex = list.Count;
        if (flow != null)
        {
            var point = flow.PointToClient(new Point(screenX, screenY));
            targetIndex = 0;
            for (int i = 0; i < flow.Controls.Count; i++)
            {
                var bounds = flow.Controls[i].Bounds;
                if (point.Y > bounds.Bottom || (point.Y >= bounds.Top && point.X > bounds.Right))
                    targetIndex = i + 1;
            }
        }

        list.RemoveAt(oldIndex);
        if (oldIndex < targetIndex) targetIndex--;
        targetIndex = Math.Clamp(targetIndex, 0, list.Count);
        list.Insert(targetIndex, item);

        for (int i = 0; i < list.Count; i++)
            list[i].Order = i;

        Persist();
        RebuildStack();
        Flash("바로가기 순서를 저장했습니다.");
    }

    private static string? ReadUrlFile(string file)
    {
        try
        {
            return File.ReadLines(file)
                .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..]
                .Trim();
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- notes

    private Control BuildNotesSection()
    {
        var panel = new Panel
        {
            Height = 84,
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
        };

        _groupCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 130,
            Location = new Point(10, 8),
            Font = Theme.Font8,
        };
        _groupCombo.SelectedIndexChanged += (_, _) => RefreshPhrases();
        panel.Controls.Add(_groupCombo);

        panel.Controls.Add(new Label
        {
            Text = "클릭 = 즉시 복사   ·   더블클릭 / 우클릭 = 편집",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Location = new Point(150, 11),
        });

        _phraseFlow = new FlowLayoutPanel
        {
            Location = new Point(10, 36),
            Size = new Size(panel.ClientSize.Width - 20, panel.ClientSize.Height - 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Theme.Field,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(4),
        };
        panel.Controls.Add(_phraseFlow);

        return SectionShell("3. 업무 메모 / 자주 쓰는 문구", "+ 새 문구", (_, _) => NewNote(), panel);
    }

    private NoteItem? SelectedNote()
        => _selectedPhraseId == null ? null : _data.Notes.FirstOrDefault(x => x.Id == _selectedPhraseId);

    private void RefreshPhraseGroups()
    {
        if (_groupCombo == null) return;

        string current = _groupCombo.SelectedItem?.ToString() ?? "전체";
        var groups = _data.Notes
            .Select(x => string.IsNullOrWhiteSpace(x.Group) ? "일반" : x.Group)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCulture)
            .ToList();
        groups.Insert(0, "전체");

        _groupCombo.BeginUpdate();
        _groupCombo.Items.Clear();
        _groupCombo.Items.AddRange(groups.Cast<object>().ToArray());
        int index = groups.FindIndex(x => x.Equals(current, StringComparison.CurrentCultureIgnoreCase));
        _groupCombo.SelectedIndex = index < 0 ? 0 : index;
        _groupCombo.EndUpdate();

        RefreshPhrases();
    }

    private void RefreshPhrases()
    {
        if (_phraseFlow == null) return;

        var old = _phraseFlow.Controls.Cast<Control>().ToArray();
        _phraseFlow.SuspendLayout();
        _phraseFlow.Controls.Clear();

        string group = _groupCombo?.SelectedItem?.ToString() ?? "전체";
        var notes = _data.Notes
            .Where(x => group == "전체"
                        || string.Equals(string.IsNullOrWhiteSpace(x.Group) ? "일반" : x.Group, group,
                            StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (notes.Count == 0)
        {
            _phraseFlow.Controls.Add(new Label
            {
                Text = "이 그룹에 문구가 없습니다. 오른쪽 위 + 새 문구를 눌러 추가하세요.",
                AutoSize = true,
                ForeColor = Theme.Muted,
                Margin = new Padding(6, 8, 6, 6),
            });
        }
        else
        {
            foreach (var note in notes)
                _phraseFlow.Controls.Add(CreatePhraseTile(note));
        }

        _phraseFlow.ResumeLayout(true);
        foreach (var control in old)
            control.Dispose();
    }

    private Control CreatePhraseTile(NoteItem note)
    {
        bool selected = note.Id == _selectedPhraseId;

        var tile = new Label
        {
            Text = Theme.Ellipsis(string.IsNullOrWhiteSpace(note.Title) ? "(제목 없음)" : note.Title, 16),
            Tag = note.Id,
            Size = new Size(108, 24),
            Margin = new Padding(2),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Font8,
            Cursor = Cursors.Hand,
            AutoEllipsis = true,
            BackColor = selected ? Theme.Accent : Theme.Card,
            ForeColor = selected ? Color.White : Theme.Text,
        };

        var body = string.IsNullOrWhiteSpace(note.Text) ? "(본문 없음 - 제목이 복사됩니다)" : note.Text;
        var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        tip.SetToolTip(tile, $"{note.Title}\n\n{Theme.Ellipsis(body, 500)}\n\n[그룹] {(string.IsNullOrWhiteSpace(note.Group) ? "일반" : note.Group)}");

        // 좌클릭은 항상 즉시 복사. 더블클릭은 편집. (원본은 Button.DoubleClick이 발생하지
        // 않아 편집이 동작하지 않았음 - Label은 MouseDoubleClick을 정상적으로 발생시킨다.)
        tile.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                CopyPhrase(note);
        };
        tile.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                EditNote(note);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("복사", null, (_, _) => CopyPhrase(note));
        menu.Items.Add("수정", null, (_, _) => EditNote(note));
        menu.Items.Add("삭제", null, (_, _) => DeleteNote(note));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("위로 이동", null, (_, _) => MoveNote(note, -1));
        menu.Items.Add("아래로 이동", null, (_, _) => MoveNote(note, 1));
        tile.ContextMenuStrip = menu;

        return tile;
    }

    private void RestylePhraseTiles()
    {
        if (_phraseFlow == null) return;
        foreach (Control control in _phraseFlow.Controls)
        {
            if (control is not Label tile || tile.Tag is not string id) continue;
            bool selected = id == _selectedPhraseId;
            tile.BackColor = selected ? Theme.Accent : Theme.Card;
            tile.ForeColor = selected ? Color.White : Theme.Text;
        }
    }

    private void CopyPhrase(NoteItem note)
    {
        try
        {
            AppStore.Log($"[PHRASE CLICK] {note.Title}");

            string value = string.IsNullOrWhiteSpace(note.Text) ? note.Title : note.Text;
            if (!string.IsNullOrEmpty(value))
            {
                SetClipboardText(value);
                AppStore.Log($"[CLIPBOARD] {Theme.Ellipsis(value, 60)}");
            }

            _selectedPhraseId = note.Id;
            RestylePhraseTiles();
            Flash($"복사됨: {note.Title}");
        }
        catch (Exception ex)
        {
            AppStore.Log("[CLIPBOARD] failed", ex);
            Flash($"복사 실패: {ex.Message}");
        }
    }

    /// <summary>클립보드는 다른 프로세스가 잠깐 잠글 수 있으므로 몇 번 재시도한다.</summary>
    private static void SetClipboardText(string text)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch
            {
                Thread.Sleep(40);
            }
        }
        Clipboard.SetDataObject(text, copy: true, retryTimes: 4, retryDelay: 60);
    }

    private void NewNote()
    {
        AppStore.Log("[CLICK] add_phrase");

        using var dialog = new NoteDialog(_data.Notes);
        if (ShowOwned(dialog) != DialogResult.OK) return;

        var note = new NoteItem
        {
            Title = dialog.NoteTitle,
            Text = dialog.NoteText,
            Group = dialog.NoteGroup,
        };
        _data.Notes.Add(note);
        _selectedPhraseId = note.Id;

        Persist();
        RebuildStack();
        Flash("새 문구를 저장했습니다.");
    }

    private void EditNote(NoteItem note)
    {
        using var dialog = new NoteDialog(_data.Notes, note);
        if (ShowOwned(dialog) != DialogResult.OK) return;

        note.Title = dialog.NoteTitle;
        note.Text = dialog.NoteText;
        note.Group = dialog.NoteGroup;
        _selectedPhraseId = note.Id;

        Persist();
        RebuildStack();
        Flash("문구를 수정했습니다.");
    }

    private void DeleteNote(NoteItem note)
    {
        if (MessageBox.Show($"'{note.Title}' 문구를 삭제할까요?", "문구 삭제",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _data.Notes.RemoveAll(x => x.Id == note.Id);
        if (_selectedPhraseId == note.Id)
            _selectedPhraseId = null;

        Persist();
        RebuildStack();
        Flash("문구를 삭제했습니다.");
    }

    private void MoveNote(NoteItem note, int direction)
    {
        int index = _data.Notes.FindIndex(x => x.Id == note.Id);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= _data.Notes.Count) return;

        (_data.Notes[index], _data.Notes[target]) = (_data.Notes[target], _data.Notes[index]);
        Persist();
        RebuildStack();
    }

    // ---------------------------------------------------------------- dialogs / geometry / status

    private DialogResult ShowOwned(Form dialog)
    {
        dialog.Font = Theme.Font9;
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.ShowInTaskbar = false;

        var work = Screen.FromControl(this).WorkingArea;
        int x = Bounds.Left + (Bounds.Width - dialog.Width) / 2;
        int y = Bounds.Top + (Bounds.Height - dialog.Height) / 2;
        dialog.Location = new Point(
            Math.Max(work.Left, Math.Min(x, work.Right - dialog.Width)),
            Math.Max(work.Top, Math.Min(y, work.Bottom - dialog.Height)));

        // TopMost 메인창 위에 뜨도록 다이얼로그도 잠깐 TopMost로. 원래 설정은 건드리지 않는다.
        dialog.TopMost = TopMost;
        dialog.Shown += (_, _) =>
        {
            dialog.BringToFront();
            dialog.Activate();
            Native.SetForegroundWindow(dialog.Handle);
        };

        return dialog.ShowDialog(this);
    }

    private void ApplyGeometry(string value)
    {
        try
        {
            var parts = value.Split('+');
            var size = parts[0].Split('x');
            if (size.Length == 2)
                Size = new Size(int.Parse(size[0]), int.Parse(size[1]));
            if (parts.Length >= 3)
                Location = new Point(int.Parse(parts[1]), int.Parse(parts[2]));
        }
        catch
        {
            Size = new Size(1000, 740);
        }

        // 마지막 위치의 모니터가 사라졌다면 현재 모니터 중앙으로.
        if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(Bounds)))
        {
            var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
            Location = new Point(
                work.Left + (work.Width - Width) / 2,
                work.Top + (work.Height - Height) / 2);
        }
    }

    private void SaveGeometry()
    {
        if (_applyingGeometry || !IsHandleCreated || WindowState != FormWindowState.Normal)
            return;

        string value = $"{Width}x{Height}+{Left}+{Top}";
        if (_data.Settings.MiniMode)
            _data.Settings.MiniGeometry = value;
        else
            _data.Settings.Geometry = value;

        Persist();
    }

    private void ApplyMode()
    {
        _miniMode = _data.Settings.MiniMode;
        _applyingGeometry = true;

        if (_miniMode)
        {
            MinimumSize = new Size(260, 200);
            _miniButton.Text = "전체 모드";
            _topCheck.Visible = true;
            _status.Visible = false;
            ApplyGeometry(_data.Settings.MiniGeometry);
        }
        else
        {
            MinimumSize = new Size(480, 360);
            _miniButton.Text = "미니모드";
            _topCheck.Visible = true;
            _status.Visible = true;
            ApplyGeometry(_data.Settings.Geometry);
        }

        _applyingGeometry = false;
        RebuildStack();
    }

    private void ToggleMini()
    {
        SaveGeometry();
        _data.Settings.MiniMode = !_data.Settings.MiniMode;
        Persist();
        ApplyMode();
    }

    private void Flash(string text)
    {
        if (_status == null) return;
        _status.Text = text;

        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        _statusTimer = new System.Windows.Forms.Timer { Interval = 3500 };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer?.Stop();
            if (_status != null)
                _status.Text = "준비됨 · Explorer에서 파일을 이 창으로 끌어오면 바로 등록됩니다 · F5 새로고침";
        };
        _statusTimer.Start();
    }

    private void Persist() => AppStore.Save(_data);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        CancelDesktopScan();
        StopWatching();
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
        SaveGeometry();
        Persist();
        base.OnFormClosing(e);
    }
}
```

### G. `MainForm.DesktopSearch.cs`

```csharp
using System.Diagnostics;

namespace MyWorkQuickLauncher;

public sealed partial class MainForm
{
    private static readonly (string Name, string[] Extensions)[] FilterDefinitions =
    {
        ("JPG / 이미지", new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".heic" }),
        ("Excel", new[] { ".xlsx", ".xls", ".xlsm", ".xlsb", ".csv" }),
        ("PowerPoint", new[] { ".ppt", ".pptx", ".pptm", ".pps", ".ppsx" }),
        ("PDF", new[] { ".pdf" }),
        ("MP3 / 오디오", new[] { ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".ogg" }),
        ("영상", new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".m4v", ".mpeg", ".mpg", ".webm" }),
        ("문서", new[] { ".doc", ".docx", ".hwp", ".hwpx", ".txt", ".rtf" }),
        ("전체 파일", Array.Empty<string>()),
    };

    private static readonly Dictionary<string, HashSet<string>> FilterMap =
        FilterDefinitions.ToDictionary(
            f => f.Name,
            f => new HashSet<string>(f.Extensions, StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal);

    private const string AllFilesFilter = "전체 파일";
    private const int MaxRenderedResults = 30;
    private const long MaxThumbnailBytes = 40L * 1024 * 1024;

    private FlowLayoutPanel _filterFlow = null!;
    private TextBox _searchBox = null!;
    private Label _resultInfo = null!;
    private Label _scanInfo = null!;
    private FlowLayoutPanel _resultsFlow = null!;

    private string _filter = "JPG / 이미지";
    private List<DesktopFileItem> _files = new();
    private CancellationTokenSource? _scanCts;
    private int _scanGeneration;
    private bool _scanning;

    private System.Windows.Forms.Timer? _searchDebounce;
    private System.Windows.Forms.Timer? _fileClickTimer;
    private DesktopFileItem? _pendingFile;

    private readonly object _thumbLock = new();
    private readonly Dictionary<string, Image?> _thumbnails = new(StringComparer.OrdinalIgnoreCase);

    private Control BuildDesktopSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 372,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
        };

        _filterFlow = new FlowLayoutPanel
        {
            Location = new Point(10, 10),
            Size = new Size(panel.ClientSize.Width - 20, 36),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            WrapContents = true,
            AutoScroll = false,
            BackColor = Theme.Card,
        };
        foreach (var (name, _) in FilterDefinitions)
        {
            string filterName = name;
            var button = new Button
            {
                Text = filterName,
                Tag = filterName,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 30,
                Margin = new Padding(0, 0, 5, 5),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.Font8,
                Cursor = Cursors.Hand,
                Padding = new Padding(9, 3, 9, 3),
            };
            button.FlatAppearance.BorderColor = Theme.Border;
            button.Click += (_, _) => SelectFilter(filterName);
            _filterFlow.Controls.Add(button);
        }
        panel.Controls.Add(_filterFlow);

        var searchLabel = new Label
        {
            Text = "파일명",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font9,
            Location = new Point(12, 88),
        };
        panel.Controls.Add(searchLabel);

        _searchBox = new TextBox
        {
            Location = new Point(66, 84),
            Width = 280,
            Font = Theme.Font9,
            PlaceholderText = "파일명으로 좁히기",
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchDebounce?.Stop();
            _searchDebounce?.Dispose();
            _searchDebounce = new System.Windows.Forms.Timer { Interval = 220 };
            _searchDebounce.Tick += (_, _) =>
            {
                _searchDebounce?.Stop();
                ApplyFilter();
            };
            _searchDebounce.Start();
        };
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                _searchDebounce?.Stop();
                ApplyFilter();
                e.SuppressKeyPress = true;
            }
        };
        panel.Controls.Add(_searchBox);

        _resultInfo = new Label
        {
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Location = new Point(360, 88),
        };
        panel.Controls.Add(_resultInfo);

        _scanInfo = new Label
        {
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
        };
        panel.Controls.Add(_scanInfo);
        panel.Resize += (_, _) =>
            _scanInfo.Location = new Point(panel.ClientSize.Width - _scanInfo.Width - 12, 88);

        _resultsFlow = new FlowLayoutPanel
        {
            Location = new Point(10, 114),
            Size = new Size(panel.ClientSize.Width - 20, panel.ClientSize.Height - 124),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Theme.Field,
            BorderStyle = BorderStyle.FixedSingle,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(7),
        };
        panel.Controls.Add(_resultsFlow);

        return SectionShell(
            "4. 바탕화면 파일검색",
            "새로고침",
            (_, _) =>
            {
                AppStore.Log("[CLICK] refresh_files");
                RunDesktopScan();
                Flash("바탕화면 파일을 다시 검색합니다...");
            },
            panel);
    }

    private void RefreshFilterButtons()
    {
        if (_filterFlow == null) return;
        foreach (Control control in _filterFlow.Controls)
        {
            if (control is not Button button || button.Tag is not string filter) continue;
            bool selected = string.Equals(filter, _filter, StringComparison.Ordinal);
            button.BackColor = selected ? Theme.Accent : Theme.Card;
            button.ForeColor = selected ? Color.White : Theme.Text;
            button.FlatAppearance.BorderColor = selected ? Theme.Accent : Theme.Border;
        }
    }

    private void SelectFilter(string filter)
    {
        if (!FilterMap.ContainsKey(filter)) return;

        _filter = filter;
        AppStore.Log($"[CLICK] file_filter={filter}");

        // 탭 색과 결과 렌더링은 하나의 동작이다. 스캔 중이어도 캐시된 결과로 즉시 다시 그린다.
        RefreshFilterButtons();
        ApplyFilter();
    }

    private static List<string> DesktopRoots()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(profile, "Desktop"),
            Path.Combine(profile, "OneDrive", "Desktop"),
        };

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void CancelDesktopScan()
    {
        try
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        catch
        {
            // ignore
        }
        _scanCts = null;
        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();
        _fileClickTimer?.Stop();
        _fileClickTimer?.Dispose();
    }

    private async void RunDesktopScan()
    {
        if (_resultsFlow == null) return;

        int generation = Interlocked.Increment(ref _scanGeneration);
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        _scanning = true;
        _files = new List<DesktopFileItem>();
        RenderResults(Array.Empty<DesktopFileItem>());
        _scanInfo.Text = "검색 중...";
        _resultInfo.Text = "";

        try
        {
            var roots = DesktopRoots();
            if (roots.Count == 0)
            {
                _scanning = false;
                _scanInfo.Text = "바탕화면 폴더를 찾을 수 없음";
                return;
            }

            AppStore.Log($"[SEARCH] start id={generation} roots={string.Join(";", roots)} filter={_filter}");
            var results = await Task.Run(() => ScanFiles(roots, token), token);
            await Task.Yield();

            if (IsDisposed || token.IsCancellationRequested || generation != _scanGeneration)
            {
                AppStore.Log($"[SEARCH] stale id={generation} ignored");
                return;
            }

            _scanning = false;
            _files = results;
            _scanInfo.Text = $"검색 완료 · 전체 {results.Count:N0}개";
            ApplyFilter();
            AppStore.Log($"[SEARCH] complete id={generation} found={results.Count}");
        }
        catch (OperationCanceledException)
        {
            // 새 검색이 시작되어 취소됨 - 무시
        }
        catch (Exception ex)
        {
            AppStore.Log($"[SEARCH] failed id={generation}", ex);
            if (generation == _scanGeneration)
            {
                _scanning = false;
                _scanInfo.Text = "검색 실패";
                _resultInfo.Text = ex.Message;
                RenderResults(Array.Empty<DesktopFileItem>());
            }
        }
    }

    private static List<DesktopFileItem> ScanFiles(IReadOnlyList<string> roots, CancellationToken token)
    {
        // 바탕화면에 "노출된" 파일만: 하위 폴더로 내려가지 않고 각 루트의 최상위 파일만 수집한다.
        var found = new List<DesktopFileItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (token.IsCancellationRequested || !Directory.Exists(root)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(root))
                {
                    if (token.IsCancellationRequested) break;
                    try
                    {
                        var info = new FileInfo(file);
                        if (!seen.Add(info.Name)) continue; // Desktop과 OneDrive\Desktop 중복 제거
                        found.Add(new DesktopFileItem
                        {
                            FullPath = file,
                            Name = info.Name,
                            Folder = "바탕화면",
                            Ext = info.Extension,
                            Modified = info.LastWriteTime,
                        });
                    }
                    catch
                    {
                        // 개별 파일 접근 오류는 건너뛴다.
                    }
                }
            }
            catch
            {
                // 폴더 열거 실패는 전체 검색을 멈추지 않는다.
            }
        }

        return found;
    }

    private void ApplyFilter()
    {
        if (_resultsFlow == null) return;

        string query = _searchBox?.Text.Trim() ?? "";
        var extensions = FilterMap.TryGetValue(_filter, out var set) ? set : new HashSet<string>();

        var filtered = _files
            .Where(file =>
                (_filter == AllFilesFilter || extensions.Contains(file.Ext)) &&
                (query.Length == 0 || file.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(file => file.Modified)
            .ToList();

        _resultInfo.Text = _scanning
            ? $"{_filter} — 검색 중..."
            : $"{_filter} — {filtered.Count:N0}개" + (filtered.Count > MaxRenderedResults ? $" (최근 {MaxRenderedResults}개 표시)" : "");

        RenderResults(filtered);

        if (!_scanning)
            Flash($"{_filter}: {filtered.Count:N0}개");
    }

    private void RenderResults(IReadOnlyList<DesktopFileItem> files)
    {
        if (_resultsFlow == null) return;

        var old = _resultsFlow.Controls.Cast<Control>().ToArray();
        _resultsFlow.SuspendLayout();
        _resultsFlow.Controls.Clear();

        if (files.Count == 0)
        {
            _resultsFlow.Controls.Add(new Label
            {
                Text = _scanning ? "검색 중..." : "조건에 맞는 파일이 없습니다.",
                AutoSize = true,
                ForeColor = Theme.Muted,
                Margin = new Padding(6),
            });
        }
        else
        {
            foreach (var file in files.Take(MaxRenderedResults))
                _resultsFlow.Controls.Add(CreateResultRow(file));

            if (files.Count > MaxRenderedResults)
            {
                _resultsFlow.Controls.Add(new Label
                {
                    Text = $"전체 {files.Count:N0}개 중 최근 {MaxRenderedResults}개만 표시 · 파일명을 입력해 좁혀보세요",
                    AutoSize = true,
                    ForeColor = Theme.Muted,
                    Margin = new Padding(6, 8, 6, 6),
                });
            }
        }

        _resultsFlow.ResumeLayout(true);
        foreach (var control in old)
            control.Dispose();
    }

    private Control CreateResultRow(DesktopFileItem file)
    {
        var row = new Panel
        {
            Width = 172,
            Height = 66,
            Margin = new Padding(3),
            BackColor = Theme.Field,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand,
            Tag = file,
        };

        var picture = new PictureBox
        {
            Size = new Size(46, 46),
            Location = new Point(5, 8),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
        };
        row.Controls.Add(picture);
        LoadThumbnailAsync(file, picture);

        var name = new Label
        {
            Text = file.Name,
            AutoEllipsis = true,
            ForeColor = Theme.Text,
            Font = Theme.Font8,
            Location = new Point(56, 8),
            Size = new Size(108, 32),
            TextAlign = ContentAlignment.TopLeft,
        };
        row.Controls.Add(name);

        var folder = new Label
        {
            Text = file.Modified.ToString("yyyy-MM-dd HH:mm"),
            AutoEllipsis = true,
            ForeColor = Theme.Muted,
            Font = new Font("Malgun Gothic", 7.5F),
            Location = new Point(56, 44),
            Size = new Size(108, 16),
        };
        row.Controls.Add(folder);

        var tip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300 };
        string tipText = $"{file.Name}\n{file.FullPath}\n수정: {file.Modified:yyyy-MM-dd HH:mm}";
        tip.SetToolTip(row, tipText);
        tip.SetToolTip(picture, tipText);
        tip.SetToolTip(name, tipText);
        tip.SetToolTip(folder, tipText);

        var menu = new ContextMenuStrip();
        menu.Items.Add("파일 위치 열기", null, (_, _) => RevealFile(file));
        menu.Items.Add("파일 실행", null, (_, _) => OpenFile(file));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("전체 경로 복사", null, (_, _) => TryCopy(file.FullPath));
        menu.Items.Add("파일명 복사", null, (_, _) => TryCopy(file.Name));
        row.ContextMenuStrip = menu;

        void OnClick(object? _, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                QueueReveal(file);
        }

        void OnDoubleClick(object? _, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                OpenAfterDoubleClick(file);
        }

        foreach (var control in new Control[] { row, picture, name, folder })
        {
            control.MouseClick += OnClick;
            control.MouseDoubleClick += OnDoubleClick;
            control.ContextMenuStrip = menu;
        }

        return row;
    }

    /// <summary>썸네일/아이콘을 백그라운드에서 만들고, 살아있는 PictureBox에만 반영한다.</summary>
    private void LoadThumbnailAsync(DesktopFileItem file, PictureBox target)
    {
        _ = Task.Run(() =>
        {
            var image = BuildThumbnail(file);
            if (image == null) return;

            try
            {
                if (target.IsDisposed) return;
                target.BeginInvoke(new Action(() =>
                {
                    if (target.IsDisposed) return;
                    try
                    {
                        target.Image = (Image)image.Clone();
                    }
                    catch
                    {
                        // ignore
                    }
                }));
            }
            catch
            {
                // 컨트롤이 이미 파괴됨
            }
        });
    }

    private Image? BuildThumbnail(DesktopFileItem file)
    {
        lock (_thumbLock)
        {
            if (_thumbnails.TryGetValue(file.FullPath, out var cached))
                return cached;
        }

        Image? image = null;
        try
        {
            if (IsImageExtension(file.Ext))
            {
                var info = new FileInfo(file.FullPath);
                if (info.Exists && info.Length <= MaxThumbnailBytes)
                {
                    using var stream = new MemoryStream(File.ReadAllBytes(file.FullPath));
                    using var source = Image.FromStream(stream);
                    image = new Bitmap(source, new Size(44, 44));
                }
            }

            image ??= Native.ShellIcon(file.FullPath, large: true)
                      ?? Icon.ExtractAssociatedIcon(file.FullPath)?.ToBitmap();
        }
        catch
        {
            image = null;
        }

        lock (_thumbLock)
        {
            _thumbnails[file.FullPath] = image;
        }
        return image;
    }

    private static bool IsImageExtension(string extension)
        => FilterMap["JPG / 이미지"].Contains(extension);

    private void QueueReveal(DesktopFileItem file)
    {
        _fileClickTimer?.Stop();
        _fileClickTimer?.Dispose();
        _pendingFile = file;

        var timer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(250, SystemInformation.DoubleClickTime),
        };
        _fileClickTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            if (ReferenceEquals(_fileClickTimer, timer))
                _fileClickTimer = null;
            if (ReferenceEquals(_pendingFile, file))
            {
                _pendingFile = null;
                RevealFile(file);
            }
        };
        timer.Start();
    }

    private void OpenAfterDoubleClick(DesktopFileItem file)
    {
        _fileClickTimer?.Stop();
        _fileClickTimer?.Dispose();
        _fileClickTimer = null;
        _pendingFile = null;
        OpenFile(file);
    }

    private void RevealFile(DesktopFileItem file)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{file.FullPath}\"",
                UseShellExecute = true,
            });
            Flash($"위치 열기: {file.Name}");
        }
        catch (Exception ex)
        {
            AppStore.Log("[REVEAL] failed", ex);
        }
    }

    private void OpenFile(DesktopFileItem file)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
            Flash($"실행: {file.Name}");
        }
        catch (Exception ex)
        {
            AppStore.Log("[OPEN] failed", ex);
            MessageBox.Show($"파일을 열 수 없습니다.\n\n{file.FullPath}\n\n{ex.Message}",
                "실행 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TryCopy(string text)
    {
        try
        {
            SetClipboardText(text);
            Flash("복사됨");
        }
        catch (Exception ex)
        {
            AppStore.Log("[CLIPBOARD] failed", ex);
        }
    }
}
```

### H. `MainForm.SheetAnalysis.cs`

```csharp
namespace MyWorkQuickLauncher;

public sealed partial class MainForm
{
    private TextBox _leftSheetBox = null!;
    private TextBox _rightSheetBox = null!;
    private Button _analyzeButton = null!;
    private Label _sheetStatus = null!;

    private Control BuildSheetAnalysisSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 10, 12, 10),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = "A시트  ·  공임비교분석 / 사용자·주체·도장 조회 (URL)",
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 6),
        }, 0, 0);

        _leftSheetBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = Theme.Font9,
            Text = _data.Settings.LeftSheetUrl,
            PlaceholderText = "http://<사내서버주소>:8180/ant/gongim/popup_detail1.php?eseq=...  (한 개만)",
            Margin = new Padding(0, 3, 0, 3),
        };
        panel.Controls.Add(_leftSheetBox, 1, 0);

        panel.Controls.Add(new Label
        {
            Text = "B시트  ·  업로드 로그 (URL)",
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 6),
        }, 0, 1);

        _rightSheetBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = Theme.Font9,
            Text = _data.Settings.RightSheetUrl,
            PlaceholderText = "http://<사내서버주소>:8180/ant/api/popup_detail.php?eseq=...",
            Margin = new Padding(0, 3, 0, 3),
        };
        panel.Controls.Add(_rightSheetBox, 1, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
        };

        _analyzeButton = Theme.FlatButton("작업항목 비교 분석", Theme.Accent, Color.White);
        _analyzeButton.Margin = new Padding(0, 0, 12, 0);
        _analyzeButton.Click += (_, _) => RunSheetAnalysis();
        actions.Controls.Add(_analyzeButton);

        _sheetStatus = new Label
        {
            Text = "두 시트의 URL을 넣고 비교하면, B시트에 없는 작업항목을 노란색으로 보여줍니다.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 0),
        };
        actions.Controls.Add(_sheetStatus);

        panel.Controls.Add(actions, 1, 2);

        return SectionShell("2. 시트 분석 TOOL", null, null, panel);
    }

    private async void RunSheetAnalysis()
    {
        string leftUrl = _leftSheetBox.Text.Trim();
        string rightUrl = _rightSheetBox.Text.Trim();

        if (!SheetSource.LooksLikeUrl(leftUrl) || !SheetSource.LooksLikeUrl(rightUrl))
        {
            MessageBox.Show(
                "A시트와 B시트의 URL을 각각 한 개씩 http(s):// 형식으로 입력하세요.",
                "URL 확인",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _analyzeButton.Enabled = false;
        _sheetStatus.ForeColor = Theme.Muted;
        _sheetStatus.Text = "시트를 불러오는 중...";
        AppStore.Log("[SHEET] analyze start");

        try
        {
            var leftTask = SheetSource.FetchAsync(leftUrl);
            var rightTask = SheetSource.FetchAsync(rightUrl);
            await Task.WhenAll(leftTask, rightTask);

            var left = leftTask.Result;
            var right = rightTask.Result;

            if (left.Count == 0)
            {
                _sheetStatus.ForeColor = Theme.Danger;
                _sheetStatus.Text = "A시트에서 작업항목을 찾지 못했습니다. URL을 확인하세요.";
                return;
            }

            var rightKeys = right
                .Select(r => SheetSource.ItemKey(r.Item))
                .ToHashSet(StringComparer.Ordinal);

            var missingKeys = left
                .Select(r => SheetSource.ItemKey(r.Item))
                .Where(key => key.Length > 0 && !rightKeys.Contains(key))
                .ToHashSet(StringComparer.Ordinal);

            int missingRows = left.Count(r => missingKeys.Contains(SheetSource.ItemKey(r.Item)));

            _data.Settings.LeftSheetUrl = leftUrl;
            _data.Settings.RightSheetUrl = rightUrl;
            Persist();

            _sheetStatus.ForeColor = missingKeys.Count > 0 ? Theme.Accent : Theme.Muted;
            _sheetStatus.Text =
                $"A시트 {left.Count}행 · B시트 {right.Count}행 · B시트에 없는 작업항목 {missingKeys.Count}건({missingRows}행)";
            AppStore.Log($"[SHEET] analyze done left={left.Count} right={right.Count} missing={missingKeys.Count}");

            using var dialog = new SheetAnalysisDialog(left, missingKeys, right.Count, leftUrl, SetClipboardText);
            ShowOwned(dialog);
        }
        catch (Exception ex)
        {
            AppStore.Log("[SHEET] analyze failed", ex);
            _sheetStatus.ForeColor = Theme.Danger;
            _sheetStatus.Text = "분석 실패: " + ex.Message;
            MessageBox.Show(
                $"시트를 불러오지 못했습니다.\n\n{ex.Message}",
                "시트 분석 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _analyzeButton.Enabled = true;
        }
    }
}
```

### I. `SheetAnalysis.cs`

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace MyWorkQuickLauncher;

/// <summary>시트(견적) 한 행. 2번째 열(<see cref="Item"/>)이 작업항목 비교 기준이다.</summary>
public sealed record SheetRow(
    string No,
    string Item,
    string Work,
    string TimeClaim,
    string PartAmount,
    string Labor);

/// <summary>
/// gomail 팝업 URL을 받아 견적 테이블을 파싱한다.
/// popup_detail1 / popup_detail2 / api/popup_detail 세 형식 모두 &lt;tr class='tableRow'&gt; 행에
/// [no, 작업항목, 작업, 시간/청구, 부품금액, 공임] 순으로 셀이 들어 있다.
/// </summary>
public static class SheetSource
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.Add("User-Agent", "MyWorkQuickLauncher/2.0");
        return client;
    }

    public static bool LooksLikeUrl(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static async Task<List<SheetRow>> FetchAsync(string url, CancellationToken token = default)
    {
        byte[] bytes = await Http.GetByteArrayAsync(url.Trim(), token);
        // gomail 팝업은 UTF-8로 서빙된다.
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    public static List<SheetRow> Parse(string html)
    {
        var rows = new List<SheetRow>();
        if (string.IsNullOrEmpty(html)) return rows;

        int tableStart = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        int tableEnd = html.IndexOf("</table>", StringComparison.OrdinalIgnoreCase);
        string scope = tableStart >= 0 && tableEnd > tableStart
            ? html.Substring(tableStart, tableEnd - tableStart)
            : html;

        foreach (Match rowMatch in Regex.Matches(
                     scope,
                     @"<tr\b[^>]*>(.*?)(?=<tr\b|</table>|$)",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            string rowHtml = rowMatch.Value;
            if (!rowHtml.Contains("tableRow", StringComparison.OrdinalIgnoreCase))
                continue;

            var cells = Regex.Matches(rowHtml, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Select(m => CleanCell(m.Groups[1].Value))
                .ToList();

            if (cells.Count < 2) continue;

            string item = cells[1];
            if (string.IsNullOrWhiteSpace(item) || item is "작업항목" or "작업내용")
                continue;

            rows.Add(new SheetRow(
                Cell(cells, 0),
                item,
                Cell(cells, 2),
                Cell(cells, 3),
                Cell(cells, 4),
                Cell(cells, 5)));
        }

        return rows;
    }

    private static string Cell(List<string> cells, int index)
        => index >= 0 && index < cells.Count ? cells[index] : "";

    private static string CleanCell(string raw)
    {
        string noTags = Regex.Replace(raw, "<[^>]+>", " ");
        string decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    /// <summary>작업항목 비교 키: 공백을 모두 제거해 표기 차이를 흡수한다.</summary>
    public static string ItemKey(string item)
        => Regex.Replace(item ?? "", @"\s+", "").Trim();
}
```

### J. `SheetAnalysisDialog.cs`

```csharp
namespace MyWorkQuickLauncher;

/// <summary>
/// 시트 분석 결과 팝업. A시트의 전체 견적을 보여주고, B시트에 없는 작업항목 행을
/// 노란색으로 음영 처리한다. 노란색 행을 클릭하면 업무 메모와 동일하게 클립보드로 복사된다.
/// </summary>
public sealed class SheetAnalysisDialog : Form
{
    private static readonly Color MissingBack = Color.FromArgb(255, 244, 143);
    private static readonly Color MissingSelect = Color.FromArgb(255, 227, 82);

    private readonly Action<string> _copy;
    private readonly DataGridView _grid = new();
    private readonly Label _feedback = new();
    private readonly CheckBox _onlyMissing = new();
    private readonly IReadOnlyList<SheetRow> _rows;
    private readonly HashSet<string> _missingKeys;

    public SheetAnalysisDialog(
        IReadOnlyList<SheetRow> leftRows,
        HashSet<string> missingKeys,
        int rightRowCount,
        string leftUrl,
        Action<string> copy)
    {
        _rows = leftRows;
        _missingKeys = missingKeys;
        _copy = copy;

        var missingItems = leftRows
            .Where(r => missingKeys.Contains(SheetSource.ItemKey(r.Item)))
            .Select(r => r.Item)
            .Distinct(StringComparer.CurrentCulture)
            .ToList();

        Text = "시트 분석 결과";
        ClientSize = new Size(900, 640);
        MinimumSize = new Size(620, 420);
        Font = Theme.Font9;
        BackColor = Theme.Bg;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = true;
        MinimizeBox = false;

        // ---- 상단 요약 ----
        var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Card, Padding = new Padding(14, 10, 14, 10) };

        header.Controls.Add(new Label
        {
            Text = $"A시트 {leftRows.Count}행   ·   B시트 {rightRowCount}행   ·   "
                   + $"B시트에 없는 작업항목 {missingItems.Count}건",
            AutoSize = true,
            Font = Theme.Font11B,
            ForeColor = Theme.Text,
            Location = new Point(2, 4),
        });

        header.Controls.Add(new Label
        {
            Text = "노란색 = B시트(업로드 로그)에 없는 작업항목   ·   노란색 행을 클릭하면 즉시 복사됩니다",
            AutoSize = true,
            Font = Theme.Font8,
            ForeColor = Theme.Muted,
            Location = new Point(2, 30),
        });

        _onlyMissing.Text = "누락 항목만 보기";
        _onlyMissing.AutoSize = true;
        _onlyMissing.Location = new Point(2, 52);
        _onlyMissing.FlatStyle = FlatStyle.Flat;
        _onlyMissing.CheckedChanged += (_, _) => ApplyRowFilter();
        header.Controls.Add(_onlyMissing);

        var copyAll = Theme.ActionButton("누락 항목 전체 복사");
        copyAll.Location = new Point(170, 48);
        copyAll.Enabled = missingItems.Count > 0;
        copyAll.Click += (_, _) =>
        {
            _copy(string.Join(Environment.NewLine, missingItems));
            ShowFeedback($"누락 항목 {missingItems.Count}건을 복사했습니다.");
        };
        header.Controls.Add(copyAll);

        Controls.Add(header);

        // ---- 하단 피드백 ----
        _feedback.Dock = DockStyle.Bottom;
        _feedback.Height = 26;
        _feedback.BackColor = Color.FromArgb(226, 232, 240);
        _feedback.ForeColor = Theme.Muted;
        _feedback.Font = Theme.Font8;
        _feedback.Padding = new Padding(14, 5, 8, 0);
        _feedback.Text = missingItems.Count > 0
            ? "노란색 작업항목을 클릭해 복사하세요."
            : "B시트에 없는 작업항목이 없습니다.";
        Controls.Add(_feedback);

        // ---- 표 ----
        BuildGrid();
        Controls.Add(_grid);
        _grid.BringToFront();

        LoadRows();

        var close = new Button
        {
            Text = "닫기",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Card,
            Size = new Size(72, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = Theme.Font9,
        };
        close.Location = new Point(header.ClientSize.Width - 84, 48);
        header.Controls.Add(close);
        header.Resize += (_, _) => close.Location = new Point(header.ClientSize.Width - 84, 48);
        CancelButton = close;
        AcceptButton = close;
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 32;
        _grid.RowTemplate.Height = 26;
        _grid.GridColor = Theme.Border;
        _grid.DefaultCellStyle.Font = Theme.Font9;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 222, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.HeaderBg;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = Theme.Font9B;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        AddColumn("no", "번호", 12, DataGridViewContentAlignment.MiddleLeft);
        AddColumn("item", "작업항목", 42, DataGridViewContentAlignment.MiddleLeft);
        AddColumn("work", "작업", 12, DataGridViewContentAlignment.MiddleCenter);
        AddColumn("time", "시간/청구", 12, DataGridViewContentAlignment.MiddleRight);
        AddColumn("part", "부품금액", 11, DataGridViewContentAlignment.MiddleRight);
        AddColumn("labor", "공임", 11, DataGridViewContentAlignment.MiddleRight);

        _grid.CellMouseDown += OnCellMouseDown;
        _grid.CellDoubleClick += (_, e) => CopyRowItem(e.RowIndex);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && IsMissingRow(e.RowIndex))
                _grid.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = MissingSelect;
        };
    }

    private void AddColumn(string name, string header, int fillWeight, DataGridViewContentAlignment align)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Alignment = align },
        };
        _grid.Columns.Add(column);
    }

    private void LoadRows()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var row in _rows)
        {
            int index = _grid.Rows.Add(row.No, row.Item, row.Work, row.TimeClaim, row.PartAmount, row.Labor);
            var gridRow = _grid.Rows[index];
            gridRow.Tag = row;

            if (IsMissing(row))
            {
                gridRow.DefaultCellStyle.BackColor = MissingBack;
                gridRow.DefaultCellStyle.SelectionBackColor = MissingSelect;
                gridRow.DefaultCellStyle.Font = Theme.Font9B;
            }
        }

        _grid.ResumeLayout();
        _grid.ClearSelection();
        ApplyRowFilter();
    }

    private void ApplyRowFilter()
    {
        bool onlyMissing = _onlyMissing.Checked;
        _grid.SuspendLayout();
        // CurrentCell을 먼저 비워야 숨겨질 행이 현재 셀이어도 예외가 나지 않는다.
        _grid.CurrentCell = null;
        foreach (DataGridViewRow row in _grid.Rows)
            row.Visible = !onlyMissing || (row.Tag is SheetRow sheetRow && IsMissing(sheetRow));
        _grid.ResumeLayout();
    }

    private bool IsMissing(SheetRow row) => _missingKeys.Contains(SheetSource.ItemKey(row.Item));

    private bool IsMissingRow(int rowIndex)
        => rowIndex >= 0 && _grid.Rows[rowIndex].Tag is SheetRow row && IsMissing(row);

    private void OnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.Button == MouseButtons.Left)
        {
            if (IsMissingRow(e.RowIndex))
                CopyRowItem(e.RowIndex);
            return;
        }

        if (e.Button == MouseButtons.Right && _grid.Rows[e.RowIndex].Tag is SheetRow row)
        {
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            var menu = new ContextMenuStrip();
            menu.Items.Add("작업항목 복사", null, (_, _) =>
            {
                _copy(row.Item);
                ShowFeedback($"복사됨: {row.Item}");
            });
            menu.Items.Add("행 전체 복사(탭 구분)", null, (_, _) =>
            {
                string line = string.Join('\t', row.No, row.Item, row.Work, row.TimeClaim, row.PartAmount, row.Labor);
                _copy(line);
                ShowFeedback($"행 복사됨: {row.Item}");
            });
            menu.Show(_grid, _grid.PointToClient(Cursor.Position));
        }
    }

    private void CopyRowItem(int rowIndex)
    {
        if (rowIndex < 0 || _grid.Rows[rowIndex].Tag is not SheetRow row) return;
        _copy(row.Item);
        ShowFeedback($"복사됨: {row.Item}");
    }

    private void ShowFeedback(string message)
    {
        _feedback.Text = message;
        _feedback.ForeColor = Theme.Accent;
    }
}
```

### K. `Dialogs.cs`

```csharp
namespace MyWorkQuickLauncher;

/// <summary>바로가기 추가 / 편집 다이얼로그. 메인창의 소유(modal) 창으로 띄운다.</summary>
public sealed class ShortcutDialog : Form
{
    public ShortcutItem Result { get; }

    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new();
    private readonly TextBox _path = new();
    private readonly TextBox _args = new();
    private readonly TextBox _icon = new();

    public ShortcutDialog(ShortcutItem source)
    {
        Result = new ShortcutItem
        {
            Id = source.Id,
            Kind = source.Kind,
            Name = source.Name,
            Path = source.Path,
            Arguments = source.Arguments,
            Icon = source.Icon,
            IconData = source.IconData,
            Order = source.Order,
        };

        Text = "바로가기 추가 / 편집";
        ClientSize = new Size(400, 258);
        Font = Theme.Font9;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Bg;

        var labels = new[] { "종류", "표시 이름", "실행 경로 / URL", "실행 옵션", "아이콘 경로" };
        for (int i = 0; i < labels.Length; i++)
        {
            Controls.Add(new Label
            {
                Text = labels[i],
                Location = new Point(14, 18 + i * 32),
                Size = new Size(96, 22),
                ForeColor = Theme.Text,
            });
        }

        _kind.Items.AddRange(new object[] { "프로그램", "웹사이트", "폴더 / 파일" });
        _kind.SelectedIndex = (int)source.Kind;
        _kind.SetBounds(116, 14, 200, 24);
        Controls.Add(_kind);

        Place(_name, source.Name, 46);
        Place(_path, source.Path, 78);
        Place(_args, source.Arguments, 110);
        Place(_icon, source.Icon, 142);

        var browse = MakeButton("찾기", 322, 77, 62);
        browse.Click += (_, _) => BrowsePath();
        Controls.Add(browse);

        var browseIcon = MakeButton("찾기", 322, 141, 62);
        browseIcon.Click += (_, _) => BrowseIcon();
        Controls.Add(browseIcon);

        var save = MakeButton("저장", 236, 210, 72);
        save.BackColor = Theme.Accent;
        save.ForeColor = Color.White;
        save.Click += (_, _) => Commit();
        Controls.Add(save);

        var cancel = MakeButton("취소", 316, 210, 72);
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            Activate();
            _name.Focus();
            _name.SelectAll();
        };
    }

    private void Place(TextBox box, string value, int y)
    {
        box.Text = value;
        box.SetBounds(116, y, 200, 24);
        Controls.Add(box);
    }

    private static Button MakeButton(string text, int x, int y, int width)
        => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 26),
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Font9,
            BackColor = Theme.Card,
            Cursor = Cursors.Hand,
        };

    private void BrowsePath()
    {
        if (_kind.SelectedIndex == 2)
        {
            using var folder = new FolderBrowserDialog { Description = "폴더 선택" };
            if (folder.ShowDialog(this) == DialogResult.OK)
            {
                _path.Text = folder.SelectedPath;
                if (string.IsNullOrWhiteSpace(_name.Text))
                    _name.Text = new DirectoryInfo(folder.SelectedPath).Name;
            }
            return;
        }

        using var open = new OpenFileDialog
        {
            Filter = _kind.SelectedIndex == 0
                ? "실행 파일 및 바로가기|*.exe;*.lnk;*.bat;*.cmd;*.com;*.msi;*.py;*.pyw|모든 파일|*.*"
                : "모든 파일|*.*",
        };
        if (open.ShowDialog(this) == DialogResult.OK)
        {
            _path.Text = open.FileName;
            if (string.IsNullOrWhiteSpace(_name.Text))
                _name.Text = Path.GetFileNameWithoutExtension(open.FileName);
        }
    }

    private void BrowseIcon()
    {
        using var open = new OpenFileDialog
        {
            Filter = "아이콘 이미지|*.ico;*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*",
        };
        if (open.ShowDialog(this) == DialogResult.OK)
            _icon.Text = open.FileName;
    }

    private void Commit()
    {
        string name = _name.Text.Trim();
        string path = _path.Text.Trim();

        if (name.Length == 0 || path.Length == 0)
        {
            MessageBox.Show("표시 이름과 실행 경로(또는 URL)를 입력하세요.", "입력 필요",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var kind = (ShortcutKind)_kind.SelectedIndex;
        if (kind == ShortcutKind.Web
            && !path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            path = "https://" + path;
        }

        Result.Kind = kind;
        Result.Name = name;
        Result.Path = path;
        Result.Arguments = _args.Text.Trim();
        Result.Icon = _icon.Text.Trim();
        DialogResult = DialogResult.OK;
    }
}

/// <summary>업무 문구 신규 작성 / 수정 다이얼로그.</summary>
public sealed class NoteDialog : Form
{
    public string NoteTitle => _title.Text.Trim();
    public string NoteText => _body.Text;
    public string NoteGroup => string.IsNullOrWhiteSpace(_group.Text) ? "일반" : _group.Text.Trim();

    private readonly TextBox _title = new();
    private readonly TextBox _body = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
    private readonly ComboBox _group = new() { DropDownStyle = ComboBoxStyle.DropDown };

    public NoteDialog(IEnumerable<NoteItem> existing, NoteItem? note = null)
    {
        Text = note == null ? "새 문구" : "문구 수정";
        ClientSize = new Size(420, 320);
        Font = Theme.Font9;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Bg;

        Controls.Add(new Label { Text = "제목", Location = new Point(14, 16), Size = new Size(50, 22), ForeColor = Theme.Text });
        _title.Text = note?.Title ?? "";
        _title.SetBounds(70, 12, 330, 24);
        Controls.Add(_title);

        Controls.Add(new Label { Text = "그룹", Location = new Point(14, 48), Size = new Size(50, 22), ForeColor = Theme.Text });
        var groups = existing
            .Select(x => string.IsNullOrWhiteSpace(x.Group) ? "일반" : x.Group)
            .Concat(new[] { "일반", "견적", "도장", "보험", "작업지시" })
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCulture)
            .ToArray();
        _group.Items.AddRange(groups.Cast<object>().ToArray());
        _group.Text = note?.Group ?? "일반";
        _group.SetBounds(70, 44, 160, 24);
        Controls.Add(_group);

        Controls.Add(new Label
        {
            Text = "본문 (비우면 제목이 복사됩니다)",
            Location = new Point(14, 78),
            Size = new Size(390, 20),
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
        });
        _body.Text = note?.Text ?? "";
        _body.SetBounds(14, 100, 388, 160);
        Controls.Add(_body);

        var save = new Button
        {
            Text = "저장",
            Location = new Point(248, 274),
            Size = new Size(72, 28),
            BackColor = Theme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Font9,
            Cursor = Cursors.Hand,
        };
        save.Click += (_, _) =>
        {
            if (NoteTitle.Length == 0)
            {
                MessageBox.Show("제목을 입력하세요.", "입력 필요", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
        };
        Controls.Add(save);

        var cancel = new Button
        {
            Text = "취소",
            Location = new Point(328, 274),
            Size = new Size(72, 28),
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Font9,
            BackColor = Theme.Card,
            Cursor = Cursors.Hand,
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;

        Shown += (_, _) =>
        {
            Activate();
            _title.Focus();
            _title.SelectAll();
        };
    }
}
```

### L. `publish.cmd`

```bat
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
```

### M. `installer/` — 배포/설치 스크립트

> **중요**: `install.ps1` / `uninstall.ps1` / `pack.ps1` 은 **UTF-8 BOM**으로 저장해야 합니다
> (Windows PowerShell 5.1이 한글 주석을 깨뜨리지 않도록). §9-8 참고.

#### `installer/INSTALL.cmd`
```bat
@echo off
REM 더블클릭하면 MY WORK QUICK LAUNCHER 를 설치합니다 (관리자 권한 불필요).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
```

#### `installer/UNINSTALL.cmd`
```bat
@echo off
REM MY WORK QUICK LAUNCHER 제거. (설치 후에는 "프로그램 추가/제거"에서도 가능)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
```

#### `installer/install.ps1`
```powershell
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
$Version    = '2.0.0'
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
```

#### `installer/uninstall.ps1`
```powershell
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
```

#### `installer/pack.ps1`
```powershell
<#
  배포 패키지(zip) 조립.
  publish\MyWorkQuickLauncher.exe + installer 스크립트 + 아이콘 + README 를 묶는다.
  결과물: dist\MyWorkQuickLauncher-2.0.0-win-x64.zip
  (압축 해제 후 exe를 바로 실행하면 무설치, INSTALL.cmd를 실행하면 설치)
#>
$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$version = '2.0.0'
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
```

#### `installer/MyWorkQuickLauncher.iss` (Inno Setup — 선택)
```pascal
; MY WORK QUICK LAUNCHER - Inno Setup 스크립트
; 정식 설치 exe(MyWorkQuickLauncher-Setup-2.0.0.exe)를 만든다.
;
; 사용법:
;   1) https://jrsoftware.org/isdl.php 에서 Inno Setup 6 설치
;   2) 먼저 상위 폴더에서  publish.cmd  실행 (publish\MyWorkQuickLauncher.exe 생성)
;   3) 이 파일을 Inno Setup Compiler로 열고 Build  (또는  iscc MyWorkQuickLauncher.iss)
;   4) 결과물: installer\Output\MyWorkQuickLauncher-Setup-2.0.0.exe

#define AppName "MY WORK QUICK LAUNCHER"
#define AppId "MyWorkQuickLauncher"
#define AppVersion "2.0.0"
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
```

### N. `MainForm.FolderWatch.cs` (v1.1.0에서 추가)

```csharp
using System.Diagnostics;
using System.IO.Compression;

namespace MyWorkQuickLauncher;

/// <summary>
/// "5. 파일자동읽기 폴더지정" — 지정한 폴더(예: 다운로드)에 그림 파일이 든 zip이 도착하면
/// 등록해둔 프로그램(예: 중복 사진 정리 도구)을 그 zip과 함께 실행하고, 뒤이어 뜨는
/// 압축 프로그램의 "압축풀기" 류 창은 자동으로 닫아준다.
/// </summary>
public sealed partial class MainForm
{
    private TextBox _watchFolderBox = null!;
    private ComboBox _watchProgramCombo = null!;
    private TextBox _watchCloseTitleBox = null!;
    private CheckBox _watchEnabledCheck = null!;
    private Label _watchStatus = null!;

    private FileSystemWatcher? _folderWatcher;
    private readonly HashSet<string> _watchProcessedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _watchLock = new();

    private Control BuildFolderWatchSection()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 10, 12, 10),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < 4; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        string downloadsHint = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // ---- 감시 폴더 ----
        panel.Controls.Add(WatchFieldLabel("감시 폴더 (URL 자리에 경로 입력)"), 0, 0);

        var folderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Margin = new Padding(0, 3, 0, 3),
        };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _watchFolderBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = Theme.Font9,
            Text = _data.Settings.WatchFolder,
            PlaceholderText = downloadsHint + "  (예: 다운로드 폴더)",
        };
        folderRow.Controls.Add(_watchFolderBox, 0, 0);

        var browse = Theme.ActionButton("찾아보기");
        browse.Margin = new Padding(6, 0, 0, 0);
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "감시할 폴더 선택 (예: 다운로드)",
                SelectedPath = Directory.Exists(_watchFolderBox.Text) ? _watchFolderBox.Text : downloadsHint,
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _watchFolderBox.Text = dialog.SelectedPath;
        };
        folderRow.Controls.Add(browse, 1, 0);
        panel.Controls.Add(folderRow, 1, 0);

        // ---- 실행할 프로그램 ----
        panel.Controls.Add(WatchFieldLabel("그림이 든 zip 도착 시 실행할 프로그램"), 0, 1);
        _watchProgramCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font9,
        };
        panel.Controls.Add(_watchProgramCombo, 1, 1);

        // ---- 자동으로 닫을 창 제목 ----
        panel.Controls.Add(WatchFieldLabel("자동으로 닫을 압축 창 제목(포함 문자열)"), 0, 2);
        _watchCloseTitleBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = Theme.Font9,
            Text = _data.Settings.WatchCloseWindowTitleContains,
            PlaceholderText = "예: 압축풀기  (비우면 이 동작을 끕니다)",
        };
        panel.Controls.Add(_watchCloseTitleBox, 1, 2);

        // ---- 사용 여부 / 저장 / 상태 ----
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
        };

        _watchEnabledCheck = new CheckBox
        {
            Text = "자동 감시 사용",
            Checked = _data.Settings.WatchEnabled,
            AutoSize = true,
            Margin = new Padding(0, 6, 14, 0),
        };
        actions.Controls.Add(_watchEnabledCheck);

        var save = Theme.FlatButton("저장", Theme.Accent, Color.White);
        save.Margin = new Padding(0, 0, 12, 0);
        save.Click += (_, _) => SaveWatchSettings();
        actions.Controls.Add(save);

        _watchStatus = new Label
        {
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Margin = new Padding(0, 7, 0, 0),
        };
        actions.Controls.Add(_watchStatus);

        panel.Controls.Add(actions, 1, 3);

        RefreshWatchProgramOptions();
        RefreshWatchStatusLabel();

        return SectionShell("5. 파일자동읽기 폴더지정", null, null, panel);
    }

    private static Label WatchFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.Text,
        Font = Theme.Font8,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 6, 12, 6),
    };

    private void RefreshWatchProgramOptions()
    {
        if (_watchProgramCombo == null) return;

        var apps = _data.Shortcuts.Where(x => x.Kind == ShortcutKind.App).OrderBy(x => x.Order).ToList();

        _watchProgramCombo.DataSource = null;
        _watchProgramCombo.Items.Clear();

        if (apps.Count == 0)
        {
            _watchProgramCombo.Enabled = false;
            return;
        }

        _watchProgramCombo.Enabled = true;
        _watchProgramCombo.DisplayMember = "Name";
        _watchProgramCombo.ValueMember = "Id";
        _watchProgramCombo.DataSource = apps;

        var match = apps.FirstOrDefault(x => x.Id == _data.Settings.WatchProgramShortcutId);
        _watchProgramCombo.SelectedItem = match ?? apps[0];
    }

    private void RefreshWatchStatusLabel()
    {
        if (_watchStatus == null) return;
        _watchStatus.Text = _folderWatcher != null
            ? $"감시 중: {_data.Settings.WatchFolder}"
            : "감시 꺼짐";
    }

    private void SaveWatchSettings()
    {
        string folder = _watchFolderBox.Text.Trim();
        if (folder.Length == 0)
            folder = _watchFolderBox.PlaceholderText.Split(' ')[0];

        if (!Directory.Exists(folder))
        {
            MessageBox.Show($"폴더를 찾을 수 없습니다.\n\n{folder}", "경로 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_watchEnabledCheck.Checked && _watchProgramCombo.SelectedItem is not ShortcutItem)
        {
            MessageBox.Show(
                "실행할 프로그램을 선택하세요.\n(먼저 '1. 바로가기'에 프로그램을 등록해야 목록에 나타납니다)",
                "프로그램 선택 필요", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _data.Settings.WatchFolder = folder;
        _data.Settings.WatchProgramShortcutId = (_watchProgramCombo.SelectedItem as ShortcutItem)?.Id ?? "";
        _data.Settings.WatchCloseWindowTitleContains = _watchCloseTitleBox.Text.Trim();
        _data.Settings.WatchEnabled = _watchEnabledCheck.Checked;

        Persist();
        ApplyWatchState();
        Flash("자동 감시 설정을 저장했습니다.");
        AppStore.Log($"[WATCH] settings saved folder={folder} enabled={_data.Settings.WatchEnabled}");
    }

    /// <summary>저장된 설정에 맞춰 감시를 다시 시작/중지한다. 앱 시작 시와 설정 저장 시 호출.</summary>
    private void ApplyWatchState()
    {
        StopWatching();
        if (_data.Settings.WatchEnabled && Directory.Exists(_data.Settings.WatchFolder))
            StartWatching(_data.Settings.WatchFolder);
        RefreshWatchStatusLabel();
    }

    private void StartWatching(string folder)
    {
        try
        {
            var watcher = new FileSystemWatcher(folder, "*.zip")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            };
            watcher.Created += (_, e) => HandleNewZip(e.FullPath);
            watcher.Renamed += (_, e) => HandleNewZip(e.FullPath);
            watcher.Error += (_, e) => AppStore.Log("[WATCH] watcher error", e.GetException());
            watcher.EnableRaisingEvents = true;
            _folderWatcher = watcher;
            AppStore.Log($"[WATCH] started {folder}");
        }
        catch (Exception ex)
        {
            AppStore.Log("[WATCH] start failed", ex);
            _folderWatcher = null;
        }
    }

    private void StopWatching()
    {
        if (_folderWatcher == null) return;
        try
        {
            _folderWatcher.EnableRaisingEvents = false;
            _folderWatcher.Dispose();
        }
        catch
        {
            // ignore
        }
        _folderWatcher = null;
    }

    /// <summary>FileSystemWatcher 콜백(백그라운드 스레드)에서 호출된다. 중복 처리 방지 후 비동기로 넘긴다.</summary>
    private void HandleNewZip(string path)
    {
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return;

        lock (_watchLock)
        {
            if (!_watchProcessedFiles.Add(path)) return;
        }

        _ = Task.Run(() => ProcessNewZipAsync(path));
    }

    private async Task ProcessNewZipAsync(string path)
    {
        try
        {
            if (!await WaitUntilReadableAsync(path, TimeSpan.FromSeconds(30)))
            {
                AppStore.Log($"[WATCH] gave up waiting for file to finish: {path}");
                return;
            }

            if (!ZipContainsImage(path))
            {
                AppStore.Log($"[WATCH] skip (이미지 없음) {Path.GetFileName(path)}");
                return;
            }

            AppStore.Log($"[WATCH] zip 감지: {path}");
            try { BeginInvoke(new Action(() => LaunchWatchProgram(path))); }
            catch (ObjectDisposedException) { return; } // 창이 이미 닫힘

            // 알집 등 압축 프로그램이 뒤이어 "압축풀기" 류 창을 띄우면 잠깐 지켜보다 자동으로 닫는다.
            string titleFilter = _data.Settings.WatchCloseWindowTitleContains;
            await CloseSpawnedWindowsAsync(titleFilter, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            AppStore.Log("[WATCH] 처리 실패", ex);
        }
    }

    private static async Task<bool> WaitUntilReadableAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return stream.Length > 0;
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private static bool ZipContainsImage(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.Entries.Any(entry => IsImageExtension(Path.GetExtension(entry.FullName)));
        }
        catch
        {
            return false;
        }
    }

    private void LaunchWatchProgram(string zipPath)
    {
        var item = _data.Shortcuts.FirstOrDefault(x =>
            x.Id == _data.Settings.WatchProgramShortcutId && x.Kind == ShortcutKind.App);

        if (item == null)
        {
            AppStore.Log("[WATCH] 실행할 프로그램이 지정되지 않음");
            Flash("자동 감시: 실행할 프로그램이 지정되지 않았습니다.");
            return;
        }

        try
        {
            string extraArgs = string.IsNullOrWhiteSpace(item.Arguments) ? "" : item.Arguments + " ";
            var start = new ProcessStartInfo
            {
                FileName = item.Path,
                Arguments = $"{extraArgs}\"{zipPath}\"",
                UseShellExecute = true,
            };
            Process.Start(start);
            AppStore.Log($"[WATCH] 실행: {item.Name} <- {zipPath}");
            Flash($"자동 감시: '{item.Name}' 실행 ({Path.GetFileName(zipPath)})");
        }
        catch (Exception ex)
        {
            AppStore.Log("[WATCH] 프로그램 실행 실패", ex);
        }
    }

    /// <summary>제목에 <paramref name="titleContains"/>가 포함된 창을 일정 시간 지켜보다 뜨면 자동으로 닫는다.</summary>
    private static async Task CloseSpawnedWindowsAsync(string titleContains, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(titleContains)) return;

        var closed = new HashSet<IntPtr>();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            foreach (var hWnd in Native.FindVisibleWindowsByTitle(titleContains))
            {
                if (closed.Add(hWnd))
                {
                    Native.RequestCloseWindow(hWnd);
                    AppStore.Log($"[WATCH] '{titleContains}' 포함 창 닫기 요청");
                }
            }
            await Task.Delay(400);
        }
    }
}
```

---

## 부록 A. `assets/app.ico` 재생성

아이콘 원본이 없으면 아래 PowerShell로 동일하게 다시 만들 수 있습니다(256px, 네이비 배경 + 파란 사각 + 흰 번개).

```powershell
Add-Type -AssemblyName System.Drawing
$sz = 256
$bmp = New-Object System.Drawing.Bitmap $sz, $sz
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
function RoundRect($gr,$rect,$rad,$brush){
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $rad*2
  $p.AddArc($rect.X,$rect.Y,$d,$d,180,90)
  $p.AddArc($rect.Right-$d,$rect.Y,$d,$d,270,90)
  $p.AddArc($rect.Right-$d,$rect.Bottom-$d,$d,$d,0,90)
  $p.AddArc($rect.X,$rect.Bottom-$d,$d,$d,90,90)
  $p.CloseFigure(); $gr.FillPath($brush,$p)
}
$navy  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,15,23,42))
$blue  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,37,99,235))
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
RoundRect $g (New-Object System.Drawing.Rectangle 0,0,$sz,$sz) 52 $navy
RoundRect $g (New-Object System.Drawing.Rectangle 34,34,188,188) 40 $blue
$pts = @(
  (New-Object System.Drawing.PointF 150,52),(New-Object System.Drawing.PointF 96,140),
  (New-Object System.Drawing.PointF 132,140),(New-Object System.Drawing.PointF 110,204),
  (New-Object System.Drawing.PointF 170,116),(New-Object System.Drawing.PointF 132,116))
$g.FillPolygon($white, $pts); $g.Dispose()
$png = "$env:TEMP\app256.png"
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = [System.IO.File]::ReadAllBytes($png)
$ico = "assets\app.ico"
New-Item -ItemType Directory -Force -Path "assets" | Out-Null
$fs = [System.IO.File]::Create($ico); $bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]1)
$bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([Byte]0)
$bw.Write([UInt16]1); $bw.Write([UInt16]32)
$bw.Write([UInt32]$pngBytes.Length); $bw.Write([UInt32]22); $bw.Write($pngBytes)
$bw.Flush(); $fs.Close()
```

## 부록 B. 처음부터 완전 재현 체크리스트

```
[ ] .NET SDK 10 설치 후  dotnet --version  이  10.0.x
[ ] 소스 12개 파일(.cs) + .csproj + assets/app.ico 배치 (§2, §6)
[ ] installer/ 6개 파일 배치, ps1 3개는 UTF-8 BOM 확인 (§6-M, §9-8)
[ ] dotnet build -c Release  →  경고 0 / 오류 0 (§8-1)
[ ] 실행 → 4개 번호 섹션 표시 (§8-2), launcher.log 에 [APP START] (§8-3)
[ ] 바탕화면 필터 클릭/문구 복사/시트 분석 동작 확인 (§8-4·5)
[ ] publish.cmd → publish\MyWorkQuickLauncher.exe (약 47MB)
[ ] installer\pack.ps1 → dist\...zip (약 41.5MB)
[ ] (선택) Inno Setup 으로 setup.exe
```

---
_문서 끝. 문의는 이 폴더의 `README.md`와 함께 참고._


---

## 7. 데이터베이스 / 스키마

**데이터베이스 없음.** 영속 데이터는 단일 JSON 파일입니다.

| 항목 | 내용 |
| --- | --- |
| 저장 위치 | `%APPDATA%\MyWorkQuickLauncher\data.json` |
| 형식 | UTF-8, `System.Text.Json` 들여쓰기 직렬화, enum은 문자열(`JsonStringEnumConverter`) |
| 스키마 | §4의 예시 = 전체 스키마 (`AppData` = `Shortcuts[]` + `Notes[]` + `Settings`) |
| 마이그레이션 | 없음. 필드가 없으면 C# 프로퍼티 기본값이 채워짐(하위 호환). 1차 버전의 `WebsiteLayoutMigrated`는 **제거**됨 |
| 저장 방식 | 원자적: `data.json.tmp`에 쓰고 `File.Copy(overwrite)` 후 tmp 삭제 (`AppStore.Save`) |
| 초기 시드 | 새 파일일 때 안내 문구 1개 자동 생성(§6-C `AppStore.Load`) |
| 로그 | `%APPDATA%\MyWorkQuickLauncher\launcher.log` (append, `yyyy-MM-dd HH:mm:ss.fff [TAG] msg`) |

아이콘 캐시는 `IconData`(base64 PNG)로 JSON 안에 함께 저장되므로 별도 파일/폴더가 없습니다.

---

## 8. 검증 방법

### 8-1. 빌드 검증
```bat
cd "6. MyWorkQuickLauncher"
dotnet build -c Release
```
**기대 출력**(마지막 줄):
```
빌드했습니다.
    경고 0개
    오류 0개
```
(영문 SDK면 `Build succeeded. 0 Warning(s) 0 Error(s)`)

### 8-2. 실행 검증
`bin\Release\net10.0-windows\MyWorkQuickLauncher.exe` 실행 → 다음이 보여야 정상:
- 제목 표시줄 `MY WORK QUICK LAUNCHER  ·  BUILD 20260916`
- 번호 붙은 5개 섹션: `1. 바로가기`(테두리 박스), `2. 시트 분석 TOOL`, `3. 업무 메모 / 자주 쓰는 문구`, `4. 바탕화면 파일검색`(테두리 박스), `5. 파일자동읽기 폴더지정`
- 하단 상태바 `준비됨 · Explorer에서 파일을 이 창으로 끌어오면 바로 등록됩니다 · F5 새로고침`

### 8-3. 로그 검증
실행 직후 `%APPDATA%\MyWorkQuickLauncher\launcher.log` 마지막 줄들:
```
2026-09-16 10:24:20.000 [APP START] BUILD=20260916 EXE=...\MyWorkQuickLauncher.exe
2026-09-16 10:24:25.000 [SEARCH] start id=1 roots=C:\Users\<너>\Desktop filter=JPG / 이미지
2026-09-16 10:24:25.000 [SEARCH] complete id=1 found=<바탕화면 최상위 파일 수>
2026-09-16 10:24:25.000 [WATCH] started <감시 폴더 경로>
```

### 8-7. 파일자동읽기 폴더지정 검증
1. `5. 파일자동읽기 폴더지정`에서 테스트용 빈 폴더를 감시 폴더로 지정, "1. 바로가기"에 등록된 아무 프로그램(테스트용으로는 메모장도 가능: `C:\Windows\System32\notepad.exe`)을 실행할 프로그램으로 선택, "자동 감시 사용" 체크 후 저장.
2. 이미지 파일이 하나 이상 들어 있는 `.zip`을 그 폴더에 복사(다운로드가 아니어도, 탐색기에서 복사해 넣어도 동일하게 감지됨).
3. **기대**: `launcher.log`에 아래 순서로 남고, 선택한 프로그램이 zip 경로를 인자로 받아 실행됨.
   ```
   [WATCH] zip 감지: <zip 경로>
   [WATCH] 실행: <프로그램 이름> <- <zip 경로>
   ```
4. (선택) 같은 10초 안에 제목에 설정한 문자열(기본 "압축풀기")이 포함된 창을 띄워보면
   `[WATCH] '<문자열>' 포함 창 닫기 요청` 로그와 함께 그 창이 자동으로 닫힌다.
   개발 중 실제로 `cmd /c title 압축풀기테스트 ...` 로 만든 창을 이 방식으로 자동 종료시켜 확인함.
- 바탕화면 파일 필터를 눌러보면 `[CLICK] file_filter=Excel` 등이 남습니다.
- 업무 메모 타일 좌클릭 → `[PHRASE CLICK] 예시문구` + `[CLIPBOARD] 예시문구` 이 남고 실제로 붙여넣기가 됩니다.

### 8-4. 바탕화면 검색 검증
- `JPG / 이미지` 선택 시 **바탕화면 최상위**의 이미지 파일만 나와야 함(하위 폴더 파일은 제외).
- 1차 버전은 재귀 검색으로 수천~1만 건이 나왔지만, 현재 버전은 보통 수십 건.
  (본 개발 PC 실측: 재귀 `found=11188` → 비재귀 `found=36`)
- 결과 카드 1클릭 → Explorer가 열리며 해당 파일 선택. 더블클릭 → 기본 프로그램으로 실행.

### 8-5. 시트 분석 TOOL 검증 (파서 단위 검증)
gomail 접속이 되는 환경에서, A시트=`.../ant/gongim/popup_detail1.php?eseq=<X>`,
B시트=`.../ant/api/popup_detail.php?eseq=<Y>` 입력 후 `작업항목 비교 분석`.
**기대**: 상태줄 `A시트 N행 · B시트 M행 · B시트에 없는 작업항목 K건(...행)`,
팝업의 표에서 **B시트에 없는 작업항목 행이 노란색**, 노란색 행 클릭 시 하단에 `복사됨: <작업항목>`.

파서만 따로 검증(파이썬, C# 정규식과 동일 로직) — 개발 중 실제 사내 URL(예시로 대체 표기)로 확인한 값:
```
A(popup_detail1, eseq=<실제 케이스 ID>): 328행
B(api/popup_detail, eseq=<실제 케이스 ID>): 109행
B에 없는 작업항목: 301건 / 309행
```
C# 실행 로그에서도 동일: `[SHEET] analyze done left=328 right=109 missing=301`.
(실제 eseq 값과 서버 주소는 사내 정보라 이 문서에는 남기지 않았습니다. 본인 환경의 실제 URL로 재현하면 동일하게 검증됩니다.)
```python
# 참고용 검증 스크립트 (C#의 SheetSource.Parse 와 같은 정규식)
import re, html, urllib.request
def parse(url):
    s = urllib.request.urlopen(url, timeout=40).read().decode('utf-8', 'replace')
    ts, te = s.lower().find('<table'), s.lower().find('</table>')
    scope = s[ts:te] if ts >= 0 and te > ts else s
    rows = []
    for m in re.finditer(r"<tr\b[^>]*>(.*?)(?=<tr\b|</table>|$)", scope, re.S | re.I):
        if 'tablerow' not in m.group(0).lower():
            continue
        cells = [re.sub(r'\s+', ' ', html.unescape(re.sub(r'<[^>]+>', ' ', c))).strip()
                 for c in re.findall(r"<td\b[^>]*>(.*?)</td>", m.group(0), re.S | re.I)]
        if len(cells) >= 2 and cells[1] not in ('', '작업항목', '작업내용'):
            rows.append(cells[1])
    return rows
key = lambda x: re.sub(r'\s+', '', x)
L, R = parse(A_URL), parse(B_URL)
missing = {key(i) for i in L} - {key(i) for i in R}
print(len(L), len(R), len(missing))
```

### 8-6. 설치 스크립트 검증
```powershell
powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Silent -NoDesktopShortcut
# 기대: %LOCALAPPDATA%\Programs\MyWorkQuickLauncher\MyWorkQuickLauncher.exe 존재,
#      시작 메뉴 lnk 존재,
#      HKCU:\...\Uninstall\MyWorkQuickLauncher 의 DisplayName="MY WORK QUICK LAUNCHER"
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\MyWorkQuickLauncher\uninstall.ps1" -Silent
# 기대: 설치 폴더/레지스트리/바로가기 모두 삭제, %APPDATA% 데이터는 유지
```
(개발 중 이 왕복을 실제로 실행해 dir/reg/lnk가 생성→삭제됨을 확인함.)

---

## 9. 알려진 이슈 및 트러블슈팅

개발 중 실제로 겪은 것들입니다. 재현 시 같은 곳에서 막히면 아래를 참고하세요.

### 9-1. `error CS7036: OnAnyDragEnter의 필수 매개 변수 'e'에 해당하는 인수가 없습니다`
- 원인: `DragEnter += (_, e) => OnAnyDragEnter(e);` 처럼 `(sender, e)` 시그니처 메서드에 `e`만 넘김.
- 해결: 이벤트 핸들러는 메서드 그룹으로 직접 등록 — `DragEnter += OnAnyDragEnter;`

### 9-2. 섹션 사이에 거대한 세로 공백이 생김
- 원인: `FlowLayoutPanel`(WrapContents=true)을 `AutoSize` `TableLayoutPanel` 셀에 Dock=Fill로 넣으면,
  폭이 확정되기 전에 높이를 측정해 "전부 세로로 쌓은" 크기로 잡힘.
- 해결: `ApplyResponsiveWidths()`에서 `_stack.PerformLayout()` 후 각 카드 FlowLayoutPanel의
  `MinimumSize`/`MaximumSize` 폭을 **부모 폭 - 패딩**으로 고정하고 다시 `PerformLayout()`.
  (`_phraseFlow`, `_resultsFlow`, `_filterFlow`은 앵커/내부 스크롤을 쓰므로 제외)

### 9-3. 문구 타일 더블클릭 편집이 안 됨 (1차 버전 버그)
- 원인: `Button`은 기본적으로 `DoubleClick`/`MouseDoubleClick`을 발생시키지 않음
  (`ControlStyles.StandardDoubleClick`이 꺼져 있음).
- 해결: 타일을 `Label`로 만들고 `MouseClick`(복사) + `MouseDoubleClick`(편집) 사용.

### 9-4. 문구 복사가 가끔 실패
- 원인: 다른 프로세스가 클립보드를 잠깐 잠금.
- 해결: `SetClipboardText`에서 `Clipboard.SetText` 4회 재시도 후 `Clipboard.SetDataObject(text, true, 4, 60)` 폴백.

### 9-5. 바탕화면 필터 클릭이 먹통 (1차 버전 버그)
- 원인: 스캔 중이면 `SelectDesktopFilter`가 조기 `return` 하여 탭 색만 바뀌고 결과가 안 바뀜.
- 해결: 항상 `RefreshFilterButtons()` + `ApplyFilter()`(캐시 기준 즉시 렌더). 스캔 완료 시 다시 적용.

### 9-6. 파일 검색 중 UI가 멈칫 (1차 버전)
- 원인: 이미지 썸네일을 UI 스레드에서 동기 로딩(`Image.FromFile` + `GetThumbnailImage`).
- 해결: `LoadThumbnailAsync` — `Task.Run`으로 만들고 `BeginInvoke`로 살아있는 `PictureBox`에만 반영,
  `_thumbnails` 딕셔너리에 경로별 캐시(lock). 40MB 초과 이미지는 아이콘으로 대체.

### 9-7. `CodePagesEncodingProvider` 컴파일 오류
- 원인: EUC-KR 폴백을 넣으려다 `System.Text.Encoding.CodePages`(별도 패키지) 참조.
- 해결: gomail 페이지가 UTF-8이므로 `Encoding.UTF8.GetString` 하나로 단순화(패키지 추가 안 함).

### 9-8. 설치 PowerShell 스크립트 파싱 오류 (한글 주석)
- 증상: `powershell.exe`(Windows PowerShell 5.1)로 실행 시 `'}' 토큰이 예상되지 않음` 등.
- 원인: BOM 없는 UTF-8 스크립트를 5.1이 시스템 코드페이지로 읽어 한글이 깨지며 토큰 스트림 손상.
- 해결: `install.ps1` / `uninstall.ps1` / `pack.ps1` 을 **UTF-8 BOM**으로 저장.
  (편집 후에는 반드시 BOM 유지. 다시 저장할 때
  `[IO.File]::WriteAllText(path, text, (New-Object Text.UTF8Encoding $true))`)

### 9-9. `dotnet publish` self-contained가 오래 걸리거나 실패
- 원인: 최초 1회 win-x64 런타임 팩 복원(네트워크 필요).
- 해결: 인터넷 되는 곳에서 한 번 `publish.cmd` 실행하면 이후 캐시됨. 사내망만 되는 PC라면
  다른 PC에서 만든 `publish\MyWorkQuickLauncher.exe`를 복사해 와도 됨(자체 포함).

### 9-10. 시트 분석에서 "URL을 확인하세요"
- `SheetSource.LooksLikeUrl`이 `http`/`https` 절대 URL만 통과. `호스트명:8180`처럼 포트가 붙어도 OK.
- 사내망/VPN이 아니면 사내 서버 접속 자체가 안 될 수 있음 → 팝업 대신 "시트를 불러오지 못했습니다".

### 9-11. 창이 화면 밖에서 열림(모니터 분리 후)
- `ApplyGeometry`가 저장된 `Bounds`가 어떤 모니터와도 겹치지 않으면 주 모니터 중앙으로 이동.
- 그래도 이상하면 `data.json`의 `Settings.Geometry`를 `1000x740+80+60`으로 직접 수정.

### 9-12. 미니모드로 갇힘
- `Ctrl+M` 토글, 또는 `data.json`에서 `Settings.MiniMode`를 `false`로.

### 9-13. 창이 항상 다른 창 위에 떠서 브라우저·카톡이 안 보임 (BUILD 20260909 에서 수정)
- 1차 배포본(20260908)은 `AlwaysOnTop`을 `data.json`에 저장·복원해서, 한 번 켜면
  재실행해도 계속 다른 창을 가림.
- 수정: `TopMost`는 시작 시 **항상 꺼짐**. "항상 위" 체크박스는 **현재 세션에만** 적용되고
  저장하지 않음(다음 실행 때 다시 꺼짐). `AlwaysOnTop` 설정 키 제거(옛 값은 무시).

---

## 변경 이력

| 빌드 | 변경 |
| --- | --- |
| 20260908 | v2 최초. 1차(Codex) 버전을 같은 명세로 재작성. UI 4개 번호 섹션, 시트 분석 TOOL 추가, 바탕화면 검색 비재귀화 |
| 20260909 | "항상 위(TopMost)"를 저장/복원하지 않도록 변경 — 시작 시 항상 꺼짐, 체크박스는 세션 한정 (§9-13) |
| 20260915 | GitHub 공개(v1.0.0) — 사내 서버 호스트명/실제 케이스 ID를 예시 값으로 치환, README/LICENSE/.gitignore/CHANGELOG/설치 스크립트 정리 |
| 20260916 | `5. 파일자동읽기 폴더지정` 추가(v1.1.0) — 감시 폴더의 신규 zip을 감지해 등록된 프로그램 자동 실행 + 압축 확인 창 자동 닫기(`MainForm.FolderWatch.cs`, `Native.cs`에 `EnumWindows`/`WM_CLOSE` 추가) |
