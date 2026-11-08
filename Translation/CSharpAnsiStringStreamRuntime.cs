namespace FireURQ.Porter.Translation;

public static class CSharpAnsiStringStreamRuntime
{
    public const string Source = """
namespace FireURQ.Runtime;

public static class AnsiStringStreams
{
    public static int Read(TStream stream, ref AnsiString value, int count)
    {
        using var buffer = new AnsiStringBuffer(value);
        try
        {
            return stream.Read(buffer.GetPointer(count), count);
        }
        finally
        {
            value = buffer.ToAnsiString();
        }
    }

    public static void ReadBuffer(TStream stream, ref AnsiString value, int count)
    {
        using var buffer = new AnsiStringBuffer(value);
        try
        {
            stream.ReadBuffer(buffer.GetPointer(count), count);
        }
        finally
        {
            value = buffer.ToAnsiString();
        }
    }

    public static int Write(TStream stream, AnsiString value, int count)
    {
        using var buffer = new AnsiStringBuffer(value);
        return stream.Write(buffer.GetPointer(count), count);
    }

    public static void WriteBuffer(TStream stream, AnsiString value, int count)
    {
        using var buffer = new AnsiStringBuffer(value);
        stream.WriteBuffer(buffer.GetPointer(count), count);
    }
}
""";
}