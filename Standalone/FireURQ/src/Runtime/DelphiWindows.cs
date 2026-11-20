global using static FireURQ.Runtime.DelphiWindows;
global using TKeyboardState = System.Byte[];

using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

internal static class DelphiWindows
{
    public const uint WM_CREATE = 0x0001;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_ACTIVATEAPP = 0x001C;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_SETICON = 0x0080;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_SYSCOMMAND = 0x0112;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_RBUTTONDBLCLK = 0x0206;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MBUTTONDBLCLK = 0x0209;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_QUIT = 0x0012;

    public const uint MM_MCINOTIFY = 0x03B9;
    public const nuint MCI_NOTIFY_SUCCESSFUL = 0x0001;
    public const nuint SC_CLOSE = 0xF060;

    public const nuint VK_F4 = 0x73;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_CAPITAL = 0x14;
    public const int VK_NUMLOCK = 0x90;
    public const int VK_SCROLL = 0x91;

    public const int HTCLIENT = 1;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public static readonly IntPtr IDC_ARROW = new(32512);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static extern IntPtr DefWindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", EntryPoint = "SetFocus")]
    public static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "SetCursor")]
    public static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    public static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out TRect rectangle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLongNative(IntPtr window, int index);

    public static int GetWindowLong(IntPtr window, int index) =>
        GetWindowLongNative(window, index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLongNative(IntPtr window, int index, int value);

    public static int SetWindowLong(IntPtr window, int index, int value) =>
        SetWindowLongNative(window, index, value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetKeyboardState([Out] byte[] state);

    [DllImport("user32.dll")]
    public static extern IntPtr SetCapture(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReleaseCapture();

    [DllImport("winmm.dll", EntryPoint = "timeGetTime")]
    public static extern uint timeGetTime();

    [DllImport("winmm.dll", EntryPoint = "mciSendStringW")]
    private static extern int MciSendStringNative(
        IntPtr command, IntPtr returnText, uint returnLength, IntPtr callback);

    public static int mciSendString(
        IntPtr command, object? returnText, int returnLength, int callback) =>
        MciSendStringNative(command, IntPtr.Zero, unchecked((uint)returnLength),
            new IntPtr(callback));

    [DllImport("winmm.dll", EntryPoint = "midiOutSetVolume")]
    public static extern int midiOutSetVolume(IntPtr device, uint volume);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    private static extern bool PlaySoundNative(IntPtr sound, IntPtr module, uint flags);

    public static bool PlaySound(IntPtr sound, object? module, int flags) =>
        PlaySoundNative(sound, IntPtr.Zero, unchecked((uint)flags));
}
