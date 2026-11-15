// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using Org.Eclipse.CycloneDDS;
using Test;

namespace Test
{
    public partial class Point
    {
        internal static int ConstructorCalls;
        internal int ConstructorId { get; } = Interlocked.Increment(ref ConstructorCalls);
    }
}

internal static partial class Program
{
    private static NestedDefaults NestedValue(bool optional = false)
    {
        var value = new NestedDefaults { id = 7 };
        value.pair.first.x = 1.25; value.pair.first.y = -2.5;
        value.pair.second.x = 3.75; value.pair.second.y = -4.25;
        if (optional) value.optional = new() { x = 5.5, y = -6.75 };
        return value;
    }

    private static void RegisterObjectReuseTests()
    {
        Run("ObjectReuse.generated-nested-constructor-count", () =>
        {
            var source = NestedValue();
            int before = Point.ConstructorCalls;
            var copy = NestedDefaults.TypeSupport.Clone(source);
            Equal(2, Point.ConstructorCalls - before);
            True(source.Equals(copy)); True(!ReferenceEquals(source.pair, copy.pair));
            True(!ReferenceEquals(source.pair.first, copy.pair.first));
            True(!ReferenceEquals(copy.pair.first, copy.pair.second));
            True(copy.pair.first.ConstructorId != copy.pair.second.ConstructorId);
        });
        Run("ObjectReuse.optional-null-and-present", () =>
        {
            foreach (bool present in new[] { false, true })
            {
                var source = NestedValue(present); int before = Point.ConstructorCalls;
                var copy = NestedDefaults.TypeSupport.Clone(source);
                Equal(present ? 3 : 2, Point.ConstructorCalls - before);
                True(source.Equals(copy));
                if (present) True(!ReferenceEquals(source.optional, copy.optional));
                else True(copy.optional is null);
            }
        });
        Run("ObjectReuse.results-are-independent", () =>
        {
            var source = NestedValue(true);
            var first = NestedDefaults.TypeSupport.Clone(source);
            var second = NestedDefaults.TypeSupport.Clone(source);
            first.pair.first.x = 999; first.optional!.y = 999;
            Equal(1.25, source.pair.first.x); Equal(1.25, second.pair.first.x);
            Equal(-6.75, source.optional!.y); Equal(-6.75, second.optional!.y);
        });
        Run("ObjectReuse.native-reader-samples-independent", () => Endpoints(NestedDefaults.TypeSupport, (p, t, w, r) =>
        {
            var value = NestedValue(true); w.Write(value); var first = TakeOne(r).Data;
            value.pair.first.x = 42; w.Write(value); var second = TakeOne(r).Data;
            Equal(1.25, first.pair.first.x); Equal(42.0, second.pair.first.x);
            second.pair.second.y = 999; Equal(-4.25, first.pair.second.y);
        }));
        Run("ObjectReuse.null-child-falls-back-to-construction", () =>
        {
            var codec = new StructCodec<PointBox>(Point.__Codec.Size, () => new(),
                new StructMemberCodec<PointBox, Point>(0, Point.__Codec, v => v.Value!, (v, child) => v.Value = child));
            var copy = Codecs.Clone(codec, new() { Value = new() { x = 2, y = 3 } });
            Equal(2.0, copy.Value!.x); Equal(3.0, copy.Value.y);
        });
        Run("ObjectReuse.custom-codec-keeps-read-semantics", () =>
        {
            int reads = 0;
            var element = new DelegateCodec<Point>(Point.__Codec.Size,
                pointer => { reads++; return Point.__Codec.Read(pointer); },
                (pointer, value, arena) => Point.__Codec.Write(pointer, value, arena));
            var codec = new StructCodec<PointBox>(element.Size, () => new() { Value = new() },
                new StructMemberCodec<PointBox, Point>(0, element, v => v.Value!, (v, child) => v.Value = child));
            var copy = Codecs.Clone(codec, new() { Value = new() { x = 4, y = 5 } });
            Equal(1, reads); Equal(4.0, copy.Value!.x); Equal(5.0, copy.Value.y);
        });
        Run("ObjectReuse.existing-member-codec-does-not-read-getter", () =>
        {
            var codec = new StructCodec<PointBox>(Point.__Codec.Size, () => new(),
                new MemberCodec<PointBox, Point>(0, Point.__Codec, _ => throw new Exception("Unexpected read getter"), (v, child) => v.Value = child));
            using var arena = new NativeArena(); var pointer = arena.Allocate(Point.__Codec.Size);
            Point.__Codec.Write(pointer, new() { x = 6, y = 7 }, arena);
            var result = codec.Read(pointer); Equal(6.0, result.Value!.x);
        });
    }

    private sealed class PointBox { internal Point? Value; }
}
