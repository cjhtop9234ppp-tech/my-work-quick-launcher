namespace MyWorkQuickLauncher;

public sealed partial class MainForm
{
    // ---- 작업항목 비교분석 탭 ----
    private TextBox _leftSheetBox = null!;   // A시트
    private TextBox _rightSheetBox = null!;  // B시트
    private TextBox _compareCBox = null!;    // C시트 · 7. 누락항목 통계보기(전체)
    private TextBox _compareDBox = null!;    // D시트 · 8. 누락항목 통계보기(공업사)
    private TextBox _compareEBox = null!;    // E시트 · 9. 누락항목 통계보기(특정업체)
    private TextBox _compareFBox = null!;    // F시트 · 10. 누락항목 통계보기(제작사별 지정업체)
    private Button _analyzeButton = null!;
    private Label _sheetStatus = null!;

    // ---- 작업항목 병합하기 탭 ----
    private TextBox _mergeSheetABox = null!;
    private TextBox _mergeSheetBBox = null!;
    private TextBox _mergeSheetCBox = null!;
    private TextBox _mergeSheetDBox = null!;
    private Button _mergeButton = null!;
    private Label _mergeStatus = null!;

    /// <summary>"2. 시트 분석 TOOL" 제목 옆에 붙는 탭. 0 = 작업항목 비교분석(기본), 1 = 작업항목 병합하기.</summary>
    private int _sheetToolTab;

    /// <summary>
    /// "2. 시트 분석 TOOL" 섹션. 제목 옆에 탭 2개(비교분석/병합하기)를 두어 그 아래 카드
    /// 내용을 전환한다(다른 섹션과 달리 SectionShell을 쓰지 않고 직접 제목+탭 줄을 구성한다).
    /// </summary>
    private Control BuildSheetAnalysisSection()
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
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Bg,
            Margin = new Padding(0, 0, 0, 4),
        };

        headRow.Controls.Add(new Label
        {
            Text = "2. 시트 분석 TOOL",
            Font = Theme.Font11B,
            ForeColor = Theme.Text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 6, 16, 4),
        });
        headRow.Controls.Add(BuildToolTabButton("작업항목 비교분석", 0));
        headRow.Controls.Add(BuildToolTabButton("작업항목 병합하기", 1));

        var resetUrlsButton = Theme.ActionButton("URL 입력 초기화");
        resetUrlsButton.Margin = new Padding(12, 4, 0, 0);
        resetUrlsButton.Click += (_, _) => ClearActiveTabUrls();
        headRow.Controls.Add(resetUrlsButton);

        shell.Controls.Add(headRow, 0, 0);

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var body = _sheetToolTab == 1 ? BuildSheetMergeTab() : BuildSheetCompareTab();
        body.Dock = DockStyle.Fill;
        body.Margin = new Padding(0);
        card.Controls.Add(body, 0, 0);

        shell.Controls.Add(card, 0, 1);

        return shell;
    }

    private Button BuildToolTabButton(string text, int index)
    {
        bool active = _sheetToolTab == index;
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            Font = active ? Theme.Font9B : Theme.Font9,
            BackColor = active ? Theme.Accent : Theme.Card,
            ForeColor = active ? Color.White : Theme.Muted,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 4, 6, 0),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = active ? 0 : 1;
        button.FlatAppearance.BorderColor = Theme.Border;
        button.Click += (_, _) =>
        {
            if (_sheetToolTab == index) return;
            _sheetToolTab = index;
            RebuildStack();
        };
        return button;
    }

    /// <summary>
    /// "URL 입력 초기화" 버튼: 지금 선택된 탭의 URL 입력칸만 비운다(다른 탭 값은 건드리지 않는다).
    /// 두 탭은 매 렌더링마다 한쪽 컨트롤만 새로 만들어지므로, 비활성 탭의 필드는 건드리지 않는다.
    /// </summary>
    private void ClearActiveTabUrls()
    {
        if (_sheetToolTab == 0)
        {
            _leftSheetBox.Clear();
            _rightSheetBox.Clear();
            _compareCBox.Clear();
            _compareDBox.Clear();
            _compareEBox.Clear();
            _compareFBox.Clear();
            _data.Settings.LeftSheetUrl = "";
            _data.Settings.RightSheetUrl = "";
            _data.Settings.CompareSheetCUrl = "";
            _data.Settings.CompareSheetDUrl = "";
            _data.Settings.CompareSheetEUrl = "";
            _data.Settings.CompareSheetFUrl = "";
        }
        else
        {
            _mergeSheetABox.Clear();
            _mergeSheetBBox.Clear();
            _mergeSheetCBox.Clear();
            _mergeSheetDBox.Clear();
            _data.Settings.MergeSheetAUrl = "";
            _data.Settings.MergeSheetBUrl = "";
            _data.Settings.MergeSheetCUrl = "";
            _data.Settings.MergeSheetDUrl = "";
        }
        Persist();
    }

    /// <summary>URL 라벨 + 입력칸 한 행을 <paramref name="panel"/>의 <paramref name="row"/>번째 줄에 채운다.</summary>
    private static TextBox AddSheetUrlRow(TableLayoutPanel panel, int row, string label, string placeholder, string initialText)
    {
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 6),
        }, 0, row);

        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = Theme.Font9,
            Text = initialText,
            PlaceholderText = placeholder,
            Margin = new Padding(0, 3, 0, 3),
        };
        panel.Controls.Add(box, 1, row);
        return box;
    }

    // ================================================================== 작업항목 비교분석

    /// <summary>
    /// A시트(직접 비교)와 C~F시트(누락항목 통계보기 병합 비교)는 서로 배타적으로 쓰인다 - 한쪽에
    /// 값이 있으면 다른 쪽 입력칸은 비활성화된다(B시트는 항상 입력 가능). 버튼 하나로 두 모드를
    /// 자동 판별해 B시트와 비교한다.
    /// </summary>
    private Control BuildSheetCompareTab()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < 7; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _leftSheetBox = AddSheetUrlRow(
            panel, 0,
            "A시트  ·  공임비교분석 / 사용자·주체·도장 조회 (URL)",
            "http://<사내서버주소>:8180/ant/gongim/popup_detail1.php?eseq=...  (한 개만)",
            _data.Settings.LeftSheetUrl);

        _rightSheetBox = AddSheetUrlRow(
            panel, 1,
            "B시트  ·  업로드 로그 (URL)",
            "http://<사내서버주소>:8180/ant/api/popup_detail.php?eseq=...",
            _data.Settings.RightSheetUrl);

        _compareCBox = AddSheetUrlRow(
            panel, 2,
            "C시트  ·  7. 누락항목 통계보기(전체) (URL)",
            "http://<사내서버주소>:8180/...  (A시트 대신 C~F시트를 병합해 비교할 때 사용)",
            _data.Settings.CompareSheetCUrl);

        _compareDBox = AddSheetUrlRow(
            panel, 3,
            "D시트  ·  8. 누락항목 통계보기(공업사) (URL)",
            "http://<사내서버주소>:8180/...",
            _data.Settings.CompareSheetDUrl);

        _compareEBox = AddSheetUrlRow(
            panel, 4,
            "E시트  ·  9. 누락항목 통계보기(특정업체) (URL)",
            "http://<사내서버주소>:8180/...",
            _data.Settings.CompareSheetEUrl);

        _compareFBox = AddSheetUrlRow(
            panel, 5,
            "F시트  ·  10. 누락항목 통계보기(제작사별 지정업체) (URL)",
            "http://<사내서버주소>:8180/...",
            _data.Settings.CompareSheetFUrl);

        foreach (var box in new[] { _leftSheetBox, _compareCBox, _compareDBox, _compareEBox, _compareFBox })
            box.TextChanged += (_, _) => UpdateCompareSheetExclusivity();
        UpdateCompareSheetExclusivity();

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
            Text = "A+B시트로 직접 비교하거나, B+C(~F)시트로 병합 비교할 수 있습니다(A와 C~F는 동시에 쓸 수 없음). "
                   + "B시트에 없는 작업항목을 노란색으로 보여줍니다.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 0),
        };
        actions.Controls.Add(_sheetStatus);

        panel.Controls.Add(actions, 1, 6);

        return panel;
    }

    /// <summary>
    /// A시트와 C~F시트는 배타적이다. 한쪽에 값이 있으면 다른 쪽 입력칸을 잠근다(B시트는 항상 가능).
    /// 옛 데이터 등으로 양쪽에 값이 동시에 남아 있는 경우에는 A시트를 우선해 잠기지 않게 한다.
    /// </summary>
    private void UpdateCompareSheetExclusivity()
    {
        bool aFilled = _leftSheetBox.Text.Trim().Length > 0;
        var mergeBoxes = new[] { _compareCBox, _compareDBox, _compareEBox, _compareFBox };
        bool mergeFilled = mergeBoxes.Any(b => b.Text.Trim().Length > 0);

        foreach (var box in mergeBoxes)
            box.Enabled = !aFilled;

        _leftSheetBox.Enabled = aFilled || !mergeFilled;
    }

    private async void RunSheetAnalysis()
    {
        string aUrl = _leftSheetBox.Text.Trim();
        string bUrl = _rightSheetBox.Text.Trim();

        var mergeSlots = new (char Label, string Url)[]
        {
            ('C', _compareCBox.Text.Trim()),
            ('D', _compareDBox.Text.Trim()),
            ('E', _compareEBox.Text.Trim()),
            ('F', _compareFBox.Text.Trim()),
        };
        bool useMerge = aUrl.Length == 0 && mergeSlots[0].Url.Length > 0;

        if (!SheetSource.LooksLikeUrl(bUrl))
        {
            MessageBox.Show(
                "B시트의 URL을 http(s):// 형식으로 입력하세요.",
                "URL 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<(char Label, string Url)> leftSources;
        string leftLabel;

        if (useMerge)
        {
            leftSources = mergeSlots.Where(s => s.Url.Length > 0).ToList();
            if (leftSources.Any(s => !SheetSource.LooksLikeUrl(s.Url)))
            {
                MessageBox.Show(
                    "C~F시트의 URL을 http(s):// 형식으로 입력하세요(C시트부터 순서대로 채워야 합니다).",
                    "URL 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            leftLabel = string.Join("+", leftSources.Select(s => s.Label)) + "시트(병합)";
        }
        else
        {
            if (!SheetSource.LooksLikeUrl(aUrl))
            {
                MessageBox.Show(
                    "A시트의 URL을 입력하거나, A시트를 비워두고 C시트부터 채워 병합 비교를 사용하세요.",
                    "URL 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            leftSources = new List<(char, string)> { ('A', aUrl) };
            leftLabel = "A시트";
        }

        _analyzeButton.Enabled = false;
        _sheetStatus.ForeColor = Theme.Muted;
        _sheetStatus.Text = "시트를 불러오는 중...";
        AppStore.Log($"[SHEET] analyze start mode={(useMerge ? "merge" : "direct")}");

        try
        {
            var rightTask = SheetSource.FetchAsync(bUrl);
            var leftTasks = leftSources.Select(s => SheetSource.FetchAsync(s.Url)).ToList();
            await Task.WhenAll(leftTasks.Prepend(rightTask));

            var right = rightTask.Result;
            var left = useMerge
                ? SheetSource.Merge(leftTasks.Select(t => t.Result))
                : leftTasks[0].Result;

            if (left.Count == 0)
            {
                _sheetStatus.ForeColor = Theme.Danger;
                _sheetStatus.Text = $"{leftLabel}에서 작업항목을 찾지 못했습니다. URL을 확인하세요.";
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

            _data.Settings.LeftSheetUrl = aUrl;
            _data.Settings.RightSheetUrl = bUrl;
            _data.Settings.CompareSheetCUrl = mergeSlots[0].Url;
            _data.Settings.CompareSheetDUrl = mergeSlots[1].Url;
            _data.Settings.CompareSheetEUrl = mergeSlots[2].Url;
            _data.Settings.CompareSheetFUrl = mergeSlots[3].Url;
            Persist();

            _sheetStatus.ForeColor = missingKeys.Count > 0 ? Theme.Accent : Theme.Muted;
            _sheetStatus.Text =
                $"{leftLabel} {left.Count}행 · B시트 {right.Count}행 · B시트에 없는 작업항목 {missingKeys.Count}건({missingRows}행)";
            AppStore.Log($"[SHEET] analyze done mode={(useMerge ? "merge" : "direct")} left={left.Count} right={right.Count} missing={missingKeys.Count}");

            using var dialog = new SheetAnalysisDialog(
                left, missingKeys, right.Count, leftLabel, _data.Settings.SheetAnalysisDialogSize, SetClipboardText);
            ShowOwned(dialog);

            _data.Settings.SheetAnalysisDialogSize = dialog.CurrentDialogSize;
            Persist();
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

    // ================================================================== 작업항목 병합하기

    private Control BuildSheetMergeTab()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < 5; i++)
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _mergeSheetABox = AddSheetUrlRow(
            panel, 0,
            "A시트  ·  7. 누락항목 통계보기(전체) (URL)",
            "http://<사내서버주소>:8180/...  (흰색으로 표시됩니다)",
            _data.Settings.MergeSheetAUrl);

        _mergeSheetBBox = AddSheetUrlRow(
            panel, 1,
            "B시트  ·  8. 누락항목 통계보기(공업사) (URL)",
            "http://<사내서버주소>:8180/...  (파란색으로 표시됩니다)",
            _data.Settings.MergeSheetBUrl);

        _mergeSheetCBox = AddSheetUrlRow(
            panel, 2,
            "C시트  ·  9. 누락항목 통계보기(특정업체) (URL)",
            "http://<사내서버주소>:8180/...  (노란색으로 표시됩니다)",
            _data.Settings.MergeSheetCUrl);

        _mergeSheetDBox = AddSheetUrlRow(
            panel, 3,
            "D시트  ·  10. 누락항목 통계보기(제작사별 지정업체) (URL)",
            "http://<사내서버주소>:8180/...  (보라색으로 표시됩니다)",
            _data.Settings.MergeSheetDUrl);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
        };

        _mergeButton = Theme.FlatButton("작업항목 병합하기", Theme.Accent, Color.White);
        _mergeButton.Margin = new Padding(0, 0, 12, 0);
        _mergeButton.Click += (_, _) => RunSheetMerge();
        actions.Controls.Add(_mergeButton);

        _mergeStatus = new Label
        {
            Text = "A~D 중 최소 1개의 URL을 넣고 병합하면, 작업항목 기준 중복 없이 A→B→C→D 순서로 합쳐 보여줍니다.",
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font8,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 0, 0),
        };
        actions.Controls.Add(_mergeStatus);

        panel.Controls.Add(actions, 1, 4);

        return panel;
    }

    private async void RunSheetMerge()
    {
        var slots = new (char Origin, TextBox Box)[]
        {
            ('A', _mergeSheetABox),
            ('B', _mergeSheetBBox),
            ('C', _mergeSheetCBox),
            ('D', _mergeSheetDBox),
        };

        var provided = slots
            .Select(s => (s.Origin, Url: s.Box.Text.Trim()))
            .Where(s => s.Url.Length > 0)
            .ToList();

        if (provided.Count == 0 || provided.Any(s => !SheetSource.LooksLikeUrl(s.Url)))
        {
            MessageBox.Show(
                "병합할 시트의 URL을 http(s):// 형식으로 1개 이상 입력하세요(A~D 중 비워둔 칸은 건너뜁니다).",
                "URL 확인",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _mergeButton.Enabled = false;
        _mergeStatus.ForeColor = Theme.Muted;
        _mergeStatus.Text = "시트를 불러오는 중...";
        AppStore.Log("[SHEET] merge start");

        try
        {
            var fetches = provided
                .Select(s => (s.Origin, Task: SheetSource.FetchAsync(s.Url)))
                .ToList();
            await Task.WhenAll(fetches.Select(f => f.Task));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var merged = new List<MergedSheetRow>();
            foreach (var (origin, task) in fetches)
            {
                foreach (var row in task.Result)
                {
                    string key = SheetSource.ItemKey(row.Item);
                    if (key.Length == 0 || !seen.Add(key)) continue;
                    merged.Add(new MergedSheetRow(row, origin));
                }
            }

            _data.Settings.MergeSheetAUrl = _mergeSheetABox.Text.Trim();
            _data.Settings.MergeSheetBUrl = _mergeSheetBBox.Text.Trim();
            _data.Settings.MergeSheetCUrl = _mergeSheetCBox.Text.Trim();
            _data.Settings.MergeSheetDUrl = _mergeSheetDBox.Text.Trim();
            Persist();

            string breakdown = string.Join(" · ", fetches.Select(f => $"{f.Origin}시트 {f.Task.Result.Count}행"));
            _mergeStatus.ForeColor = Theme.Accent;
            _mergeStatus.Text = $"병합 완료 · 총 {merged.Count}건 ({breakdown})";
            AppStore.Log($"[SHEET] merge done total={merged.Count}");

            using var dialog = new SheetMergeDialog(merged, SetClipboardText);
            ShowOwned(dialog);
        }
        catch (Exception ex)
        {
            AppStore.Log("[SHEET] merge failed", ex);
            _mergeStatus.ForeColor = Theme.Danger;
            _mergeStatus.Text = "병합 실패: " + ex.Message;
            MessageBox.Show(
                $"시트를 불러오지 못했습니다.\n\n{ex.Message}",
                "작업항목 병합 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _mergeButton.Enabled = true;
        }
    }
}
