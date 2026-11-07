using System.Threading;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

LifetimeObject.Free(null);
Check("Free accepts a null object", true);

var ordinary = new ChildObject();
LifetimeObject.Free(ordinary);
Check("Destructor chain preserves execution order",
    string.Join(",", ordinary.Events) ==
        "child-enter,parent-enter,parent-exit,child-exit,release");
Check("Inherited destructors release storage once",
    ordinary.ReleaseCount == 1);
Check("Storage release follows the outer destructor",
    ordinary.Events.Last() == "release");

var direct = new ChildObject();
direct.Destroy();
Check("Direct destructor call also releases storage once",
    direct.ReleaseCount == 1);

var counted = new CountedObject();
counted.AddReference();
Check("Reference count begins with two owners", counted.ReferenceCount == 2);
LifetimeObject.Free(counted);
Check("First release preserves the remaining owner",
    counted.ReferenceCount == 1 &&
    counted.CleanupCount == 0 &&
    counted.ReleaseCount == 0);
LifetimeObject.Free(counted);
Check("Last release performs cleanup once",
    counted.ReferenceCount == 0 && counted.CleanupCount == 1);
Check("Last release performs storage release once",
    counted.ReleaseCount == 1);
Check("Reference count reaches zero before storage release",
    counted.CountAtStorageRelease == 0);

Console.WriteLine($"Object-lifetime checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;

// This is an isolated lifecycle model, not generated runtime support.
class LifetimeObject
{
    private int destructorDepth;
    public int ReleaseCount { get; private set; }

    public static void Free(LifetimeObject? value)
    {
        if (value is not null)
            value.Destroy();
    }

    protected void RunDestructor(Action body)
    {
        destructorDepth++;
        try
        {
            body();
        }
        finally
        {
            destructorDepth--;
            if (destructorDepth == 0)
                FreeInstance();
        }
    }

    public virtual void Destroy()
    {
        RunDestructor(() => { });
    }

    protected virtual void FreeInstance()
    {
        ReleaseCount++;
    }
}

class ParentObject : LifetimeObject
{
    public List<string> Events { get; } = new();

    public override void Destroy()
    {
        RunDestructor(() =>
        {
            Events.Add("parent-enter");
            base.Destroy();
            Events.Add("parent-exit");
        });
    }

    protected override void FreeInstance()
    {
        Events.Add("release");
        base.FreeInstance();
    }
}

sealed class ChildObject : ParentObject
{
    public override void Destroy()
    {
        RunDestructor(() =>
        {
            Events.Add("child-enter");
            base.Destroy();
            Events.Add("child-exit");
        });
    }
}

sealed class CountedObject : LifetimeObject
{
    private int references = 1;
    public int ReferenceCount => references;
    public int CleanupCount { get; private set; }
    public int CountAtStorageRelease { get; private set; } = -1;

    public void AddReference()
    {
        Interlocked.Increment(ref references);
    }

    public override void Destroy()
    {
        RunDestructor(() =>
        {
            if (Interlocked.Decrement(ref references) == 0)
            {
                references++;
                try
                {
                    try
                    {
                        CleanupCount++;
                    }
                    finally
                    {
                        base.Destroy();
                    }
                }
                finally
                {
                    references--;
                }
            }
        });
    }

    protected override void FreeInstance()
    {
        if (references == 0)
        {
            CountAtStorageRelease = references;
            base.FreeInstance();
        }
    }
}