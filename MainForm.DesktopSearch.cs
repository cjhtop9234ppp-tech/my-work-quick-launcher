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
