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
