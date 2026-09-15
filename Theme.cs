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
