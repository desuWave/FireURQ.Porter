using System.ComponentModel;
using System.Runtime.InteropServices;
using FireURQ.Runtime;

internal static class WindowLifecycle
{
    private const uint ProbeMessage = 0x8001;
    private sealed class Receiver
    {
        public IntPtr Dispatch(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam) =>
            message == ProbeMessage ? lParam : DelphiWindows.DefWindowProc(window, message, wParam, lParam);
    }

    private static void Register(string name, IntPtr module)
    {
        var descriptor = new TWndClass { hInstance = module, lpszClassName = name, lpfnWndProc = new Receiver().Dispatch };
        if (DelphiWindows.RegisterClass(descriptor) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassW");
    }

    public static void Check()
    {
        string name = "FireURQRegression_" + Guid.NewGuid().ToString("N");
        IntPtr instance = DelphiWindows.MainInstance;
        Register(name, instance);
        try
        {
            IntPtr window = DelphiWindows.CreateWindowEx(0, name, "Фурка", 0, 0, 0, 40, 40, 0, 0, instance, 0);
            if (window == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW");
            try
            {
                if (DelphiWindows.UnregisterClass(name, instance))
                    throw new InvalidOperationException("Unregistered a class with a live window.");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                IntPtr payload = new(-12345);
                if (DelphiWindows.SendMessage(window, ProbeMessage, 0, payload) != payload)
                    throw new InvalidOperationException("The registered callback lost the message payload.");
            }
            finally
            {
                if (!DelphiWindows.DestroyWindow(window))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "DestroyWindow");
            }
        }
        finally
        {
            if (!DelphiWindows.UnregisterClass(name, instance))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UnregisterClassW");
        }
        Console.WriteLine("Win32 window creation, callback after GC, destruction and unregistration passed.");
    }
}
