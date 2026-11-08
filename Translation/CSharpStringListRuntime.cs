namespace FireURQ.Porter.Translation;

public static class CSharpStringListRuntime
{
    public const string Source = """
using System;
using System.Collections.Generic;

namespace FireURQ.Runtime;

// Non-owning string/object pairs with sorted insertion and duplicate policies.
public delegate int TStringListSortCompare(
    TStringList list, int index1, int index2);

public sealed class TStringsItems
{
    private readonly TStrings owner;
    internal TStringsItems(TStrings owner) => this.owner = owner;
    public string this[int index]
    {
        get => owner[index];
        set => owner[index] = value;
    }
}

public sealed class TStringsObjects
{
    private readonly TStrings owner;
    internal TStringsObjects(TStrings owner) => this.owner = owner;
    public TObject? this[int index]
    {
        get => owner.GetObject(index);
        set => owner.SetObject(index, value);
    }
}

public abstract class TStrings : TObject
{
    protected TStrings()
    {
        Strings = new TStringsItems(this);
        Objects = new TStringsObjects(this);
    }

    public abstract int Count { get; }
    public abstract string this[int index] { get; set; }
    public TStringsItems Strings { get; }
    public TStringsObjects Objects { get; }
    public abstract TObject? GetObject(int index);
    public abstract void SetObject(int index, TObject? value);
    public abstract int Add(string value);
    public abstract void Insert(int index, string value);
    public abstract void Delete(int index);
    public abstract void Clear();
    public abstract int IndexOf(string value);

    public virtual int AddObject(string value, TObject? associated)
    {
        int index = Add(value);
        SetObject(index, associated);
        return index;
    }

    public virtual void InsertObject(int index, string value, TObject? associated)
    {
        Insert(index, value);
        SetObject(index, associated);
    }

    public virtual int IndexOfObject(TObject? value)
    {
        for (int index = 0; index < Count; index++)
        {
            if (ReferenceEquals(GetObject(index), value))
                return index;
        }
        return -1;
    }
}

public class TStringList : TStrings
{
    private sealed class Entry
    {
        internal string Text;
        internal TObject? Associated;
        internal Entry(string text) => Text = text;
    }

    private readonly List<Entry> entries = new();
    public static TStringList Create() => new();
    private bool caseSensitive;
    private bool sorted;

    public bool CaseSensitive
    {
        get => caseSensitive;
        set
        {
            if (caseSensitive == value)
                return;
            caseSensitive = value;
            if (Sorted)
                Sort();
        }
    }

    public bool Sorted
    {
        get => sorted;
        set
        {
            if (sorted == value)
                return;
            if (value)
                Sort();
            sorted = value;
        }
    }

    public TDuplicates Duplicates { get; set; } = TDuplicates.dupIgnore;

    private StringComparer Comparer => CaseSensitive
        ? StringComparer.CurrentCulture
        : StringComparer.CurrentCultureIgnoreCase;

    public override int Count => entries.Count;

    public override string this[int index]
    {
        get => entries[index].Text;
        set
        {
            var replacement = new Entry(value)
            {
                Associated = entries[index].Associated
            };
            IList<Entry> writable = Sorted ? entries.AsReadOnly() : entries;
            writable[index] = replacement;
        }
    }

    public override TObject? GetObject(int index) => entries[index].Associated;
    public override void SetObject(int index, TObject? value) =>
        entries[index].Associated = value;

    public override int Add(string value) => AddEntry(value, null);

    public override int AddObject(string value, TObject? associated) =>
        AddEntry(value, associated);

    private int AddEntry(string value, TObject? associated)
    {
        var entry = new Entry(value) { Associated = associated };
        if (!Sorted)
        {
            int appended = entries.Count;
            entries.Add(entry);
            return appended;
        }

        StringComparer comparer = Comparer;
        int low = 0;
        int high = entries.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (comparer.Compare(entries[middle].Text, value) < 0)
                low = middle + 1;
            else
                high = middle;
        }

        if (low < entries.Count &&
            comparer.Equals(entries[low].Text, value) &&
            Duplicates == TDuplicates.dupIgnore)
            return low;

        if (Duplicates == TDuplicates.dupError)
        {
            var uniqueEntries = new Dictionary<string, Entry>(comparer);
            foreach (Entry existing in entries)
                uniqueEntries.TryAdd(existing.Text, existing);
            uniqueEntries.Add(value, entry);
        }

        entries.Insert(low, entry);
        return low;
    }

    public override void Insert(int index, string value)
    {
        IList<Entry> writable = Sorted ? entries.AsReadOnly() : entries;
        writable.Insert(index, new Entry(value));
    }

    public override void Delete(int index) => entries.RemoveAt(index);

    public override void Clear() => entries.Clear();

    public override int IndexOf(string value)
    {
        StringComparer comparer = Comparer;
        for (int index = 0; index < entries.Count; index++)
        {
            if (comparer.Equals(entries[index].Text, value))
                return index;
        }
        return -1;
    }

    public virtual void Exchange(int first, int second)
    {
        Entry firstValue = entries[first];
        Entry secondValue = entries[second];
        entries[first] = secondValue;
        entries[second] = firstValue;
    }

    public virtual void Sort() =>
        CustomSort((list, first, second) =>
            list.Comparer.Compare(list[first], list[second]));

    public virtual void CustomSort(TStringListSortCompare compare)
    {
        if (entries.Count > 1)
            QuickSort(0, entries.Count - 1, compare);
    }

    private void QuickSort(int left, int right, TStringListSortCompare compare)
    {
        int first = left;
        int second = right;
        int pivot = left + (right - left) / 2;

        do
        {
            while (first <= right && compare(this, first, pivot) < 0)
                first++;
            while (second >= left && compare(this, second, pivot) > 0)
                second--;

            if (first <= second)
            {
                Entry temporary = entries[first];
                entries[first] = entries[second];
                entries[second] = temporary;

                if (pivot == first)
                    pivot = second;
                else if (pivot == second)
                    pivot = first;

                first++;
                second--;
            }
        }
        while (first <= second);

        if (left < second)
            QuickSort(left, second, compare);
        if (first < right)
            QuickSort(first, right, compare);
    }

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        try
        {
            // Do not dispatch to a descendant's Clear/Delete during destruction.
            // A descendant may already have freed its associated objects.
            entries.Clear();
        }
        finally
        {
            base.Destroy();
        }
    }
}
""";
}