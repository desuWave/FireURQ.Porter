namespace FireURQ.Porter.Translation;

public static class CSharpListRuntime
{
    public const string Source = """
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

public delegate int TListSortCompare(IntPtr item1, IntPtr item2);

public sealed class TListItems
{
    private readonly TList owner;

    internal TListItems(TList owner) => this.owner = owner;

    public IntPtr this[int index]
    {
        get => owner[index];
        set => owner[index] = value;
    }
}

// Pointer entries are borrowed. This class never frees their native storage.
public class TList : TObject
{
    private readonly List<IntPtr> entries = new();

    public TList() => Items = new TListItems(this);

    public static TList Create() => new();

    public int Count => entries.Count;
    public TListItems Items { get; }

    public IntPtr this[int index]
    {
        get => entries[index];
        set
        {
            IntPtr previous = entries[index];
            if (previous == value)
                return;
            entries[index] = value;
            NotifyDeleted(previous);
        }
    }

    public int Add(IntPtr value)
    {
        int index = entries.Count;
        entries.Add(value);
        return index;
    }

    public void Insert(int index, IntPtr value) => entries.Insert(index, value);

    protected virtual void NotifyDeleted(IntPtr value) { }
    protected virtual void NotifyExtracted(IntPtr value) { }

    public void Delete(int index)
    {
        IntPtr previous = entries[index];
        entries.RemoveAt(index);
        NotifyDeleted(previous);
    }

    public int IndexOf(IntPtr value) => entries.IndexOf(value);

    public int Remove(IntPtr value)
    {
        int index = IndexOf(value);
        if (index >= 0)
            Delete(index);
        return index;
    }

    public IntPtr Extract(IntPtr value)
    {
        int index = IndexOf(value);
        if (index < 0)
            return IntPtr.Zero;
        entries.RemoveAt(index);
        NotifyExtracted(value);
        return value;
    }

    public void Exchange(int first, int second)
    {
        IntPtr firstValue = entries[first];
        IntPtr secondValue = entries[second];
        entries[first] = secondValue;
        entries[second] = firstValue;
    }

    public void Move(int currentIndex, int newIndex)
    {
        IntPtr value = entries[currentIndex];
        _ = entries[newIndex];
        if (currentIndex == newIndex)
            return;
        entries.RemoveAt(currentIndex);
        entries.Insert(newIndex, value);
    }

    public void Pack() => entries.RemoveAll(value => value == IntPtr.Zero);

    public void Sort(TListSortCompare compare) =>
        entries.Sort((left, right) => compare(left, right));

    public void Clear()
    {
        while (entries.Count > 0)
            Delete(entries.Count - 1);
    }

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        try
        {
            Clear();
        }
        finally
        {
            base.Destroy();
        }
    }
}

public sealed class TObjectListItems
{
    private readonly TObjectList owner;

    internal TObjectListItems(TObjectList owner) => this.owner = owner;

    public TObject? this[int index]
    {
        get => owner[index];
        set => owner[index] = value;
    }
}

public class TObjectList : TList
{
    private readonly Dictionary<IntPtr, GCHandle> handles = new();

    public TObjectList(bool ownsObjects = true)
    {
        OwnsObjects = ownsObjects;
        Items = new TObjectListItems(this);
    }

    public new static TObjectList Create() => new();
    public static TObjectList Create(bool ownsObjects) => new(ownsObjects);

    public bool OwnsObjects { get; set; }
    public new TObjectListItems Items { get; }

    private IntPtr Retain(TObject? value)
    {
        if (value is null)
            return IntPtr.Zero;

        GCHandle handle = GCHandle.Alloc(value);
        IntPtr token = GCHandle.ToIntPtr(handle);
        bool retained = false;
        try
        {
            handles.Add(token, handle);
            retained = true;
            return token;
        }
        finally
        {
            if (!retained)
                handle.Free();
        }
    }

    private TObject? Resolve(IntPtr token) =>
        token == IntPtr.Zero ? null : (TObject?)handles[token].Target;

    private void Release(IntPtr token)
    {
        if (handles.Remove(token, out GCHandle handle))
            handle.Free();
    }

    private void ReleaseIfNotStored(IntPtr token)
    {
        if (base.IndexOf(token) < 0)
            Release(token);
    }

    protected override void NotifyDeleted(IntPtr token)
    {
        TObject? value = Resolve(token);
        Release(token);
        if (OwnsObjects)
            TObject.Free(value);
    }

    protected override void NotifyExtracted(IntPtr token) => Release(token);

    public new TObject? this[int index]
    {
        get => Resolve(base[index]);
        set
        {
            if (ReferenceEquals(this[index], value))
                return;

            IntPtr token = Retain(value);
            try
            {
                base[index] = token;
            }
            finally
            {
                ReleaseIfNotStored(token);
            }
        }
    }

    public int Add(TObject? value)
    {
        IntPtr token = Retain(value);
        try
        {
            return base.Add(token);
        }
        finally
        {
            ReleaseIfNotStored(token);
        }
    }

    public void Insert(int index, TObject? value)
    {
        IntPtr token = Retain(value);
        try
        {
            base.Insert(index, token);
        }
        finally
        {
            ReleaseIfNotStored(token);
        }
    }

    public int IndexOf(TObject? value)
    {
        for (int index = 0; index < Count; index++)
        {
            if (ReferenceEquals(this[index], value))
                return index;
        }
        return -1;
    }

    public int Remove(TObject? value)
    {
        int index = IndexOf(value);
        if (index >= 0)
            Delete(index);
        return index;
    }

    public TObject? Extract(TObject? value)
    {
        int index = IndexOf(value);
        if (index < 0)
            return null;
        IntPtr token = base[index];
        base.Extract(token);
        return value;
    }
}
""";
}