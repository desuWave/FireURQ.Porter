namespace FireURQ.Porter.Translation;

public static class CSharpAnsiStringRuntime
{
    public const string Source = """
using System;
using System.Text;

namespace FireURQ.Runtime;

// Initial byte-value layer.
// Text conversion requires an explicit Encoding.
// Native pointers and implicit text conversions are not implemented yet.
public readonly struct AnsiString : IEquatable<AnsiString>
{
    private readonly byte[]? data;

    private AnsiString(byte[] ownedData) => data = ownedData;

    private ReadOnlySpan<byte> Bytes => data ?? Array.Empty<byte>();

    public static AnsiString Empty => default;
    public int Length => data?.Length ?? 0;

    // Delphi string indexing is one-based and addresses bytes.
    public byte this[int index] => Bytes[index - 1];

    public static AnsiString FromBytes(ReadOnlySpan<byte> value) =>
        new(value.ToArray());

    public byte[] ToBytes() => Bytes.ToArray();

    public static AnsiString FromText(string value, Encoding encoding) =>
        new(encoding.GetBytes(value));

    public string Decode(Encoding encoding) =>
        encoding.GetString(Bytes);

    public AnsiString WithByte(int index, byte value)
    {
        byte[] replacement = ToBytes();
        replacement[index - 1] = value;
        return new AnsiString(replacement);
    }

    public AnsiString WithLength(int length)
    {
        byte[] replacement = new byte[length];
        Bytes[..Math.Min(Length, length)].CopyTo(replacement);
        return new AnsiString(replacement);
    }

    // Delphi-style Copy: one-based start, bounded length.
    public AnsiString Copy(int index, int count)
    {
        if (count <= 0)
            return Empty;

        int start = Math.Max(index, 1) - 1;
        if (start >= Length)
            return Empty;

        int available = Math.Min(count, Length - start);
        return FromBytes(Bytes.Slice(start, available));
    }

    public static AnsiString operator +(AnsiString left, AnsiString right)
    {
        if (left.Length == 0)
            return right;
        if (right.Length == 0)
            return left;

        byte[] combined = new byte[checked(left.Length + right.Length)];
        left.Bytes.CopyTo(combined);
        right.Bytes.CopyTo(combined.AsSpan(left.Length));
        return new AnsiString(combined);
    }

    public bool Equals(AnsiString other) => Bytes.SequenceEqual(other.Bytes);
    public override bool Equals(object? other) =>
        other is AnsiString value && Equals(value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (byte value in Bytes)
            hash.Add(value);
        return hash.ToHashCode();
    }

    public static bool operator ==(AnsiString left, AnsiString right) =>
        left.Equals(right);
    public static bool operator !=(AnsiString left, AnsiString right) =>
        !left.Equals(right);
}

// A temporary writable copy. The pointer is valid only until Dispose.
// ToAnsiString takes an independent immutable snapshot.
public sealed class AnsiStringBuffer : IDisposable
{
    private byte[]? storage;
    private System.Runtime.InteropServices.GCHandle pin;

    public AnsiStringBuffer(int length)
    {
        uint capacity = checked((uint)length + 1u);
        storage = new byte[checked((int)capacity)];
        Length = length;
        if (length > 0)
        {
            pin = System.Runtime.InteropServices.GCHandle.Alloc(
                storage,
                System.Runtime.InteropServices.GCHandleType.Pinned);
        }
    }

    public AnsiStringBuffer(AnsiString value) : this(value.Length)
    {
        value.ToBytes().CopyTo(storage!, 0);
    }

    public int Length { get; }

    public IntPtr Pointer =>
        pin.IsAllocated ? pin.AddrOfPinnedObject() : IntPtr.Zero;

    public IntPtr GetPointer(int count)
    {
        _ = storage.AsSpan(0, Length).Slice(0, count);
        return Pointer;
    }

    public AnsiString ToAnsiString() =>
        AnsiString.FromBytes(storage.AsSpan(0, Length));

    private void Release()
    {
        if (pin.IsAllocated)
            pin.Free();
        storage = null;
    }

    ~AnsiStringBuffer() => Release();

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }
}
""";
}