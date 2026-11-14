// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using Omg.Types;
using Org.Eclipse.CycloneDDS;
using Test;

internal static partial class Program
{
    private static NumericCollections MakeNumericCollections()
    {
        var value = new NumericCollections { id = 4, prefix = 1, suffix = 2 };
        value.bytes.AddRange([0, 1, 255]); value.signed_bytes.AddRange([sbyte.MinValue, 0, sbyte.MaxValue]);
        value.shorts.AddRange([short.MinValue, 0, short.MaxValue]); value.ushorts.AddRange([0, 1, ushort.MaxValue]);
        value.ints.AddRange([int.MinValue, 0, int.MaxValue]); value.uints.AddRange([0, 1, uint.MaxValue]);
        value.longs.AddRange([long.MinValue, 0, long.MaxValue]); value.ulongs.AddRange([0, 1, ulong.MaxValue]);
        value.singles.AddRange([-1.25f, float.Epsilon, float.PositiveInfinity]); value.doubles.AddRange([-2.5, double.Epsilon, double.NegativeInfinity]);
        value.fixed_ints = [-1, 0, int.MaxValue]; value.fixed_doubles = [-1.25, 0, double.Epsilon]; value.matrix[1, 2] = Math.PI;
        return value;
    }
    private static void RegisterCdrBlockTests()
    {
        CdrNumeric<byte>("byte", Codecs.Byte, [0, 1, 255]);
        CdrNumeric<sbyte>("sbyte", Codecs.SByte, [sbyte.MinValue, 0, sbyte.MaxValue]);
        CdrNumeric<short>("int16", Codecs.Int16, [short.MinValue, 0, short.MaxValue]);
        CdrNumeric<ushort>("uint16", Codecs.UInt16, [0, 1, ushort.MaxValue]);
        CdrNumeric<int>("int32", Codecs.Int32, [int.MinValue, 0, int.MaxValue]);
        CdrNumeric<uint>("uint32", Codecs.UInt32, [0, 1, uint.MaxValue]);
        CdrNumeric<long>("int64", Codecs.Int64, [long.MinValue, 0, long.MaxValue]);
        CdrNumeric<ulong>("uint64", Codecs.UInt64, [0, 1, ulong.MaxValue]);
        CdrNumeric<float>("float32", Codecs.Float32, [BitConverter.Int32BitsToSingle(unchecked((int)0xffc01234)), -0f, float.Epsilon]);
        CdrNumeric<double>("float64", Codecs.Float64, [BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000001234)), -0d, double.Epsilon]);
        Run("CdrBlocks.empty-wide-sequence-does-not-align", () =>
        {
            foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
            {
                using var writer = new CdrWriter(representation, CdrExtensibility.Final);
                var codec = new SequenceCodec<double>(Codecs.Float64); codec.WriteCdr(writer, new Sequence<double>()); writer.Scalar((byte)99);
                byte[] bytes = writer.ToArray(); Equal(9, bytes.Length);
                var reader = new CdrReader(bytes, bytes.Length); Equal(0, codec.ReadCdr(reader).Count); Equal((byte)99, reader.Scalar<byte>());
            }
        });
        Run("CdrBlocks.bounded-and-malformed-counts", () =>
        {
            var codec = new SequenceCodec<long>(Codecs.Int64, 2);
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final);
            Throws<ArgumentOutOfRangeException>(() => codec.WriteCdr(writer, new Sequence<long>([1, 2, 3])));
            byte[] oversized = [0, 1, 0, 0, 255, 255, 255, 127, 0, 0, 0, 0];
            Throws<InvalidDataException>(() => new SequenceCodec<long>(Codecs.Int64).ReadCdr(new CdrReader(oversized, oversized.Length)));
        });
        Run("CdrBlocks.numeric-array-and-independent-copy", () =>
        {
            var input = new double[] { -1, 0, 3.5 }; var codec = new ArrayCodec<double[], double>(Codecs.Float64, 3);
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr2, CdrExtensibility.Final); codec.WriteCdr(writer, input);
            byte[] bytes = writer.ToArray(); var result = codec.ReadCdr(new CdrReader(bytes, bytes.Length));
            True(input.SequenceEqual(result)); result[0] = 99; Equal(-1.0, input[0]);
        });
        Run("CdrBlocks.rectangular-numeric-arrays", () =>
        {
            var input = new double[2, 2, 3];
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) for (int k = 0; k < 3; k++) input[i, j, k] = (i * 6 + j * 3 + k) * .25 - 2;
            var codec = new ArrayCodec<double[,,], double>(Codecs.Float64, 2, 2, 3);
            foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
            {
                using var writer = new CdrWriter(representation, CdrExtensibility.Final); codec.WriteCdr(writer, input);
                byte[] bytes = writer.ToArray(); bytes[1] &= 0xfe;
                for (int i = 0; i < input.Length; i++) Array.Reverse(bytes, 4 + i * 8, 8);
                var result = codec.ReadCdr(new CdrReader(bytes, bytes.Length));
                True(input.Cast<double>().SequenceEqual(result.Cast<double>())); result[0, 0, 0] = 99; Equal(-2.0, input[0, 0, 0]);
            }
        });
        Run("CdrBlocks.custom-sequence-indexer", () =>
        {
            var input = DispatchProxy.Create<ISequence<int>, SequenceProxy>(); ((SequenceProxy)(object)input).Values = new([1, 2, 3]);
            var codec = new SequenceCodec<int>(Codecs.Int32);
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final); codec.WriteCdr(writer, input);
            byte[] bytes = writer.ToArray(); True(codec.ReadCdr(new CdrReader(bytes, bytes.Length)).SequenceEqual(new[] { 101, 102, 103 }));
        });
        Run("CdrBlocks.custom-codec-semantics", () =>
        {
            var custom = new OffsetCdr();
            var element = new DelegateCodec<int>(4, p => Marshal.ReadInt32(p), (p, value, _) => Marshal.WriteInt32(p, value)).WithCdr(custom);
            var codec = new SequenceCodec<int>(element);
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final); codec.WriteCdr(writer, new Sequence<int>([1, 2, 3]));
            byte[] bytes = writer.ToArray(); True(codec.ReadCdr(new CdrReader(bytes, bytes.Length)).SequenceEqual(new[] { 1, 2, 3 }));
            Equal(3, custom.Writes); Equal(3, custom.Reads);
        });
        Run("CdrBlocks.reject-nonnumeric-bulk-types", () =>
        {
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final);
            Throws<NotSupportedException>(() => writer.WritePrimitives<bool>(new bool[] { true }));
            byte[] bytes = [0, 1, 0, 0]; var reader = new CdrReader(bytes, bytes.Length);
            Throws<NotSupportedException>(() => reader.ReadPrimitives<char>(new char[1]));
            Throws<ArgumentOutOfRangeException>(() => writer.WriteBlock(8, 3));
            Throws<ArgumentOutOfRangeException>(() => reader.ReadBlock(-1, 4));
        });
        Run("CdrBlocks.generated-big-endian-and-misalignment", () =>
        {
            foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
            {
                var point = new Point { x = -1.25, y = Math.PI };
                using var writer = new CdrWriter(representation, CdrExtensibility.Final); writer.Scalar((byte)7); Point.__WriteCdr(writer, point); writer.Scalar((byte)9);
                byte[] bytes = writer.ToArray(); bytes[1] &= 0xfe;
                int start = representation == DataRepresentationKind.Xcdr1 ? 12 : 8;
                Array.Reverse(bytes, start, 8); Array.Reverse(bytes, start + 8, 8);
                var reader = new CdrReader(bytes, bytes.Length); Equal((byte)7, reader.Scalar<byte>());
                Equal(point, Point.__ReadCdr(reader, null)); Equal((byte)9, reader.Scalar<byte>());
                byte[] truncated = bytes[..(start + 15)];
                var shortReader = new CdrReader(truncated, truncated.Length); shortReader.Scalar<byte>();
                Throws<InvalidDataException>(() => Point.__ReadCdr(shortReader, null));
            }
        });
        Run("CdrBlocks.mixed-scalar-block-big-endian", () =>
        {
            var expected = new ScalarBlocks { id = 7, a = 1, b = 255, c = -127, d = short.MinValue, e = ushort.MaxValue, f = int.MinValue, g = uint.MaxValue, h = -1.25f, x = -2.5, y = double.Epsilon, i = long.MinValue, j = ulong.MaxValue };
            foreach (byte identifier in new byte[] { 0, 6 })
            {
                var bytes = new byte[60]; bytes[1] = identifier;
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), expected.id);
                bytes[8] = expected.a; bytes[9] = expected.b; bytes[10] = unchecked((byte)expected.c);
                BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(12), expected.d); BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), expected.e);
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), expected.f); BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), expected.g);
                BinaryPrimitives.WriteSingleBigEndian(bytes.AsSpan(24), expected.h);
                BinaryPrimitives.WriteDoubleBigEndian(bytes.AsSpan(28), expected.x); BinaryPrimitives.WriteDoubleBigEndian(bytes.AsSpan(36), expected.y);
                BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(44), expected.i); BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(52), expected.j);
                Equal(expected, ScalarBlocks.TypeSupport.Deserialize(bytes));
            }
        });
        Run("CdrBlocks.depth-independent-of-cycle-tracking", () =>
        {
            using var writer = new CdrWriter(DataRepresentationKind.Xcdr1, CdrExtensibility.Final);
            var scopes = new List<CdrWriter.StructureScope>();
            try
            {
                for (int i = 0; i < 256; i++) scopes.Add(writer.BeginStructure(new object(), CdrExtensibility.Final, false));
                Throws<ArgumentException>(() => writer.BeginStructure(new object(), CdrExtensibility.Final, false));
            }
            finally { for (int i = scopes.Count - 1; i >= 0; i--) scopes[i].Dispose(); }
            using var after = writer.BeginStructure(new object(), CdrExtensibility.Final, false);
        });
        Run("CdrBlocks.mutual-recursion-and-shared-children", () =>
        {
            var root = new MutualA { id = 1 }; var child = new MutualB(); root.children.Add(child); child.parents.Add(root);
            Throws<ArgumentException>(() => MutualA.TypeSupport.Serialize(root));
            child.parents.Clear(); var shared = new MutualA { id = 2 }; child.parents.Add(shared); child.parents.Add(shared);
            var decoded = MutualA.TypeSupport.Deserialize(MutualA.TypeSupport.Serialize(root));
            Equal(root, decoded); True(!ReferenceEquals(decoded.children[0].parents[0], decoded.children[0].parents[1]));
        });
    }
    private static void CdrNumeric<T>(string name, Codec<T> element, T[] values) where T : unmanaged
        => Run("CdrBlocks.numeric-" + name, () =>
        {
            foreach (var representation in new[] { DataRepresentationKind.Xcdr1, DataRepresentationKind.Xcdr2 })
            foreach (bool prefix in new[] { false, true })
            {
                var codec = new SequenceCodec<T>(element);
                using var writer = new CdrWriter(representation, CdrExtensibility.Final);
                if (prefix) writer.Scalar((byte)7);
                codec.WriteCdr(writer, new Sequence<T>(values)); writer.Scalar((byte)9);
                byte[] bytes = writer.ToArray();
                foreach (bool bigEndian in new[] { false, true })
                {
                    byte[] data = (byte[])bytes.Clone();
                    if (bigEndian)
                    {
                        data[1] &= 0xfe; int countOffset = prefix ? 8 : 4; Array.Reverse(data, countOffset, 4);
                        int size = Unsafe.SizeOf<T>(), start = countOffset + 4, alignment = Math.Min(size, representation == DataRepresentationKind.Xcdr1 ? 8 : 4);
                        start += (-(start - 4)) & (alignment - 1);
                        for (int i = 0; i < values.Length; i++) Array.Reverse(data, start + i * size, size);
                    }
                    var reader = new CdrReader(data, data.Length); if (prefix) Equal((byte)7, reader.Scalar<byte>());
                    T[] copy = codec.ReadCdr(reader).ToArray();
                    True(MemoryMarshal.AsBytes(values.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(copy.AsSpan()))); Equal((byte)9, reader.Scalar<byte>());
                }
            }
        });
    private sealed class OffsetCdr : CdrCodec<int>
    {
        internal int Writes, Reads;
        public override void Write(CdrWriter writer, int value) { Writes++; writer.Scalar(value + 100); }
        public override int Read(CdrReader reader) { Reads++; return reader.Scalar<int>() - 100; }
    }
}
