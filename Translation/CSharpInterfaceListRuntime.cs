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
public interface IJclIntfCollection : IInterface
{
    int Size { get; }
    bool IsEmpty { get; }
    bool Add(IInterface value);
    void Clear();
    IJclIntfIterator First();
}

public interface IJclIntfList : IJclIntfCollection, IInterfaceList
{
}

public interface IJclIntfIterator : IInterface
{
    bool HasNext { get; }
    IInterface Next();
}

public sealed class TJclIntfArrayList : TInterfaceList, IJclIntfList
{
    public new static TJclIntfArrayList Create() => new();

    public int Size => Count;
    public bool IsEmpty => Count == 0;
    bool IJclIntfCollection.Add(IInterface value)
    {
        Add(value);
        return true;
    }

    public IJclIntfIterator First() => new Iterator(this);

    private sealed class Iterator(TJclIntfArrayList list) : TInterfacedObject, IJclIntfIterator
    {
        private int index;

        public bool HasNext => index < list.Count;
        public IInterface Next()
        {
            if (!HasNext)
                throw new InvalidOperationException("JCL iterator has no next element.");
            return list[index++]!;
        }
    }
}

public delegate int JclIntfCompare(in IInterface left, in IInterface right);

public static class DelphiJclIntfLists
{
    public static void QuickSort(IJclIntfList list, int left, int right, JclIntfCompare compare)
    {
        // Keep the JCL pivot and swap order, including swaps of equal keys.
        while (left < right)
        {
            int first = left;
            int last = right;
            int pivotIndex = left + ((right - left) >> 1);
            while (first <= last)
            {
                IInterface pivot = list[pivotIndex]!;
                while (compare(list[first]!, pivot) < 0)
                    first++;
                while (compare(list[last]!, pivot) > 0)
                    last--;
                if (first > last)
                    break;

                if (first != last)
                    list.Exchange(first, last);
                if (pivotIndex == first)
                    pivotIndex = last;
                else if (pivotIndex == last)
                    pivotIndex = first;

                first++;
                last--;
            }

            if (left < last)
                QuickSort(list, left, last, compare);
            left = first;
        }
    }
}
""";
}