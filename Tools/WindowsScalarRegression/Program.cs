using System.Runtime.InteropServices;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}
SemanticUnit Parse(string text) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(text).Tokenize()).ParseUnit());

var consumer = Parse("""
unit Consumer;
interface
type
TWindow = HWND;
TResultCode = HResult;
TIdentifier = TGUID;
implementation
end.
""");
var shadow = Parse("""
unit Shadow;
interface
type HWND = Integer;
implementation
end.
""");
var resolver = new TypeResolver(new[] { consumer, shadow });

Check("HWND maps to pointer-sized storage",
    CSharpTypeMapper.Map("HWND", resolver, "Consumer") == "IntPtr");
Check("THandle mapping ignores case",
    CSharpTypeMapper.Map("Thandle") == "IntPtr");
Check("HRESULT stays signed",
    CSharpTypeMapper.Map("HResult") == "int");
Check("GUID uses qualified framework type",
    CSharpTypeMapper.Map("TGUID") == "global::System.Guid");
Check("Imported-independent handle alias resolves",
    CSharpTypeMapper.Map("TWindow", resolver, "Consumer") == "IntPtr");
Check("HRESULT alias resolves",
    CSharpTypeMapper.Map("TResultCode", resolver, "Consumer") == "int");
Check("GUID alias resolves",
    CSharpTypeMapper.Map("TIdentifier", resolver, "Consumer") ==
        "global::System.Guid");
Check("Explicit local alias takes precedence",
    CSharpTypeMapper.Map("HWND", resolver, "Shadow") == "int");
Check("Array of handles uses IntPtr elements",
    CSharpTypeMapper.Map("array of THandle", resolver, "Consumer") == "IntPtr[]");

Check("Handle storage matches process pointer width",
    Marshal.SizeOf<IntPtr>() == IntPtr.Size);
Check("HRESULT storage is four bytes",
    Marshal.SizeOf<int>() == 4);
Check("Failure bit remains signed",
    unchecked((int)0x80004005) < 0);
Check("GUID storage is sixteen bytes",
    Marshal.SizeOf<Guid>() == 16);

var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
Check("GUID native field byte order",
    guid.ToByteArray().SequenceEqual(new byte[]
    {
        0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66,
        0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
    }));

foreach (var item in new[]
{
    (Delphi: "UINT", CSharp: "uint"),
    (Delphi: "HMODULE", CSharp: "IntPtr"),
    (Delphi: "WPARAM", CSharp: "UIntPtr"),
    (Delphi: "LPARAM", CSharp: "IntPtr"),
    (Delphi: "LRESULT", CSharp: "IntPtr")
})
{
    Check(item.Delphi + ": legacy mapping",
        CSharpTypeMapper.Map(item.Delphi) == item.CSharp);
    Check(item.Delphi + ": contextual mapping",
        CSharpTypeMapper.Map(item.Delphi, resolver, "Consumer") == item.CSharp);
    Check(item.Delphi + ": case-insensitive mapping",
        CSharpTypeMapper.Map(item.Delphi.ToLowerInvariant()) == item.CSharp);
}

var nativeAliases = Parse("""
unit NativeAliases;
interface
type
TMessageWord = WPARAM;
TMessageLong = LPARAM;
TMessageResult = LRESULT;
TModuleHandle = HMODULE;
TMessageID = UINT;
implementation
end.
""");
var messageShadow = Parse("""
unit MessageShadow;
interface
type WPARAM = SmallInt;
implementation
end.
""");
var nativeResolver = new TypeResolver(new[] { nativeAliases, messageShadow });

Check("WPARAM alias remains unsigned and pointer-sized",
    CSharpTypeMapper.Map("TMessageWord", nativeResolver, "NativeAliases") == "UIntPtr");
Check("LPARAM alias remains signed and pointer-sized",
    CSharpTypeMapper.Map("TMessageLong", nativeResolver, "NativeAliases") == "IntPtr");
Check("LRESULT alias remains signed and pointer-sized",
    CSharpTypeMapper.Map("TMessageResult", nativeResolver, "NativeAliases") == "IntPtr");
Check("Module handle alias resolves",
    CSharpTypeMapper.Map("TModuleHandle", nativeResolver, "NativeAliases") == "IntPtr");
Check("UINT alias remains 32-bit",
    CSharpTypeMapper.Map("TMessageID", nativeResolver, "NativeAliases") == "uint");
Check("Explicit WPARAM declaration takes precedence",
    CSharpTypeMapper.Map("WPARAM", nativeResolver, "MessageShadow") == "short");
Check("Array of WPARAM preserves unsigned elements",
    CSharpTypeMapper.Map("array of WPARAM", nativeResolver, "NativeAliases") == "UIntPtr[]");

Check("WPARAM storage matches process pointer width",
    Marshal.SizeOf<UIntPtr>() == IntPtr.Size);
Check("Signed message value preserves minus one",
    new IntPtr(-1).ToInt64() == -1);
Check("Unsigned message value preserves all native bits",
    (UIntPtr.Size == 8 ? new UIntPtr(ulong.MaxValue) : new UIntPtr(uint.MaxValue))
        .ToUInt64() == (UIntPtr.Size == 8 ? ulong.MaxValue : uint.MaxValue));
Check("UINT storage is four bytes",
    Marshal.SizeOf<uint>() == 4);

Console.WriteLine($"Process pointer size: {IntPtr.Size * 8} bits");
Console.WriteLine($"Windows-scalar checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;