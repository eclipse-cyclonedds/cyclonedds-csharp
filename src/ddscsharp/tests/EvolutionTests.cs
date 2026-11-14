// Copyright(c) 2026 Sven Trittler
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Org.Eclipse.CycloneDDS;
using Test;

internal static partial class Program
{
    private static EvolutionV1 OldModel() => new()
    {
        id = 71, text = "evolution Grüße 🌍", retired = { 1, -2, 3 }, child = new() { count = 42 }
    };
    private static EvolutionV2 NewModel(bool present) => new()
    {
        id = 71, text = "evolution Grüße 🌍", added = present ? "new high-ID member" : null,
        child = new() { count = 42, note = present ? "nested addition" : null }
    };

    private static void RegisterEvolutionTests()
    {
        Run("Evolution.Xcdr1.native-appendable-defaults-at-payload-boundary", () =>
        {
            // The oracle poisons spare serdata capacity. Vary the final string's
            // alignment so capacity/padding cannot be mistaken for appended fields.
            for (int length = 0; length < 16; length++)
            {
                string text = new('x', length);
                CheckNativeEvolution(AppendableV1.TypeSupport, new AppendableV1 { id = 9, text = text },
                    AppendableV2.TypeSupport, new AppendableV2 { id = 9, text = text }, DataRepresentationKind.Xcdr1);
            }
        });
        Run("Evolution.Xcdr1.native-fixed-size-appendable-defaults", () =>
            CheckNativeEvolution(BoundaryV1.TypeSupport, new BoundaryV1 { id = 9 },
                BoundaryV2.TypeSupport, new BoundaryV2 { id = 9 }, DataRepresentationKind.Xcdr1));
        foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
        {
            Run($"Evolution.{representation}.mutable-old-to-new", () =>
            {
                var copy = EvolutionV2.TypeSupport.Deserialize(EvolutionV1.TypeSupport.Serialize(OldModel(), representation));
                Equal(NewModel(false), copy);
            });
            foreach (bool present in new[] { false, true })
            {
                Run($"Evolution.{representation}.mutable-new-to-old.optional-{present}", () =>
                {
                    var copy = EvolutionV1.TypeSupport.Deserialize(EvolutionV2.TypeSupport.Serialize(NewModel(present), representation));
                    var expected = OldModel(); expected.retired.Clear(); Equal(expected, copy);
                });
            }
            Run($"Evolution.{representation}.mutable-known-and-unknown-must-understand", () =>
            {
                var value = new EvolutionRequired { id = 71, text = "required", required = 99, child = new() { count = 42 } };
                byte[] bytes = EvolutionRequired.TypeSupport.Serialize(value, representation);
                Equal(value, EvolutionRequired.TypeSupport.Deserialize(bytes));
                Throws<InvalidDataException>(() => EvolutionV1.TypeSupport.Deserialize(bytes));
            });
            Run($"Evolution.{representation}.appendable-both-directions", () =>
            {
                var old = new AppendableV1 { id = 9, text = "old prefix" };
                var newer = new AppendableV2 { id = 9, text = "old prefix", count = 123, note = "added tail" };
                Equal(old, AppendableV1.TypeSupport.Deserialize(AppendableV2.TypeSupport.Serialize(newer, representation)));
                var widened = AppendableV2.TypeSupport.Deserialize(AppendableV1.TypeSupport.Serialize(old, representation));
                Equal(9, widened.id); Equal(old.text, widened.text); Equal(0, widened.count); True(widened.note is null);
            });
            Run($"Evolution.{representation}.trailing-data-remains-invalid-for-final-and-mutable", () =>
            {
                byte[] final = Message.TypeSupport.Serialize(new Message(7, "final", 9), representation);
                byte[] mutable = Mutable.TypeSupport.Serialize(new Mutable { id = 7 }, representation);
                Throws<InvalidDataException>(() => Message.TypeSupport.Deserialize([.. final, 0, 0, 0, 0]));
                Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize([.. mutable, 0, 0, 0, 0]));
                if (representation == DataRepresentationKind.Xcdr2)
                {
                    byte[] appendable = AppendableV1.TypeSupport.Serialize(new AppendableV1 { id = 7, text = "prefix" }, representation);
                    Throws<InvalidDataException>(() => AppendableV1.TypeSupport.Deserialize([.. appendable, 0, 0, 0, 0]));
                }
            });
            Run($"Evolution.{representation}.native-oracle-old-to-new", () =>
                CheckNativeEvolution(EvolutionV1.TypeSupport, OldModel(), EvolutionV2.TypeSupport, NewModel(false), representation));
            Run($"Evolution.{representation}.native-oracle-new-to-old", () =>
            {
                var expected = OldModel(); expected.retired.Clear();
                CheckNativeEvolution(EvolutionV2.TypeSupport, NewModel(true), EvolutionV1.TypeSupport, expected, representation);
            });
            Run($"Evolution.{representation}.native-oracle-appendable-old-to-new", () =>
                CheckNativeEvolution(AppendableV1.TypeSupport, new AppendableV1 { id = 9, text = "prefix" },
                    AppendableV2.TypeSupport, new AppendableV2 { id = 9, text = "prefix" }, representation));
            Run($"Evolution.{representation}.native-oracle-appendable-new-to-old", () =>
                CheckNativeEvolution(AppendableV2.TypeSupport, new AppendableV2 { id = 9, text = "prefix", count = 4, note = "tail" },
                    AppendableV1.TypeSupport, new AppendableV1 { id = 9, text = "prefix" }, representation));
            Run($"Evolution.{representation}.dds-mutable-old-to-new", () =>
                CheckDdsEvolution(EvolutionV1.TypeSupport, OldModel(), EvolutionV2.TypeSupport, NewModel(false), representation));
            Run($"Evolution.{representation}.dds-mutable-new-to-old", () =>
            {
                var expected = OldModel(); expected.retired.Clear();
                CheckDdsEvolution(EvolutionV2.TypeSupport, NewModel(true), EvolutionV1.TypeSupport, expected, representation);
            });
            Run($"Evolution.{representation}.dds-appendable-old-to-new", () =>
                CheckDdsEvolution(AppendableV1.TypeSupport, new AppendableV1 { id = 9, text = "prefix" },
                    AppendableV2.TypeSupport, new AppendableV2 { id = 9, text = "prefix" }, representation));
            Run($"Evolution.{representation}.dds-appendable-new-to-old", () =>
                CheckDdsEvolution(AppendableV2.TypeSupport, new AppendableV2 { id = 9, text = "prefix", count = 4, note = "tail" },
                    AppendableV1.TypeSupport, new AppendableV1 { id = 9, text = "prefix" }, representation));
        }

        // These fixtures are assembled independently of our writer: both PL_CDR header forms and byte orders.
        foreach (bool bigEndian in new[] { false, true })
        foreach (bool extended in new[] { false, true })
        {
            string name = $"Evolution.Xcdr1.fixture.{(bigEndian ? "BE" : "LE")}.{(extended ? "extended" : "short")}";
            Run(name + ".reordered-and-unknown", () =>
            {
                byte[] bytes = ParameterList(bigEndian,
                    new WireParameter(3, StringBody("fixture", bigEndian), false, extended),
                    new WireParameter(extended ? 65537u : 99u, [1, 2, 3, 4, 5], false, extended),
                    new WireParameter(0, IntBody(17, bigEndian), true, extended));
                var copy = Mutable.TypeSupport.Deserialize(bytes);
                Equal(17, copy.id); Equal("fixture", copy.text); Equal(0, copy.numbers.Count);
            });
            Run(name + ".unknown-must-understand", () =>
                Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(ParameterList(bigEndian,
                    new WireParameter(0, IntBody(17, bigEndian), false, extended),
                    new WireParameter(extended ? 65537u : 99u, [1, 2, 3, 4], true, extended)))));
            Run(name + ".duplicate-member-rejected", () =>
                Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(ParameterList(bigEndian,
                    new WireParameter(0, IntBody(17, bigEndian), false, extended), new WireParameter(0, IntBody(18, bigEndian), false, extended)))));
            Run(name + ".all-truncated-prefixes-rejected", () =>
            {
                byte[] bytes = ParameterList(bigEndian, new WireParameter(0, IntBody(17, bigEndian), false, extended),
                    new WireParameter(3, StringBody("fixture", bigEndian), false, extended));
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte[] prefix = bytes[..i];
                    Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(prefix));
                }
            });
        }
        Run("Evolution.Xcdr1.invalid-extended-header-size", () =>
        {
            byte[] bytes = ParameterList(false, new WireParameter(0, IntBody(17, false), false, true));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 4);
            Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(bytes));
        });
        Run("Evolution.Xcdr1.member-length-outside-buffer", () =>
        {
            byte[] bytes = ParameterList(false, new WireParameter(0, IntBody(17, false), false, true));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), uint.MaxValue);
            Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(bytes));
        });
        Run("Evolution.Xcdr1.member-cannot-read-next-parameter", () =>
        {
            byte[] bytes = ParameterList(false, new WireParameter(3, StringBody("fixture", false), false, true),
                new WireParameter(0, IntBody(17, false), false, true));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 4); // string length fits, contents do not
            Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(bytes));
        });
        Run("Evolution.Xcdr1.invalid-terminator-length", () =>
        {
            byte[] bytes = ParameterList(false, new WireParameter(0, IntBody(17, false), false, false));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length - 2), 4);
            Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(bytes));
        });
    }

    private static void CheckDdsEvolution<TWrite, TRead>(TypeSupport<TWrite> writeSupport, TWrite value,
        TypeSupport<TRead> readSupport, TRead expected, DataRepresentationKind representation)
        where TWrite : class where TRead : class
    {
        // Distinct participants also exercise XTypes discovery/assignability rather than one shared topic descriptor.
        using var publisher = Participant(); using var subscriber = Participant(); string name = Name();
        using var wt = publisher.CreateTopic(name, writeSupport); using var rt = subscriber.CreateTopic(name, readSupport);
        var qos = Reliable with { DataRepresentation = [representation],
            TypeConsistency = new(TypeConsistencyKind.AllowTypeCoercion, ForceTypeValidation: true) };
        using var reader = subscriber.CreateReader(rt, qos); using var writer = publisher.CreateWriter(wt, qos);
        WaitUntil(() => writer.MatchedSubscriptions.Count == 1 && reader.MatchedPublications.Count == 1);
        writer.Write(value); var sample = TakeOne(reader); Equal(expected, sample.Data);
        writer.DisposeInstance(value); var disposed = TakeOne(reader, false);
        True(!disposed.Info.ValidData); Equal(InstanceState.NotAliveDisposed, disposed.Info.InstanceState);
        // The key must survive evolution and the key-only path as well.
        Equal(sample.Info.InstanceHandle, disposed.Info.InstanceHandle);
        Equal(typeof(TRead).GetProperty("id")!.GetValue(expected), typeof(TRead).GetProperty("id")!.GetValue(disposed.Data));
    }

    private static void CheckNativeEvolution<TWrite, TRead>(TypeSupport<TWrite> writeSupport, TWrite value,
        TypeSupport<TRead> readSupport, TRead expected, DataRepresentationKind representation)
        where TWrite : class where TRead : class
    {
        using var p = Participant(); using var wt = p.CreateTopic(Name(), writeSupport); using var rt = p.CreateTopic(Name(), readSupport);
        var qos = Reliable with { DataRepresentation = [representation] };
        using var writer = p.CreateWriter(wt, qos); using var target = p.CreateWriter(rt, qos);
        using var arena = new NativeArena(); nint native = arena.Allocate(writeSupport.Codec.Size);
        writeSupport.Codec.Write(native, value, arena);
        Equal(0, CdrOracle.oracle_encode(CdrOracle.Handle(writer), native, out nint buffer, out uint size, new byte[16], 0));
        try
        {
            var bytes = new byte[size]; Marshal.Copy(buffer, bytes, 0, checked((int)size));
            Equal(expected, readSupport.Deserialize(bytes));
        }
        finally { CdrOracle.oracle_free(buffer); }
        byte[] managed = writeSupport.Serialize(value, representation); nint decoded = arena.Allocate(readSupport.Codec.Size);
        try
        {
            Equal(0, CdrOracle.oracle_decode(CdrOracle.Handle(target), managed, (uint)managed.Length, decoded, new byte[16], 0));
            Equal(expected, readSupport.Codec.Read(decoded));
        }
        finally { CdrOracle.oracle_fini(decoded, readSupport.Descriptor); }
    }

    private sealed record WireParameter(uint Id, byte[] Body, bool MustUnderstand, bool Extended);
    private static byte[] IntBody(int value, bool bigEndian)
    {
        byte[] bytes = new byte[4];
        if (bigEndian) BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        else BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }
    private static byte[] StringBody(string value, bool bigEndian)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
        return [.. IntBody(utf8.Length + 1, bigEndian), .. utf8, 0];
    }
    private static byte[] ParameterList(bool bigEndian, params WireParameter[] parameters)
    {
        var bytes = new List<byte> { 0, (byte)(bigEndian ? 2 : 3), 0, 0 };
        void UInt16(ushort value)
        {
            byte[] encoded = new byte[2];
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(encoded, value);
            else BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
            bytes.AddRange(encoded);
        }
        foreach (var parameter in parameters)
        {
            while ((bytes.Count & 3) != 0) bytes.Add(0);
            if (parameter.Extended)
            {
                UInt16(0x7f01); UInt16(8);
                bytes.AddRange(IntBody(unchecked((int)(parameter.Id | (parameter.MustUnderstand ? 0x40000000u : 0))), bigEndian));
                bytes.AddRange(IntBody(parameter.Body.Length, bigEndian));
            }
            else
            {
                UInt16((ushort)(parameter.Id | (parameter.MustUnderstand ? 0x4000u : 0))); UInt16((ushort)parameter.Body.Length);
            }
            bytes.AddRange(parameter.Body);
        }
        while ((bytes.Count & 3) != 0) bytes.Add(0);
        UInt16(0x7f02); UInt16(0);
        return bytes.ToArray();
    }
}
