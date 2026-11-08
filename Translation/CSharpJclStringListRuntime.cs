namespace FireURQ.Porter.Translation;

public static class CSharpJclStringListRuntime
{
    public const string Source = """
using System;
using System.Collections.Generic;

namespace FireURQ.Runtime;

// TObject storage is not implemented yet.
// Interface cells retain managed references, without automatic Delphi refcount calls.
public static class DelphiJclStringLists
{
    public static IJclStringList JclStringList => new TJclStringList();
}



public enum TJclStringListObjectsMode
{
    omNone,
    omObjects,
    omVariants,
    omInterfaces
}

public interface IJclStringList
{
    int Count { get; }
    int LastIndex { get; }
    bool CaseSensitive { get; set; }
    bool Sorted { get; set; }
    TDuplicates Duplicates { get; set; }
    TJclStringListObjectsMode ObjectsMode { get; }
    string this[int index] { get; set; }
    JclIndexedValues<int, string> Strings { get; }
    JclIndexedValues<int, Variant> Variants { get; }
    JclIndexedValues<string, Variant> KeyVariant { get; }
    JclIndexedValues<int, object?> Interfaces { get; }
    JclIndexedValues<int, IJclStringList> Lists { get; }
    int Add(string value);
    int IndexOf(string value);
    IJclStringList Sort();
    IJclStringList Delete(int index);
    IJclStringList Clear();
}

public sealed class JclIndexedValues<TKey, TValue>
{
    private readonly Func<TKey, TValue> read;
    private readonly Action<TKey, TValue> write;

    internal JclIndexedValues(
        Func<TKey, TValue> read,
        Action<TKey, TValue> write)
    {
        this.read = read;
        this.write = write;
    }

    public TValue this[TKey index]
    {
        get => read(index);
        set => write(index, value);
    }
}

public sealed class TJclStringList : TInterfacedObject, IJclStringList
{
    private sealed class Entry
    {
        internal string Text;
        internal Entry(string text) => Text = text;
    }

    private interface IAssociatedValues
    {
        void Remove(Entry entry);
        void Move(Entry previous, Entry replacement);
        void Clear();
    }

    private sealed class AssociatedValues<T> : IAssociatedValues
    {
        private readonly Dictionary<Entry, T> values = new();

        internal T Get(Entry entry) =>
            values.TryGetValue(entry, out var value) ? value : default!;

        internal void Set(Entry entry, T value) => values[entry] = value;

        public void Remove(Entry entry) => values.Remove(entry);

        public void Move(Entry previous, Entry replacement)
        {
            if (values.Remove(previous, out var value))
                values[replacement] = value;
        }

        public void Clear() => values.Clear();
    }

    private readonly List<Entry> entries = new();
    private IAssociatedValues? associatedValues;
    private bool caseSensitive;
    private bool sorted;

    // The concrete storage type fixes the list-wide associated-data mode.
    // A cast rejects an attempt to use another incompatible storage type.
    private AssociatedValues<T> Storage<T>()
    {
        associatedValues ??= new AssociatedValues<T>();
        return (AssociatedValues<T>)associatedValues;
    }

    public TJclStringList()
    {
        Strings = new(
            index => entries[index].Text,
            (index, value) =>
            {
                Entry previous = entries[index];
                var replacement = new Entry(value);
                IList<Entry> writable = Sorted
                    ? entries.AsReadOnly()
                    : entries;
                writable[index] = replacement;
                associatedValues?.Move(previous, replacement);
            });

        Variants = new(
            index => Storage<Variant>().Get(entries[index]),
            (index, value) => Storage<Variant>().Set(entries[index], value));

        KeyVariant = new(
            key =>
            {
                int index = IndexOf(key);
                return index < 0 ? Variant.Unassigned : Variants[index];
            },
            (key, value) =>
            {
                Storage<Variant>();
                int index = IndexOf(key);
                if (index < 0)
                    index = Add(key);
                Variants[index] = value;
            });

        Interfaces = new(
            index => Storage<object?>().Get(entries[index]),
            (index, value) => Storage<object?>().Set(entries[index], value));

        Lists = new(
            index =>
            {
                object? value = Interfaces[index];
                if (value is null)
                {
                    IJclStringList child = new TJclStringList();
                    Interfaces[index] = child;
                    return child;
                }
                return (IJclStringList)value;
            },
            (index, value) => Interfaces[index] = value);
    }

    private StringComparer Comparer => CaseSensitive
        ? StringComparer.CurrentCulture
        : StringComparer.CurrentCultureIgnoreCase;

    public int Count => entries.Count;
    public int LastIndex => Count - 1;

    public TJclStringListObjectsMode ObjectsMode => associatedValues switch
    {
        AssociatedValues<Variant> => TJclStringListObjectsMode.omVariants,
        AssociatedValues<object?> => TJclStringListObjectsMode.omInterfaces,
        _ => TJclStringListObjectsMode.omNone
    };

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
    public JclIndexedValues<int, string> Strings { get; }
    public JclIndexedValues<int, Variant> Variants { get; }
    public JclIndexedValues<string, Variant> KeyVariant { get; }
    public JclIndexedValues<int, object?> Interfaces { get; }
    public JclIndexedValues<int, IJclStringList> Lists { get; }

    public string this[int index]
    {
        get => Strings[index];
        set => Strings[index] = value;
    }

    public int Add(string value)
    {
        if (!Sorted)
        {
            int appended = entries.Count;
            entries.Add(new Entry(value));
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
        {
            return low;
        }

        if (Duplicates == TDuplicates.dupError)
        {
            var uniqueEntries = new Dictionary<string, Entry>(comparer);
            foreach (Entry existing in entries)
                uniqueEntries.TryAdd(existing.Text, existing);
            uniqueEntries.Add(value, new Entry(value));
        }

        entries.Insert(low, new Entry(value));
        return low;
    }

    public int IndexOf(string value)
    {
        StringComparer comparer = Comparer;
        for (int index = 0; index < entries.Count; index++)
        {
            if (comparer.Equals(entries[index].Text, value))
                return index;
        }
        return -1;
    }

    public IJclStringList Sort()
    {
        StringComparer comparer = Comparer;
        entries.Sort((left, right) =>
            comparer.Compare(left.Text, right.Text));
        return this;
    }

    public IJclStringList Delete(int index)
    {
        Entry entry = entries[index];
        associatedValues?.Remove(entry);
        entries.RemoveAt(index);
        return this;
    }

    public IJclStringList Clear()
    {
        associatedValues?.Clear();
        entries.Clear();
        return this;
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