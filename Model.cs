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
