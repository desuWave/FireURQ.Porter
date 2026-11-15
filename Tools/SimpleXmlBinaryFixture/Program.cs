using System;
using System.IO;
using FireURQ.Runtime;

foreach (var (type, payload, wire) in new (ushort, string, string)[]
{
    (3, "EB32A4F8", "03EB32A4F8"),
    (5, "0000000000002940", "050000000000002940"),
    (11, "FFFF", "0BFFFF"),
    (256, "46697265555251", "81000746697265555251"),
    (8, "1604", "08011604"),
    (0x2011, "0080FF", "A011030080FF")
})
{
    Check(type, Convert.FromHexString(payload), Convert.FromHexString(wire));
}

// The long form keeps the data readable by the original reader at the FF boundary.
var boundaryPayload = new byte[0x7F00];
var boundaryWire = new byte[boundaryPayload.Length + 7];
Convert.FromHexString("A011FF007F0000").CopyTo(boundaryWire, 0);
Check(0x2011, boundaryPayload, boundaryWire);

using var adjacent = new MemoryStream(Convert.FromHexString("0BFFFF08011604"));
if (SimpleXmlVariantBinary.Read(adjacent.ReadExactly).TypeCode != 11 ||
    adjacent.Position != 3 ||
    SimpleXmlVariantBinary.Read(adjacent.ReadExactly).TypeCode != 8 ||
    adjacent.Position != adjacent.Length)
    throw new InvalidDataException("Reading a value consumed bytes of its neighbor.");

Console.WriteLine("SimpleXML variant wire checks passed.");

static void Check(ushort type, byte[] payload, byte[] wire)
{
    using var input = new MemoryStream(wire);
    var decoded = SimpleXmlVariantBinary.Read(input.ReadExactly);
    if (decoded.TypeCode != type || !decoded.Payload.AsSpan().SequenceEqual(payload) ||
        input.Position != input.Length)
        throw new InvalidDataException($"Unexpected decoded value for type {type}.");

    using var output = new MemoryStream();
    SimpleXmlVariantBinary.Write(output.Write, type, payload);
    if (!output.ToArray().AsSpan().SequenceEqual(wire))
        throw new InvalidDataException($"Unexpected written bytes for type {type}.");
}
