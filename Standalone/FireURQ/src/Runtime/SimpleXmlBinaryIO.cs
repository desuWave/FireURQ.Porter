using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FireURQ.Runtime;

// Delphi unit-private classes are emitted at assembly scope by Porter.
public abstract class TBinXmlReader : TObject
{
    private readonly SimpleXmlVariantBinary.ReadBuffer read;
    private readonly uint options;

    internal TBinXmlReader(SimpleXmlVariantBinary.ReadBuffer read)
    {
        this.read = read;
        Span<byte> flags = stackalloc byte[sizeof(uint)];
        read(flags);
        options = BinaryPrimitives.ReadUInt32LittleEndian(flags);
    }

    public int ReadLongint() => SimpleXmlVariantBinary.ReadLongint(read);

    public string ReadAnsiString() => ReadString(SimpleXmlBinaryEncoding.Ansi);
    public string ReadWideString() => ReadString(Encoding.Unicode);
    public string ReadXmlString() => (options & 1) == 0 ? ReadAnsiString() : ReadWideString();

    public void ReadVariant(ref Variant value) =>
        value = SimpleXmlVariantValue.Read(read, SimpleXmlBinaryEncoding.Ansi);

    public byte ReadNodeType()
    {
        Span<byte> data = stackalloc byte[1];
        read(data);
        return data[0];
    }

    public void ReadNameIndexes(int[] indexes)
    {
        Span<byte> data = stackalloc byte[sizeof(int)];
        foreach (ref int index in indexes.AsSpan())
        {
            read(data);
            index = BinaryPrimitives.ReadInt32LittleEndian(data);
        }
    }

    private string ReadString(Encoding encoding)
    {
        int width = encoding == Encoding.Unicode ? 2 : 1;
        int length = ReadLongint();
        if (length < 0 || length > int.MaxValue / width)
            throw new InvalidDataException("Invalid SimpleXML string length.");

        using var data = new MemoryStream();
        Span<byte> buffer = stackalloc byte[4096];
        for (int remaining = length * width; remaining > 0;)
        {
            Span<byte> chunk = buffer[..Math.Min(remaining, buffer.Length)];
            read(chunk);
            data.Write(chunk);
            remaining -= chunk.Length;
        }

        return encoding.GetString(data.GetBuffer(), 0, (int)data.Length);
    }
}

public sealed class TStmXmlReader : TBinXmlReader
{
    private TStmXmlReader(TStream stream) : base(buffer =>
    {
        var bytes = new byte[buffer.Length];
        stream.ReadBuffer(bytes, bytes.Length);
        bytes.AsSpan().CopyTo(buffer);
    }) { }

    public static TStmXmlReader Create(TStream stream, int bufferSize) => new(stream);

    public static string ReadSignature(TStream stream)
    {
        var bytes = new byte[SimpleXmlBinaryEncoding.Signature.Length];
        stream.ReadBuffer(bytes, bytes.Length);
        return Encoding.Latin1.GetString(bytes);
    }
}

public sealed class TStrXmlReader : TBinXmlReader
{
    private TStrXmlReader(MemoryStream stream) : base(stream.ReadExactly) { }

    public static TStrXmlReader Create(in string data)
    {
        var stream = new MemoryStream(Encoding.Latin1.GetBytes(data), writable: false);
        stream.Position = SimpleXmlBinaryEncoding.Signature.Length;
        return new TStrXmlReader(stream);
    }
}

public abstract class TBinXmlWriter : TObject
{
    private readonly SimpleXmlVariantBinary.WriteBuffer write;
    private readonly uint options;

    internal TBinXmlWriter(SimpleXmlVariantBinary.WriteBuffer write, uint options)
    {
        this.write = write;
        this.options = options;
        write(SimpleXmlBinaryEncoding.Signature);
        Span<byte> flags = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(flags, options);
        write(flags);
    }

    public void WriteLongint(int value) => SimpleXmlVariantBinary.WriteLongint(write, value);

    public void WriteAnsiString(in string value) => WriteString(value, SimpleXmlBinaryEncoding.Ansi);
    public void WriteWideString(in string value) => WriteString(value, Encoding.Unicode);
    public void WriteXmlString(in string value)
    {
        if ((options & 1) == 0)
            WriteAnsiString(value);
        else
            WriteWideString(value);
    }

    public void WriteVariant(in Variant value) =>
        SimpleXmlVariantValue.Write(write, value, SimpleXmlBinaryEncoding.Ansi);

    public void WriteNodeType(byte value)
    {
        Span<byte> data = stackalloc byte[1];
        data[0] = value;
        write(data);
    }

    public void WriteNameIndexes(int[] indexes)
    {
        Span<byte> data = stackalloc byte[sizeof(int)];
        foreach (int index in indexes)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data, index);
            write(data);
        }
    }

    private void WriteString(string text, Encoding encoding)
    {
        byte[] bytes = encoding.GetBytes(text);
        WriteLongint(encoding == Encoding.Unicode ? bytes.Length / 2 : bytes.Length);
        if (bytes.Length != 0)
            write(bytes);
    }
}

public sealed class TStmXmlWriter : TBinXmlWriter
{
    private TStmXmlWriter(TStream stream, uint options) : base(buffer =>
    {
        byte[] bytes = buffer.ToArray();
        stream.WriteBuffer(bytes, bytes.Length);
    }, options) { }

    public static TStmXmlWriter Create(TStream stream, uint options, int bufferSize) => new(stream, options);
}

public sealed class TStrXmlWriter : TBinXmlWriter
{
    private readonly MemoryStream stream;

    private TStrXmlWriter(MemoryStream stream, uint options) : base(stream.Write, options) =>
        this.stream = stream;

    public static TStrXmlWriter Create(uint options, int bufferSize) =>
        new(new MemoryStream(), options);

    public void FlushBuf() { } // MemoryStream already holds every byte written.

    public string FData => Encoding.Latin1.GetString(stream.GetBuffer(), 0, (int)stream.Length);
}

internal static class SimpleXmlBinaryEncoding
{
    internal static ReadOnlySpan<byte> Signature => "< binary-xml >"u8;
    internal static Encoding Ansi => CodePage.Value;

    private static readonly Lazy<Encoding> CodePage = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        uint codePage = GetACP();
        if (codePage == 0)
            throw new Win32Exception("Could not determine the Windows ANSI code page.");
        return Encoding.GetEncoding((int)codePage);
    });

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetACP();
}
