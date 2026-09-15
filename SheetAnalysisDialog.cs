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
