using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.Geometry." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

var utf8 = new UTF8Encoding(false);

File.WriteAllText(
    Path.Combine(directory, "Geometry.cs"),
    CSharpGeometryRuntime.Source,
    utf8);

File.WriteAllText(
    Path.Combine(directory, "Probe.csproj"),
    """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""",
    utf8);

File.WriteAllText(
    Path.Combine(directory, "Program.cs"),
    """
using System.Runtime.InteropServices;
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiGeometry;

int passed = 0;
int total = 0;

void Check(bool condition, string name)
{
    total++;
    if (condition)
        passed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

Check(Marshal.SizeOf<TPoint>() == 8, "TPoint occupies eight bytes");
Check(Marshal.SizeOf<TRect>() == 16, "TRect occupies sixteen bytes");

Check(
    Marshal.OffsetOf<TPoint>(nameof(TPoint.X)).ToInt32() == 0 &&
    Marshal.OffsetOf<TPoint>(nameof(TPoint.Y)).ToInt32() == 4,
    "Point fields have signed 32-bit native layout");

Check(
    Marshal.OffsetOf<TRect>(nameof(TRect.Left)).ToInt32() == 0 &&
    Marshal.OffsetOf<TRect>(nameof(TRect.Top)).ToInt32() == 4 &&
    Marshal.OffsetOf<TRect>(nameof(TRect.Right)).ToInt32() == 8 &&
    Marshal.OffsetOf<TRect>(nameof(TRect.Bottom)).ToInt32() == 12,
    "Rectangle scalar fields have the expected offsets");

Check(
    Marshal.OffsetOf<TRect>(nameof(TRect.TopLeft)).ToInt32() == 0 &&
    Marshal.OffsetOf<TRect>(nameof(TRect.BottomRight)).ToInt32() == 8,
    "Rectangle point views overlap the scalar fields");

var point = Point(-17, 29);
Check(point.X == -17 && point.Y == 29,
    "Point factory preserves signed coordinates");

var rect = Rect(-10, -20, 30, 40);
Check(
    rect.TopLeft.X == -10 && rect.TopLeft.Y == -20 &&
    rect.BottomRight.X == 30 && rect.BottomRight.Y == 40,
    "Rectangle factory initializes both point views");

rect.TopLeft = Point(-7, -9);
rect.BottomRight = Point(13, 15);
Check(
    rect.Left == -7 && rect.Top == -9 &&
    rect.Right == 13 && rect.Bottom == 15,
    "Point-view assignment updates scalar fields");

rect.Left = -100;
rect.Top = -200;
rect.Right = 300;
rect.Bottom = 400;
Check(
    rect.TopLeft.X == -100 && rect.TopLeft.Y == -200 &&
    rect.BottomRight.X == 300 && rect.BottomRight.Y == 400,
    "Scalar assignment updates point views");

var copy = rect;
copy.TopLeft.X = 777;
Check(rect.Left == -100 && copy.Left == 777,
    "Rectangle copies have independent value storage");

var bounds = Rect(-2, -3, 4, 5);
Check(PtInRect(bounds, Point(-2, -3)),
    "Point containment includes left and top edges");
Check(!PtInRect(bounds, Point(4, 0)) &&
      !PtInRect(bounds, Point(0, 5)),
    "Point containment excludes right and bottom edges");
Check(!PtInRect(bounds, Point(-3, 0)) &&
      !PtInRect(bounds, Point(0, -4)),
    "Point containment rejects points outside the rectangle");

Check(
    !PtInRect(Rect(1, 1, 1, 4), Point(1, 2)) &&
    !PtInRect(Rect(1, 1, 4, 1), Point(2, 1)) &&
    !PtInRect(Rect(4, 4, 1, 1), Point(2, 2)),
    "Empty and inverted rectangles contain no points");

Check(
    PtInRect(
        Rect(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue),
        Point(int.MinValue, int.MinValue)) &&
    !PtInRect(
        Rect(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue),
        Point(int.MaxValue, 0)),
    "Extreme coordinates do not require overflowing subtraction");

IntPtr memory = Marshal.AllocHGlobal(16);
try
{
    var original = Rect(-123, 456, int.MaxValue, int.MinValue);
    Marshal.StructureToPtr(original, memory, false);
    Check(
        Marshal.ReadInt32(memory, 0) == -123 &&
        Marshal.ReadInt32(memory, 4) == 456 &&
        Marshal.ReadInt32(memory, 8) == int.MaxValue &&
        Marshal.ReadInt32(memory, 12) == int.MinValue,
        "Native rectangle storage preserves every coordinate");

    Marshal.StructureToPtr(Point(-91, 82), memory, false);
    var nativePoint = Marshal.PtrToStructure<TPoint>(memory);
    Check(nativePoint.X == -91 && nativePoint.Y == 82,
        "Writing through a native point address updates both coordinates");

    Marshal.StructureToPtr(
        Point(-31, 42), IntPtr.Add(memory, 8), false);
    var nativeRect = Marshal.PtrToStructure<TRect>(memory);
    Check(
        nativeRect.Left == -91 && nativeRect.Top == 82 &&
        nativeRect.Right == -31 && nativeRect.Bottom == 42,
        "Two adjacent native points share the rectangle layout");

    Marshal.WriteInt32(memory, 0, int.MinValue);
    Marshal.WriteInt32(memory, 4, int.MaxValue);
    nativePoint = Marshal.PtrToStructure<TPoint>(memory);
    Check(
        nativePoint.X == int.MinValue &&
        nativePoint.Y == int.MaxValue,
        "Native writes preserve signed coordinate limits");
}
finally
{
    Marshal.FreeHGlobal(memory);
}

Console.WriteLine($"Geometry checks: {passed}/{total} passed.");
Environment.ExitCode = passed == total ? 0 : 1;
""",
    utf8);

var start = new ProcessStartInfo("dotnet")
{
    WorkingDirectory = directory,
    UseShellExecute = false
};
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Probe.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");

using var process = Process.Start(start)!;
process.WaitForExit();

Console.WriteLine("Generated compilation directory: " + directory);
Environment.ExitCode = process.ExitCode;