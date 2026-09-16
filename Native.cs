using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MyWorkQuickLauncher;

/// <summary>Windows 셸(Explorer)과 동일한 아이콘을 얻기 위한 얇은 상호운용 계층.</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    private const uint WM_CLOSE = 0x0010;

    /// <summary>보이는 최상위 창 중 제목에 <paramref name="titleContains"/>가 포함된 것을 모두 찾는다.</summary>
    internal static List<IntPtr> FindVisibleWindowsByTitle(string titleContains)
    {
        var found = new List<IntPtr>();
        if (string.IsNullOrWhiteSpace(titleContains)) return found;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(titleContains, StringComparison.OrdinalIgnoreCase))
                    found.Add(hWnd);
            }
            catch
            {
                // 개별 창 조회 실패는 전체 열거를 멈추지 않는다.
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>창에 닫기(WM_CLOSE)를 보낸다. 강제 종료가 아니라 창 자신의 닫기 처리를 그대로 따른다.</summary>
    internal static void RequestCloseWindow(IntPtr hWnd) => PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    /// <summary>경로(파일/폴더/.lnk/.url/존재하지 않는 경로)에 대해 Explorer가 쓰는 아이콘을 비트맵으로 돌려준다.</summary>
    internal static Bitmap? ShellIcon(string path, bool large = true)
    {
        try
        {
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);

            bool onDisk = File.Exists(path) || Directory.Exists(path);
            uint attr;
            if (!onDisk)
            {
                // 경로가 없어도 확장자만으로 연결 아이콘을 얻는다.
                flags |= SHGFI_USEFILEATTRIBUTES;
                attr = FILE_ATTRIBUTE_NORMAL;
            }
            else
            {
                attr = Directory.Exists(path) ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            }

            var result = SHGetFileInfo(path, attr, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(info.hIcon);
                return new Bitmap(icon.ToBitmap());
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>바로가기 아이콘을 결정한다. 우선순위: 지정 아이콘 파일 → 저장된 IconData → 셸 추출.</summary>
internal static class IconResolver
{
    public static Image Placeholder(ShortcutKind kind)
        => (kind == ShortcutKind.Web ? SystemIcons.Information : SystemIcons.Application).ToBitmap();

    public static Image? FromBase64(string base64)
    {
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(base64));
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    public static string ToBase64(Image? image)
    {
        try
        {
            if (image == null) return "";
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
        catch
        {
            return "";
        }
    }

    public static Image? Resolve(ShortcutItem item)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(item.Icon) && File.Exists(item.Icon))
            {
                if (string.Equals(Path.GetExtension(item.Icon), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var ico = new Icon(item.Icon);
                    return ico.ToBitmap();
                }

                using var stream = new MemoryStream(File.ReadAllBytes(item.Icon));
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
        }
        catch
        {
            // 지정 아이콘이 깨졌으면 다음 후보로 넘어간다.
        }

        if (!string.IsNullOrWhiteSpace(item.IconData))
        {
            var fromData = FromBase64(item.IconData);
            if (fromData != null) return fromData;
        }

        return Extract(item.Path, item.Kind);
    }

    /// <summary>경로에서 아이콘을 직접 추출한다(느릴 수 있으므로 최초 1회만 호출하고 IconData로 캐시).</summary>
    public static Image? Extract(string path, ShortcutKind kind)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var shell = Native.ShellIcon(path, large: true);
                if (shell != null) return shell;

                if (File.Exists(path))
                {
                    var associated = Icon.ExtractAssociatedIcon(path);
                    if (associated != null) return associated.ToBitmap();
                }
            }
        }
        catch
        {
            // 무시하고 기본 아이콘 사용
        }

        return Placeholder(kind);
    }
}
