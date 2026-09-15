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
