namespace FireURQ.Porter.Translation;

public static class CSharpInterfaceListRuntime
{
    public const string Source = """
using System;
using System.Collections.Generic;

namespace FireURQ.Runtime;

// Initial subset. Indexer reads are borrowed references;
// translated temporary-variable lifetime management is not implemented yet.
public interface IInterfaceList : IInterface
{
    int Count { get; }
    IInterface? this[int index] { get; set; }
    TInterfaceListItems Items { get; }
    int Add(IInterface? value);
    void Insert(int index, IInterface? value);
    void Delete(int index);
    int Remove(IInterface? value);
    int IndexOf(IInterface? value);
    void Exchange(int first, int second);
    void Clear();
}

public sealed class TInterfaceListItems
{
    private readonly IInterfaceList owner;
    internal TInterfaceListItems(IInterfaceList owner) => this.owner = owner;

    public IInterface? this[int index]
    {
        get => owner[index];
        set => owner[index] = value;
    }
}

public class TInterfaceList : TInterfacedObject, IInterfaceList
{
    private readonly object gate = new();
    private readonly List<IInterface?> entries = new();

    public TInterfaceList() => Items = new TInterfaceListItems(this);
    public static TInterfaceList Create() => new();

    public TInterfaceListItems Items { get; }

    public int Count
    {
        get
        {
            lock (gate)
                return entries.Count;
        }
    }

    public IInterface? this[int index]
    {
        get
        {
            lock (gate)
                return entries[index];
        }
        set
        {
            IInterface? previous;
            lock (gate)
            {
                previous = entries[index];
                if (ReferenceEquals(previous, value))
                    return;
                value?._AddRef();
                entries[index] = value;
            }
            previous?._Release();
        }
    }

    public int Add(IInterface? value)
    {
        lock (gate)
        {
            value?._AddRef();
            bool stored = false;
            try
            {
                int index = entries.Count;
                entries.Add(value);
                stored = true;
                return index;
            }
            finally
            {
                if (!stored)
                    value?._Release();
            }
        }
    }

    public void Insert(int index, IInterface? value)
    {
        lock (gate)
        {
            value?._AddRef();
            bool stored = false;
            try
            {
                entries.Insert(index, value);
                stored = true;
            }
            finally
            {
                if (!stored)
                    value?._Release();
            }
        }
    }

    private int FindIndex(IInterface? value)
    {
        for (int index = 0; index < entries.Count; index++)
        {
            if (ReferenceEquals(entries[index], value))
                return index;
        }
        return -1;
    }

    public int IndexOf(IInterface? value)
    {
        lock (gate)
            return FindIndex(value);
    }

    public void Delete(int index)
    {
        IInterface? previous;
        lock (gate)
        {
            previous = entries[index];
            entries.RemoveAt(index);
        }
        previous?._Release();
    }

    public int Remove(IInterface? value)
    {
        int index;
        IInterface? previous;
        lock (gate)
        {
            index = FindIndex(value);
            if (index < 0)
                return -1;
            previous = entries[index];
            entries.RemoveAt(index);
        }
        previous?._Release();
        return index;
    }

    public void Exchange(int first, int second)
    {
        lock (gate)
        {
            IInterface? firstValue = entries[first];
            IInterface? secondValue = entries[second];
            entries[first] = secondValue;
            entries[second] = firstValue;
        }
    }

    public void Clear()
    {
        IInterface?[] previous;
        lock (gate)
        {
            previous = entries.ToArray();
            entries.Clear();
        }
        for (int index = previous.Length - 1; index >= 0; index--)
            previous[index]?._Release();
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
""";
}