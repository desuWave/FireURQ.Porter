using System.Runtime.InteropServices;
using FireURQ.Runtime;

var processEncoding = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(0) ?? System.Text.Encoding.UTF8;
string sample = "Фурка\0quest";
byte[] expectedBytes = processEncoding.GetBytes(sample);
var encoded = AnsiString.FromSystemText(sample);
if (!encoded.ToBytes().SequenceEqual(expectedBytes) || encoded.ToSystemText() != processEncoding.GetString(expectedBytes))
    throw new InvalidOperationException("The ANSI text boundary lost the process encoding or the embedded NUL.");

IntPtr memory = Marshal.AllocHGlobal(16);
try
{
    DelphiSystem.FillNative(memory, 16, 0xCC);
    DelphiSystem.ElementAt<uint>(memory, 1) = 0x12345678;
    if (Marshal.ReadInt32(memory, 4) != 0x12345678 ||
        Marshal.ReadByte(memory, 3) != 0xCC || Marshal.ReadByte(memory, 8) != 0xCC)
        throw new InvalidOperationException("The write did not respect the element stride.");

    byte[] original = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    foreach (var (source, destination) in new[] { (0, 3), (3, 0) })
    {
        Marshal.Copy(original, 0, memory, original.Length);
        byte[] expected = (byte[])original.Clone();
        Array.Copy(original, source, expected, destination, 10);
        DelphiSystem.MoveNative(memory + source, memory + destination, 10);
        var actual = new byte[16];
        Marshal.Copy(memory, actual, 0, actual.Length);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Overlapping Move changed the wrong bytes.");
    }
    var words = new uint[] { 0xAABBCCDD, 0x11223344 };
    DelphiSystem.CopyToNative(words, ref DelphiSystem.ElementAt<uint>(memory, 1), 8);
    if (Marshal.ReadInt32(memory, 4) != unchecked((int)words[0]) || Marshal.ReadInt32(memory, 8) != (int)words[1])
        throw new InvalidOperationException("A multi-element copy lost part of the source array.");
    DelphiSystem.FillNative(IntPtr.Zero, 0, 1);
    DelphiSystem.MoveNative(IntPtr.Zero, IntPtr.Zero, 0);
}
finally
{
    Marshal.FreeHGlobal(memory);
}
Console.WriteLine("Native buffer stride, overlapping moves and zero-length operations passed.");
