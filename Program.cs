using System.Runtime.InteropServices;

namespace MyWorkQuickLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 마우스 커서 아래의 스크롤 영역으로 휠 이벤트를 넘겨준다(포커스 없어도 스크롤).
        Application.AddMessageFilter(new WheelRedirector());

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            AppStore.Log("[FATAL] unhandled", ex);
            MessageBox.Show(
                $"예기치 못한 오류로 종료되었습니다.\n\n{ex.Message}\n\n로그: {AppStore.LogFile}",
                "MY WORK QUICK LAUNCHER",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// WinForms는 휠 메시지를 포커스 컨트롤로만 보낸다. 이 필터는 커서 위치의
/// 스크롤 가능한 컨트롤을 찾아 휠 메시지를 다시 전달한다.
/// </summary>
internal sealed class WheelRedirector : IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL) return false;

        int x = unchecked((short)(long)m.LParam);
        int y = unchecked((short)((long)m.LParam >> 16));

        var control = Control.FromChildHandle(WindowFromPoint(new Point(x, y)));
        for (var current = control; current != null; current = current.Parent)
        {
            if (current is ScrollableControl scrollable
                && scrollable.AutoScroll
                && scrollable.VerticalScroll.Visible
                && current.IsHandleCreated)
            {
                SendMessage(current.Handle, WM_MOUSEWHEEL, m.WParam, m.LParam);
                return true;
            }
        }

        return false;
    }
}
