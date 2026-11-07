using System.Runtime.InteropServices;

internal sealed class Sprite
{
    public int Z;
}

internal static class Program
{
    private static int Main()
    {
        int total = 0;
        int failed = 0;
        int allocated = 0;
        int released = 0;

        void Check(string name, bool passed)
        {
            total++;
            if (!passed) failed++;
            Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
        }

        T Resolve<T>(IntPtr value) where T : class =>
            (T)GCHandle.FromIntPtr(value).Target!;

        int WithPointers(
            Sprite left, Sprite right, Func<IntPtr, IntPtr, int> compare)
        {
            GCHandle first = default;
            GCHandle second = default;
            try
            {
                first = GCHandle.Alloc(left);
                allocated++;
                second = GCHandle.Alloc(right);
                allocated++;
                return compare(
                    GCHandle.ToIntPtr(first), GCHandle.ToIntPtr(second));
            }
            finally
            {
                if (second.IsAllocated)
                {
                    second.Free();
                    released++;
                }
                if (first.IsAllocated)
                {
                    first.Free();
                    released++;
                }
            }
        }

        int Compare(IntPtr item1, IntPtr item2)
        {
            Sprite first = Resolve<Sprite>(item1);
            Sprite second = Resolve<Sprite>(item2);
            if (first.Z > second.Z) return -1;
            if (first.Z < second.Z) return 1;
            return 0;
        }

        var low = new Sprite { Z = 1 };
        var high = new Sprite { Z = 9 };
        Check("Comparator preserves descending order",
            WithPointers(high, low, Compare) == -1 &&
            WithPointers(low, high, Compare) == 1);
        Check("Comparator preserves equality",
            WithPointers(low, low, Compare) == 0);

        bool identity = false;
        WithPointers(low, high, (first, second) =>
        {
            identity = ReferenceEquals(Resolve<Sprite>(first), low) &&
                ReferenceEquals(Resolve<Sprite>(second), high);
            return 0;
        });
        Check("Pointers resolve to original objects", identity);

        bool survivedCollection = false;
        WithPointers(low, high, (first, second) =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            survivedCollection = Resolve<Sprite>(first).Z == 1 &&
                Resolve<Sprite>(second).Z == 9;
            return 0;
        });
        Check("Object references survive garbage collection", survivedCollection);

        var items = new List<Sprite>
        {
            new() { Z = 2 }, new() { Z = 8 }, new() { Z = -3 },
            new() { Z = 8 }, new() { Z = 0 }
        };
        items.Sort((first, second) => WithPointers(first, second, Compare));
        Check("Collection sorting uses expected Z order",
            items.Select(item => item.Z).SequenceEqual(new[] { 8, 8, 2, 0, -3 }));

        Check("Every comparison releases its handles", allocated == released);

        bool castRejected = false;
        try
        {
            WithPointers(low, high, (first, second) =>
            {
                _ = Resolve<string>(first);
                return 0;
            });
        }
        catch (InvalidCastException)
        {
            castRejected = true;
        }
        Check("Mismatched object type is rejected", castRejected);
        Check("Handles are released after comparator failure", allocated == released);

        Console.WriteLine(
            $"Object-pointer checks: {total - failed}/{total} passed.");
        Console.WriteLine($"Handles allocated={allocated}; released={released}");
        return failed == 0 ? 0 : 1;
    }
}