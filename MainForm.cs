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
