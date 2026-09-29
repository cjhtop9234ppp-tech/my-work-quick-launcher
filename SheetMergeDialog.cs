using System.Diagnostics;

namespace MyWorkQuickLauncher;

/// <summary>
/// 작업항목 병합 결과 팝업. A~D 네 시트를 작업항목 기준으로 중복 없이 병합해서 보여준다.
/// A시트 값을 먼저 나열하고, 그 뒤에 A시트에 없는 값을 B, C, D 순서로 각 시트의 원래 행 순서를
/// 유지한 채 이어 붙인다. 각 행은 어느 시트에서 왔는지(A=흰색, B=파란색, C=노란색, D=보라색)로
/// 구분하며, "엑셀로 다운로드"는 화면과 같은 색상을 그대로 살린 .xlsx로 저장한다.
/// </summary>
public sealed class SheetMergeDialog : Form
{
    private static readonly Color OriginAFill = Color.White;
    private static readonly Color OriginBFill = Color.FromArgb(191, 219, 254);
    private static readonly Color OriginCFill = Color.FromArgb(255, 244, 143);
    private static readonly Color OriginDFill = Color.FromArgb(233, 213, 255);

    private const string OriginAArgb = "FFFFFFFF";
    private const string OriginBArgb = "FFBFDBFE";
    private const string OriginCArgb = "FFFFF48F";
    private const string OriginDArgb = "FFE9D5FF";

    private readonly Action<string> _copy;
    private readonly DataGridView _grid = new();
    private readonly Label _feedback = new();
    private readonly IReadOnlyList<MergedSheetRow> _rows;

    public SheetMergeDialog(IReadOnlyList<MergedSheetRow> rows, Action<string> copy)
    {
        _rows = rows;
        _copy = copy;

        int countA = rows.Count(r => r.Origin == 'A');
        int countB = rows.Count(r => r.Origin == 'B');
        int countC = rows.Count(r => r.Origin == 'C');
        int countD = rows.Count(r => r.Origin == 'D');

        Text = "작업항목 병합 결과";
        ClientSize = new Size(960, 640);
        MinimumSize = new Size(680, 420);
        Font = Theme.Font9;
        BackColor = Theme.Bg;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = true;
        MinimizeBox = false;

        // ---- 상단 요약 ----
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Card, Padding = new Padding(14, 10, 14, 10) };

        header.Controls.Add(new Label
        {
            Text = $"병합 결과 총 {rows.Count}건   ·   A {countA}   B {countB}   C {countC}   D {countD}",
            AutoSize = true,
            Font = Theme.Font11B,
            ForeColor = Theme.Text,
            Location = new Point(2, 4),
        });

        header.Controls.Add(BuildLegend());

        var actionsRow = new FlowLayoutPanel
        {
            Location = new Point(2, 62),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Card,
        };

        var copyAll = Theme.ActionButton("전체 작업항목 복사");
        copyAll.Margin = new Padding(0, 0, 8, 0);
        copyAll.Enabled = rows.Count > 0;
        copyAll.Click += (_, _) =>
        {
            _copy(string.Join(Environment.NewLine, rows.Select(r => r.Row.Item)));
            ShowFeedback($"작업항목 {rows.Count}건을 복사했습니다.");
        };
        actionsRow.Controls.Add(copyAll);

        var exportExcel = Theme.ActionButton("엑셀로 다운로드");
        exportExcel.Click += (_, _) => ExportToExcel();
        actionsRow.Controls.Add(exportExcel);

        header.Controls.Add(actionsRow);

        Controls.Add(header);

        // ---- 하단 피드백 ----
        _feedback.Dock = DockStyle.Bottom;
        _feedback.Height = 26;
        _feedback.BackColor = Color.FromArgb(226, 232, 240);
        _feedback.ForeColor = Theme.Muted;
        _feedback.Font = Theme.Font8;
        _feedback.Padding = new Padding(14, 5, 8, 0);
        _feedback.Text = rows.Count > 0
            ? "행을 클릭하면 작업항목이 복사됩니다."
            : "병합된 작업항목이 없습니다.";
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
        close.Location = new Point(header.ClientSize.Width - 84, 4);
        header.Controls.Add(close);
        header.Resize += (_, _) => close.Location = new Point(header.ClientSize.Width - 84, 4);
        CancelButton = close;
        AcceptButton = close;
    }

    private static Control BuildLegend()
    {
        var legend = new FlowLayoutPanel
        {
            Location = new Point(2, 34),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Card,
        };
        legend.Controls.Add(LegendChip("A시트", OriginAFill, outline: true));
        legend.Controls.Add(LegendChip("B시트", OriginBFill, outline: false));
        legend.Controls.Add(LegendChip("C시트", OriginCFill, outline: false));
        legend.Controls.Add(LegendChip("D시트", OriginDFill, outline: false));
        return legend;
    }

    private static Control LegendChip(string text, Color color, bool outline)
    {
        return new Label
        {
            Text = "  " + text + "  ",
            AutoSize = true,
            BackColor = color,
            ForeColor = Theme.Text,
            Font = Theme.Font8,
            Padding = new Padding(4, 2, 4, 2),
            Margin = new Padding(0, 0, 8, 0),
            BorderStyle = outline ? BorderStyle.FixedSingle : BorderStyle.None,
        };
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
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.HeaderBg;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = Theme.Font9B;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        AddColumn("origin", "출처", 8, DataGridViewContentAlignment.MiddleCenter);
        AddColumn("no", "번호", 10, DataGridViewContentAlignment.MiddleLeft);
        AddColumn("item", "작업항목", 40, DataGridViewContentAlignment.MiddleLeft);
        AddColumn("work", "작업", 12, DataGridViewContentAlignment.MiddleCenter);
        AddColumn("time", "시간/청구", 12, DataGridViewContentAlignment.MiddleRight);
        AddColumn("part", "부품금액", 11, DataGridViewContentAlignment.MiddleRight);
        AddColumn("labor", "공임", 11, DataGridViewContentAlignment.MiddleRight);

        _grid.CellMouseDown += OnCellMouseDown;
        _grid.CellDoubleClick += (_, e) => CopyRowItem(e.RowIndex);
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

    private static Color BackColorFor(char origin) => origin switch
    {
        'A' => OriginAFill,
        'B' => OriginBFill,
        'C' => OriginCFill,
        'D' => OriginDFill,
        _ => Color.White,
    };

    private static string ArgbFor(char origin) => origin switch
    {
        'A' => OriginAArgb,
        'B' => OriginBArgb,
        'C' => OriginCArgb,
        'D' => OriginDArgb,
        _ => OriginAArgb,
    };

    private void LoadRows()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var merged in _rows)
        {
            var row = merged.Row;
            int index = _grid.Rows.Add(merged.Origin.ToString(), row.No, row.Item, row.Work, row.TimeClaim, row.PartAmount, row.Labor);
            var gridRow = _grid.Rows[index];
            gridRow.Tag = merged;
            gridRow.DefaultCellStyle.BackColor = BackColorFor(merged.Origin);
            gridRow.DefaultCellStyle.SelectionBackColor = Color.FromArgb(207, 222, 252);
        }

        _grid.ResumeLayout();
        _grid.ClearSelection();
    }

    private void OnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (e.Button == MouseButtons.Left)
        {
            CopyRowItem(e.RowIndex);
            return;
        }

        if (e.Button == MouseButtons.Right && _grid.Rows[e.RowIndex].Tag is MergedSheetRow merged)
        {
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            var menu = new ContextMenuStrip();
            menu.Items.Add("작업항목 복사", null, (_, _) =>
            {
                _copy(merged.Row.Item);
                ShowFeedback($"복사됨: {merged.Row.Item}");
            });
            menu.Items.Add("행 전체 복사(탭 구분)", null, (_, _) =>
            {
                var row = merged.Row;
                string line = string.Join('\t', merged.Origin, row.No, row.Item, row.Work, row.TimeClaim, row.PartAmount, row.Labor);
                _copy(line);
                ShowFeedback($"행 복사됨: {row.Item}");
            });
            menu.Show(_grid, _grid.PointToClient(Cursor.Position));
        }
    }

    private void CopyRowItem(int rowIndex)
    {
        if (rowIndex < 0 || _grid.Rows[rowIndex].Tag is not MergedSheetRow merged) return;
        _copy(merged.Row.Item);
        ShowFeedback($"복사됨: {merged.Row.Item}");
    }

    private void ShowFeedback(string message)
    {
        _feedback.Text = message;
        _feedback.ForeColor = Theme.Accent;
    }

    /// <summary>
    /// 지금 표에 보이는 병합 결과를 화면과 같은 출처별 색상(A=흰색/B=파란색/C=노란색/D=보라색)
    /// 그대로 .xlsx로 저장한다. 외부 라이브러리 없이 <see cref="Xlsx"/>로 직접 만든다.
    /// </summary>
    private void ExportToExcel()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show("내보낼 행이 없습니다.", "엑셀 다운로드", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "작업항목 병합 결과 - 엑셀로 저장",
            Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
            FileName = $"작업항목병합결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var sheetRows = new List<IReadOnlyList<Xlsx.Cell>>
            {
                new List<Xlsx.Cell>
                {
                    new("출처", Xlsx.HeaderCenter),
                    new("번호", Xlsx.HeaderLeft),
                    new("작업항목", Xlsx.HeaderLeft),
                    new("작업", Xlsx.HeaderCenter),
                    new("시간/청구", Xlsx.HeaderRight),
                    new("부품금액", Xlsx.HeaderRight),
                    new("공임", Xlsx.HeaderRight),
                },
            };

            foreach (var merged in _rows)
            {
                var row = merged.Row;
                string fill = ArgbFor(merged.Origin);
                var left = Xlsx.TintedLeft(fill);
                var center = Xlsx.TintedCenter(fill);
                var general = Xlsx.TintedRightGeneral(fill);
                var money = Xlsx.TintedRightMoney(fill);

                sheetRows.Add(new List<Xlsx.Cell>
                {
                    new(merged.Origin.ToString(), center),
                    new(row.No, left),
                    new(row.Item, left),
                    new(row.Work, center),
                    new(row.TimeClaim, general),
                    new(row.PartAmount, money),
                    new(row.Labor, money),
                });
            }

            Xlsx.Save(dialog.FileName, "작업항목병합", new[] { 6, 8, 34, 10, 10, 12, 12 }, sheetRows);

            AppStore.Log($"[SHEET] merge excel export -> {dialog.FileName} rows={sheetRows.Count - 1}");
            ShowFeedback($"엑셀로 저장됨: {Path.GetFileName(dialog.FileName)}");

            if (MessageBox.Show("엑셀 파일로 저장했습니다. 지금 열까요?", "저장 완료",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            AppStore.Log("[SHEET] merge excel export failed", ex);
            MessageBox.Show($"엑셀 저장에 실패했습니다.\n\n{ex.Message}", "저장 실패",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
