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
using System.Runtime.InteropServices;
using Omg.Types;
using Org.Eclipse.CycloneDDS;

internal static partial class Program
{
    private static void RegisterSequenceCodecTests()
    {
        SequenceValues<byte>("byte", Codecs.Byte, [byte.MinValue, 1, byte.MaxValue]);
        SequenceValues<sbyte>("sbyte", Codecs.SByte, [sbyte.MinValue, 0, sbyte.MaxValue]);
        SequenceValues<short>("int16", Codecs.Int16, [short.MinValue, 0, short.MaxValue]);
        SequenceValues<ushort>("uint16", Codecs.UInt16, [ushort.MinValue, 1, ushort.MaxValue]);
        SequenceValues<int>("int32", Codecs.Int32, [int.MinValue, 0, int.MaxValue]);
        SequenceValues<uint>("uint32", Codecs.UInt32, [uint.MinValue, 1, uint.MaxValue]);
        SequenceValues<long>("int64", Codecs.Int64, [long.MinValue, 0, long.MaxValue]);
        SequenceValues<ulong>("uint64", Codecs.UInt64, [ulong.MinValue, 1, ulong.MaxValue]);
        SequenceValues<float>("float32", Codecs.Float32, [float.NegativeInfinity, -0f, float.Epsilon, float.PositiveInfinity, float.NaN]);
        SequenceValues<double>("float64", Codecs.Float64, [double.NegativeInfinity, -0d, double.Epsilon, double.PositiveInfinity, double.NaN]);
        Run("SequenceCodec.float-bit-patterns", () =>
        {
            var singles = new Sequence<float>(new[] { BitConverter.Int32BitsToSingle(unchecked((int)0xffc01234)), -0f });
            var doubles = new Sequence<double>(new[] { BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000001234)), -0d });
            var a = Codecs.Clone(new SequenceCodec<float>(Codecs.Float32), singles);
            var b = Codecs.Clone(new SequenceCodec<double>(Codecs.Float64), doubles);
            True(singles.Select(BitConverter.SingleToInt32Bits).SequenceEqual(a.Select(BitConverter.SingleToInt32Bits)));
            True(doubles.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(b.Select(BitConverter.DoubleToInt64Bits)));
        });
        Run("SequenceCodec.empty-and-bounds", () =>
        {
            var codec = new SequenceCodec<byte>(Codecs.Byte, 2);
            Equal(0, Codecs.Clone(codec, new Sequence<byte>()).Count);
            Equal(2, Codecs.Clone(codec, new Sequence<byte>([1, 2])).Count);
            Throws<ArgumentOutOfRangeException>(() => Codecs.Clone(codec, new Sequence<byte>([1, 2, 3])));
        });
        Run("SequenceCodec.independent-managed-copy", () =>
        {
            var input = new Sequence<byte>(Enumerable.Range(0, 65536).Select(i => (byte)i));
            var copy = Codecs.Clone(new SequenceCodec<byte>(Codecs.Byte), input);
            True(input.SequenceEqual(copy)); input[0] = 99; Equal((byte)0, copy[0]);
            copy[1] = 100; Equal((byte)1, input[1]);
        });
        Run("SequenceCodec.custom-codec-semantics", () =>
        {
            int writes = 0, reads = 0;
            var element = new DelegateCodec<int>(4,
                pointer => { reads++; return Marshal.ReadInt32(pointer) - 100; },
                (pointer, value, _) => { writes++; Marshal.WriteInt32(pointer, value + 100); });
            var copy = Codecs.Clone(new SequenceCodec<int>(element), new Sequence<int>([-7, 0, 300]));
            True(copy.SequenceEqual(new[] { -7, 0, 300 })); Equal(3, writes); Equal(3, reads);
        });
        Run("SequenceCodec.custom-sequence-indexer", () =>
        {
            var value = DispatchProxy.Create<ISequence<int>, SequenceProxy>();
            ((SequenceProxy)(object)value).Values = new([1, 2, 3]);
            var copy = Codecs.Clone(new SequenceCodec<int>(Codecs.Int32), value);
            True(copy.SequenceEqual(new[] { 101, 102, 103 }));
        });
        Run("SequenceCodec.converting-types", () =>
        {
            True(Codecs.Clone(new SequenceCodec<bool>(Codecs.Boolean), new Sequence<bool>([true, false, true])).SequenceEqual(new[] { true, false, true }));
            True(Codecs.Clone(new SequenceCodec<SmallEnum>(Codecs.Enum<SmallEnum>()), new Sequence<SmallEnum>([SmallEnum.One, SmallEnum.High])).SequenceEqual(new[] { SmallEnum.One, SmallEnum.High }));
            Throws<ArgumentOutOfRangeException>(() => Codecs.Clone(new SequenceCodec<char>(Codecs.Char), new Sequence<char>(['€'])));
        });
    }

    private static void SequenceValues<T>(string name, Codec<T> element, T[] values) => Run("SequenceCodec." + name, () =>
    {
        var codec = new SequenceCodec<T>(element);
        True(values.SequenceEqual(Codecs.Clone(codec, new Sequence<T>(values))));
        Equal(0, Codecs.Clone(codec, new Sequence<T>()).Count);
    });

    private enum SmallEnum : byte { One = 1, High = 255 }
    public class SequenceProxy : DispatchProxy
    {
        public Sequence<int> Values = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_Item") return Values[(int)args![0]!] + 100;
            return method.Invoke(Values, args);
        }
    }
}
