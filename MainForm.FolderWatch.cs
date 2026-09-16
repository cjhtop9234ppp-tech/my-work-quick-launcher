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
