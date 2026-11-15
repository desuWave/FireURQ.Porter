using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

public class TClipboard : TObject
{
    private const uint UnicodeText = 13;
    private const uint MoveableMemory = 0x0002;

    protected static void SetUnicodeText(string text)
    {
        var chars = new char[checked(text.Length + 1)];
        text.CopyTo(0, chars, 0, text.Length);
        IntPtr window = GetActiveWindow();
        if (window == IntPtr.Zero)
            throw new InvalidOperationException("The clipboard needs an active window on the current thread.");

        IntPtr memory = GlobalAlloc(MoveableMemory, (UIntPtr)checked((uint)chars.Length * sizeof(char)));
        if (memory == IntPtr.Zero)
            throw new Win32Exception();

        try
        {
            IntPtr buffer = GlobalLock(memory);
            if (buffer == IntPtr.Zero)
                throw new Win32Exception();

            try { Marshal.Copy(chars, 0, buffer, chars.Length); }
            finally { GlobalUnlock(memory); }

            if (!OpenClipboard(window))
                throw new Win32Exception();

            try
            {
                if (!EmptyClipboard() || SetClipboardData(UnicodeText, memory) == IntPtr.Zero)
                    throw new Win32Exception();

                // Windows owns the allocation after SetClipboardData succeeds.
                memory = IntPtr.Zero;
            }
            finally { CloseClipboard(); }
        }
        finally
        {
            if (memory != IntPtr.Zero)
                GlobalFree(memory);
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
