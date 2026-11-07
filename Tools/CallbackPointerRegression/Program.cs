using System.Runtime.InteropServices;

internal static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint StreamProc(
        uint handle, IntPtr buffer, uint length, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint StreamCreate(
        uint frequency, uint channels, uint flags, IntPtr proc, IntPtr user);

    private static readonly IntPtr Dummy = IntPtr.Zero;
    private static readonly IntPtr Push = new IntPtr(-1);

    private static int Main()
    {
        int total = 0;
        int failed = 0;

        void Check(string name, bool passed)
        {
            total++;
            if (!passed) failed++;
            Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
        }

        Console.WriteLine($"Process pointer size: {IntPtr.Size * 8} bits");
        Check("Dummy preserves zero", Dummy == IntPtr.Zero);
        Check("Push preserves signed minus one", Push.ToInt64() == -1);
        Check("Special values remain distinct", Dummy != Push);

        byte[] pushBytes = IntPtr.Size == 8
            ? BitConverter.GetBytes(Push.ToInt64())
            : BitConverter.GetBytes(Push.ToInt32());
        Check("Push fills every pointer byte with FF",
            pushBytes.Length == IntPtr.Size &&
            pushBytes.All(value => value == 0xFF));

        IntPtr received = IntPtr.Zero;
        IntPtr receivedUser = IntPtr.Zero;
        bool callbackInvoked = false;

        StreamCreate nativeShape = (frequency, channels, flags, proc, user) =>
        {
            received = proc;
            receivedUser = user;

            if (proc == Dummy) return 10;
            if (proc == Push) return 20;

            var callback = Marshal.GetDelegateForFunctionPointer<StreamProc>(proc);
            return callback(7, new IntPtr(11), 64, user);
        };

        IntPtr entry = Marshal.GetFunctionPointerForDelegate(nativeShape);
        var api = Marshal.GetDelegateForFunctionPointer<StreamCreate>(entry);
        IntPtr userData = new IntPtr(123);

        Check("Dummy passes through function-pointer boundary",
            api(44100, 2, 0, Dummy, userData) == 10 && received == Dummy);
        Check("Push passes through function-pointer boundary",
            api(44100, 2, 0, Push, userData) == 20 && received == Push);
        Check("User pointer is preserved", receivedUser == userData);

        StreamProc managedCallback = (handle, buffer, length, user) =>
        {
            callbackInvoked =
                handle == 7 &&
                buffer == new IntPtr(11) &&
                length == 64 &&
                user == userData;
            return length;
        };

        IntPtr callbackAddress =
            Marshal.GetFunctionPointerForDelegate(managedCallback);

        Check("Real callback has a distinct function address",
            callbackAddress != Dummy && callbackAddress != Push);
        Check("Real callback is invoked through its pointer",
            api(44100, 2, 0, callbackAddress, userData) == 64 &&
            callbackInvoked);

        GC.KeepAlive(managedCallback);
        GC.KeepAlive(nativeShape);

        Console.WriteLine(
            $"Callback-pointer checks: {total - failed}/{total} passed.");
        return failed == 0 ? 0 : 1;
    }
}