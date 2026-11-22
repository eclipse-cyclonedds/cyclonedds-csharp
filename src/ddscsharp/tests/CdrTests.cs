// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Reflection;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Org.Eclipse.CycloneDDS;
using Omg.Types;
using Test;

internal static partial class Program
{
    private static class CdrOracle
    {
        static CdrOracle()
        {
            NativeLibrary.SetDllImportResolver(typeof(CdrOracle).Assembly, (name, _, _) =>
            {
                if (name != "cdr_oracle") return 0;
                string file = OperatingSystem.IsWindows() ? "cdr_oracle.dll" : OperatingSystem.IsMacOS() ? "libcdr_oracle.dylib" : "libcdr_oracle.so";
                return NativeLibrary.Load(Path.Combine(DdsRuntime.LibraryDirectory!, file));
            });
        }
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern int oracle_encode(int writer, nint sample, out nint buffer, out uint size, [Out] byte[] hash, uint force);
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern int oracle_decode(int writer, byte[] buffer, uint size, nint sample, [Out] byte[] hash, uint force);
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern void oracle_free(nint buffer);
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern void oracle_fini(nint sample, nint descriptor);
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern int oracle_entity_parent(int entity);
        [DllImport("cdr_oracle", CallingConvention = CallingConvention.Cdecl)] internal static extern int oracle_entity_children(int entity, [Out] int[] children, uint capacity);
        internal static int Handle(Entity entity) => (int)typeof(Entity).GetProperty("Handle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(entity)!;
    }
    private static void RegisterCdrTests()
    {
        foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
        {
            void Check<T>(string name, TypeSupport<T> support, T sample) where T : class
                => Run($"CDR.{representation}.{name}", () => CheckCdr(support, sample, representation));
            Check("message", Message.TypeSupport, new Message(77, "Grüße 🌍", -19));
            Check("containers", Containers.TypeSupport, MakeContainers());
            Check("primitives", Primitives.TypeSupport, new Primitives { id = 1, flag = true, letter = 'ä', i8 = -99, i16 = -123, i32 = -991, i64 = long.MinValue, u64 = ulong.MaxValue, f32 = -1.25f, f64 = Math.PI, color = Color.BLUE, permissions = Permissions.ADMIN });
            Check("optional-absent", Optional.TypeSupport, new Optional { id = 1 });
            Check("optional-present", Optional.TypeSupport, new Optional { id = 2, number = -44, text = "optional 🌍", point = new Point { x = 1.25, y = -7.5 } });
            Check("inheritance-empty", InheritanceDerived.TypeSupport, new InheritanceDerived { id = 17 });
            Check("inheritance-populated", InheritanceDerived.TypeSupport, new InheritanceDerived { id = 18, values = { 1, -2, 3 }, flag = 255, level = 13, text = "inherited", measurement = -1.25 });
            Check("mutable", Mutable.TypeSupport, new Mutable { id = 7, text = "mutable", numbers = { 1, -2, 3 } });
            Check("recursive", Recursive.TypeSupport, new Recursive { id = 1, child = new Recursive { id = 2, children = { new Recursive { id = 3 } } } });
            Check("collections", WireCollections.TypeSupport, new WireCollections { id = 8, flags = { true, false, true }, chars = { 'a', 'ä' }, enums = { Test.SmallEnum.SMALL_ONE }, masks = { Permissions.ADMIN }, groups = { new Sequence<Point>([new Point { x = 4, y = 5 }]) } });
            Check("long-nested-key", LongKey.TypeSupport, new LongKey { key = new NestedKey { name = new string('ä', 100), number = long.MaxValue, ignored = "not a key" }, second = 55, data = { MakeContainers() } });
            Check("union-scalar", Choice.TypeSupport, new Choice { number = 6 });
            Check("union-string", Choice.TypeSupport, new Choice { text = "union" });
            Check("union-sequence", Choice.TypeSupport, UnionSequence());
            Check("union-struct", Choice.TypeSupport, new Choice { point = new Point { x = 1, y = 2 } });
            Check("union-default", Choice.TypeSupport, new Choice { fallback = 3.5 });
            Check("bool-union", BoolChoice.TypeSupport, new BoolChoice { yes = 17 });
            Check("enum-union", ColorChoice.TypeSupport, new ColorChoice { blue = new Point { x = 7, y = 8 } });
            Check("keyed-union", KeyedChoice.TypeSupport, new KeyedChoice { text = "keyed union" });
            Check("mutable-union", MutableChoice.TypeSupport, new MutableChoice { text = "mutable union" });
            Check("appendable-union", AppendableChoice.TypeSupport, new AppendableChoice { text = "appendable union" });
            var directRecursive = new DirectRecursive { id = 12 };
            directRecursive.children.Add(new DirectRecursive { id = 13 });
            Check("direct-recursive", DirectRecursive.TypeSupport, directRecursive);
            if (representation == DataRepresentationKind.Xcdr2) Check("direct-mixed", DirectMixed.TypeSupport, new DirectMixed { id = 4, optional_value = new Optional { id = 8, number = 2, point = new Point { x = 3, y = 4 } }, mutable_value = new Recursive { id = 9, child = new Recursive { id = 10 } } });
            Check("numeric-collections", NumericCollections.TypeSupport, MakeNumericCollections());
            Check("empty-numeric-collections", NumericCollections.TypeSupport, new NumericCollections { id = 7, prefix = 9, suffix = 8 });
            Check("scalar-blocks", ScalarBlocks.TypeSupport, new ScalarBlocks { id = 7, a = 1, b = 255, c = -127, d = short.MinValue, e = ushort.MaxValue, f = int.MinValue, g = uint.MaxValue, h = -1.25f, x = -2.5, y = double.Epsilon, i = long.MinValue, j = ulong.MaxValue });
            var mutual = new MutualA { id = 1 }; var child = new MutualB(); child.parents.Add(new MutualA { id = 2 }); mutual.children.Add(child);
            Check("mutual-recursion", MutualA.TypeSupport, mutual);
            Check("keyless", Keyless.TypeSupport, new Keyless { value = 10 });
        }
        Run("Generated.mutable-inheritance-copy-and-equality", () =>
        {
            var original = new InheritanceDerived { id = 21, values = { 7, 8 }, flag = 9, level = 12, text = "derived", measurement = 2.5 };
            var copy = new InheritanceDerived(original);
            Equal(original, copy); Equal(original.GetHashCode(), copy.GetHashCode());
            IEquatable<InheritanceBase> baseView = original;
            True(baseView.Equals(copy));
            copy.text = "changed";
            True(!baseView.Equals(copy));
            True(!original.Equals((object)new InheritanceBase { id = 21, values = { 7, 8 }, flag = 9 }));
            copy.values[0] = -1;
            Equal(7, original.values[0]);
            var constructed = new InheritanceDerived(21, new Sequence<int>([7, 8]), 9, 12, "derived", 2.5);
            Equal(original, constructed);
        });
        Run("CDR.big-endian-fixture", () =>
        {
            byte[] data = [0, 0, 0, 0, 0, 0, 0, 42, 0, 0, 0, 2, (byte)'x', 0, 0, 0, 0, 0, 0, 7];
            Equal(new Message(42, "x", 7), Message.TypeSupport.Deserialize(data));
        });
        Run("CDR.truncated-and-invalid-input", () =>
        {
            var bytes = Message.TypeSupport.Serialize(new Message(42, "text", 7));
            for (int i = 0; i < bytes.Length; i++) { var truncated = bytes[..i]; Throws<InvalidDataException>(() => Message.TypeSupport.Deserialize(truncated)); }
            Throws<InvalidDataException>(() => Message.TypeSupport.Deserialize([0, 99, 0, 0]));
            Throws<InvalidDataException>(() => Message.TypeSupport.Deserialize([0, 1, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255]));
        });
        Run("CDR.mutable-evolution-and-must-understand", () =>
        {
            byte[] original = Mutable.TypeSupport.Serialize(new Mutable { id = 19, text = "known" });
            byte[] extended = new byte[original.Length + 12]; original.CopyTo(extended, 0);
            int offset = (original.Length + 3) & ~3;
            if (offset != original.Length) Array.Resize(ref extended, offset + 12);
            BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(4), (uint)(extended.Length - 8));
            BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(offset), 0x40000077u);
            BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(offset + 4), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(offset + 8), 123);
            Equal(19, Mutable.TypeSupport.Deserialize(extended).id);
            BinaryPrimitives.WriteUInt32LittleEndian(extended.AsSpan(offset), 0xc0000077u);
            Throws<InvalidDataException>(() => Mutable.TypeSupport.Deserialize(extended));
        });
        Run("CDR.appendable-evolution", () =>
        {
            byte[] bytes = Optional.TypeSupport.Serialize(new Optional { id = 23 });
            Array.Resize(ref bytes, bytes.Length + 4);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(bytes.Length - 8));
            Equal(23, Optional.TypeSupport.Deserialize(bytes).id);
            byte[] older = [0, 9, 0, 0, 4, 0, 0, 0, 23, 0, 0, 0];
            var result = Optional.TypeSupport.Deserialize(older);
            Equal(23, result.id); True(result.number is null && result.point is null);
        });
        Run("CDR.constructor-owned-children", () =>
        {
            var source = NestedValue(true);
            byte[] bytes = NestedDefaults.TypeSupport.Serialize(source);
            int before = Point.ConstructorCalls;
            var copy = NestedDefaults.TypeSupport.Deserialize(bytes);
            Equal(3, Point.ConstructorCalls - before); Equal(source, copy);
        });
        Run("CDR.parallel-codec-use", () =>
        {
            var source = MakeContainers();
            Parallel.For(0, 64, i => { var bytes = Containers.TypeSupport.Serialize(source, (i & 1) == 0 ? DataRepresentationKind.Xcdr1 : DataRepresentationKind.Xcdr2); Equal(source, Containers.TypeSupport.Deserialize(bytes)); });
        });
        Run("CDR.native-sample-marshalling-is-bypassed", () =>
        {
            using var participant = Participant(); using var metadataTopic = participant.CreateTopic(Name(), Message.TypeSupport);
            var codec = new DelegateCodec<Message>(Message.__Codec.Size,
                _ => throw new Exception("Native sample decoding was used."),
                (_, _, _) => throw new Exception("Native sample encoding was used."))
                .WithCdr(new CdrStructCodec<Message>(() => new(), CdrExtensibility.Final,
                    new CdrMember<Message, int>(0, true, false, false, Codecs.Int32, v => v.id, (v, f) => v.id = f),
                    new CdrMember<Message, string>(1, false, false, false, new StringCodec(), v => v.text, (v, f) => v.text = f),
                    new CdrMember<Message, int>(2, false, false, false, Codecs.Int32, v => v.value, (v, f) => v.value = f)));
            var support = new TypeSupport<Message>("Test::Message", Message.TypeSupport.Descriptor, codec, () => new());
            using var topic = participant.CreateTopic(Name(), support); using var reader = participant.CreateReader(topic, Reliable); using var writer = participant.CreateWriter(topic, Reliable);
            Match(writer, reader); var message = new Message(12, "managed", 34);
            writer.Write(message); Equal(message, TakeOne(reader).Data);
            var timestamp = Timestamp.Now; writer.Write(message, timestamp); Equal(timestamp, TakeOne(reader).Info.SourceTimestamp);
            writer.WriteDispose(message); Equal(message, TakeOne(reader).Data);
            Throws<DdsException>(() => writer.Write(message, new Timestamp(-1)));
        });
        Run("CDR.direct-mixed-representation-rejected", () =>
        {
            using var participant = Participant(); using var topic = participant.CreateTopic(Name(), DirectMixed.TypeSupport);
            var qos = Reliable with { DataRepresentation = [DataRepresentationKind.Xcdr1] };
            Throws<DdsException>(() => participant.CreateReader(topic, qos));
        });
        Run("CDR.direct-cycles-and-depth", () =>
        {
            var cyclic = new DirectRecursive(); cyclic.children.Add(cyclic);
            Throws<ArgumentException>(() => DirectRecursive.TypeSupport.Serialize(cyclic));
            var root = new DirectRecursive(); var leaf = root;
            for (int i = 0; i < 130; i++) { var child = new DirectRecursive(); leaf.children.Add(child); leaf = child; }
            Throws<ArgumentException>(() => DirectRecursive.TypeSupport.Serialize(root));
        });
        Run("CDR.direct-null-child-read-and-write", () =>
        {
            var source = NestedValue(true);
            using var output = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final);
            var pair = new PointPair { first = null!, second = new Point() };
            Throws<ArgumentNullException>(() => PointPair.__WriteCdr(output, pair));
            // A failed child write must release both scopes so another write can enter this object.
            pair.first = new Point(); PointPair.__WriteCdr(output, pair);
            using var encoded = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final);
            PointPair.__WriteCdr(encoded, source.pair);
            byte[] data = encoded.ToArray();
            var result = PointPair.__ReadCdr(new CdrReader(data, data.Length), new PointPair { first = null!, second = null! });
            Equal(source.pair, result);
        });
        Run("CDR.writer-capacity-hint-and-disposal", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final, 3));
            byte[] expected = Message.TypeSupport.Serialize(new Message(2, new string('x', 16384), 3), DataRepresentationKind.Xcdr1);
            using var stream = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final, expected.Length);
            Message.__WriteCdr(stream, new Message(2, new string('x', 16384), 3)); True(expected.SequenceEqual(stream.ToArray()));
            stream.Dispose(); Throws<ObjectDisposedException>(() => stream.Scalar(42));
        });
        Run("CDR.writer-changing-sample-sizes", () => Endpoints(Message.TypeSupport, (_, _, writer, reader) =>
        {
            foreach (int length in new[] { 8, 32768, 8, 1024, 65536, 16 })
            {
                var value = new Message(length, new string('x', length), -length);
                writer.Write(value); Equal(value, TakeOne(reader).Data);
            }
        }));
        Run("CDR.validation-and-cycles", () =>
        {
            var value = new Recursive(); value.child = value;
            Throws<ArgumentException>(() => Recursive.TypeSupport.Serialize(value));
            Throws<ArgumentException>(() => Message.TypeSupport.Serialize(new Message(1, "x\0y", 0)));
            var containers = MakeContainers(); containers.label = new string('ä', 9);
            Throws<ArgumentOutOfRangeException>(() => Containers.TypeSupport.Serialize(containers));
        });
    }
    private static Choice UnionSequence() { var value = new Choice(); value.Setnumbers([1, 2, 3]); return value; }
    private static void CheckCdr<T>(TypeSupport<T> support, T sample, DataRepresentationKind representation) where T : class
    {
        using var participant = Participant(); using var topic = participant.CreateTopic(Name(), support);
        var qos = Reliable with { DataRepresentation = [representation] };
        using var reader = participant.CreateReader(topic, qos); using var writer = participant.CreateWriter(topic, qos);
        Match(writer, reader);
        byte[] bytes = support.Serialize(sample, representation);
        Equal(sample, support.Deserialize(bytes));
        using var arena = new NativeArena();
        var native = arena.Allocate(support.Codec.Size); support.Codec.Write(native, sample, arena);
        foreach (uint force in new[] { 0u, 1u })
        {
            var expectedHash = new byte[16]; var actualHash = new byte[16];
            Equal(0, CdrOracle.oracle_encode(CdrOracle.Handle(writer), native, out nint buffer, out uint size, expectedHash, force));
            try
            {
                var reference = new byte[size]; Marshal.Copy(buffer, reference, 0, checked((int)size));
                Equal(sample, support.Deserialize(reference));
            }
            finally { CdrOracle.oracle_free(buffer); }
            var decoded = arena.Allocate(support.Codec.Size);
            try
            {
                Equal(0, CdrOracle.oracle_decode(CdrOracle.Handle(writer), bytes, checked((uint)bytes.Length), decoded, actualHash, force));
                Equal(sample, support.Codec.Read(decoded));
                True(expectedHash.SequenceEqual(actualHash));
            }
            finally { CdrOracle.oracle_fini(decoded, support.Descriptor); }
        }
        writer.Write(sample); Equal(sample, TakeOne(reader).Data);
        ulong handle = writer.LookupInstance(sample);
        writer.DisposeInstance(sample); var disposed = TakeOne(reader, false); True(!disposed.Info.ValidData);
        Equal(handle, writer.LookupInstance(disposed.Data));
    }
}
