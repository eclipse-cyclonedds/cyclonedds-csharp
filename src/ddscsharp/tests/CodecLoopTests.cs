// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Runtime.InteropServices;
using System.Text;
using Omg.Types;
using Org.Eclipse.CycloneDDS;

internal static partial class Program
{
    private static void RegisterCodecLoopTests()
    {
        Run("CodecLoops.sequence-size-resolved-per-operation", () =>
        {
            var element = new CountingIntCodec(); var codec = new SequenceCodec<int>(element);
            using var arena = new NativeArena(); var pointer = arena.Allocate(codec.Size);
            codec.Write(pointer, new Sequence<int>([1, -2, 3]), arena); Equal(1, element.SizeReads);
            element.SizeReads = 0;
            True(codec.Read(pointer).SequenceEqual(new[] { 1, -2, 3 })); Equal(1, element.SizeReads);
        });
        Run("CodecLoops.empty-sequence-keeps-deferred-codec-lazy", () =>
        {
            bool ready = false;
            var deferred = new DeferredCodec<int>(() => ready ? Codecs.Int32 : throw new Exception("Resolved too early"));
            var codec = new SequenceCodec<int>(deferred);
            using var arena = new NativeArena(); var pointer = arena.Allocate(codec.Size);
            codec.Write(pointer, new Sequence<int>(), arena); Equal(0, codec.Read(pointer).Count);
            ready = true; codec.Write(pointer, new Sequence<int>([7, 8]), arena);
            True(codec.Read(pointer).SequenceEqual(new[] { 7, 8 }));
        });
        Run("CodecLoops.array-size-resolved-per-operation", () =>
        {
            var element = new CountingIntCodec(); var codec = new ArrayCodec<int[,], int>(element, 2, 3);
            using var arena = new NativeArena(); var pointer = arena.Allocate(6 * sizeof(int));
            var input = new int[,] { { 1, 2, 3 }, { 4, 5, 6 } };
            codec.Write(pointer, input, arena); Equal(1, element.SizeReads);
            element.SizeReads = 0;
            True(input.Cast<int>().SequenceEqual(codec.Read(pointer).Cast<int>())); Equal(1, element.SizeReads);
        });
        Run("CodecLoops.array-row-major-layout-and-shapes", () =>
        {
            var input = new int[2,3,5];
            for (int i=0;i<2;i++) for(int j=0;j<3;j++) for(int k=0;k<5;k++) input[i,j,k]=i*100+j*10+k;
            var codec = new ArrayCodec<int[,,],int>(Codecs.Int32,2,3,5);
            using var arena = new NativeArena(); var pointer = arena.Allocate(codec.Size);
            codec.Write(pointer,input,arena);
            for (int i=0;i<2;i++) for(int j=0;j<3;j++) for(int k=0;k<5;k++)
                Equal(input[i,j,k],Marshal.ReadInt32(pointer,((i*3+j)*5+k)*sizeof(int)));
            var copy = codec.Read(pointer); True(input.Cast<int>().SequenceEqual(copy.Cast<int>()));
            copy[1,2,4]=999; Equal(124,input[1,2,4]);
            True(new[]{1,-2,3}.SequenceEqual(Codecs.Clone(new ArrayCodec<int[],int>(Codecs.Int32,3),new[]{1,-2,3})));
            Equal(0,Codecs.Clone(new ArrayCodec<int[,],int>(Codecs.Int32,0,3),new int[0,3]).Length);
            Throws<ArgumentOutOfRangeException>(() => Codecs.Clone(codec,new int[2,3,4]));
        });
        Run("CodecLoops.array-index-buffer-is-per-call", () =>
        {
            var codec = new ArrayCodec<int[,],int>(Codecs.Int32,3,7);
            Parallel.For(0,32,iteration =>
            {
                var input = new int[3,7];
                for(int i=0;i<3;i++) for(int j=0;j<7;j++) input[i,j]=iteration*100+i*7+j;
                var copy = Codecs.Clone(codec,input);
                True(input.Cast<int>().SequenceEqual(copy.Cast<int>()));
            });
        });
        Run("CodecLoops.utf8-boundary-and-strict-validation", () =>
        {
            const string text="Aä€🌍";
            Equal(text,Codecs.Clone(new StringCodec(10),text));
            Equal(text,Codecs.Clone(new StringCodec(),text));
            Equal(string.Empty,Codecs.Clone(new StringCodec(),string.Empty));
            Throws<ArgumentOutOfRangeException>(()=>Codecs.Clone(new StringCodec(9),text));
            Throws<EncoderFallbackException>(()=>Codecs.Clone(new StringCodec(),"\ud800"));
            Throws<EncoderFallbackException>(()=>Codecs.Clone(new StringCodec(10),"\udc00"));
            Throws<ArgumentException>(()=>Codecs.Clone(new StringCodec(),"a\0b"));
        });
        Run("CodecLoops.utf8-native-buffer-guards", CheckStringGuards);
    }

    private static unsafe void CheckStringGuards()
    {
        var codec = new StringCodec(4);
        using var arena = new NativeArena(); var pointer = arena.Allocate(8+codec.Size+8);
        var memory = new Span<byte>((void*)pointer,8+codec.Size+8);
        foreach (string text in new[]{string.Empty,"x","ä","äé"})
        {
            memory.Fill(0xa5); codec.Write(pointer+8,text,arena);
            var expected=Encoding.UTF8.GetBytes(text);
            True(memory.Slice(8,expected.Length).SequenceEqual(expected)); Equal((byte)0,memory[8+expected.Length]);
            for(int i=0;i<8;i++) Equal((byte)0xa5,memory[i]);
            for(int i=8+expected.Length+1;i<memory.Length;i++) Equal((byte)0xa5,memory[i]);
            Equal(text,codec.Read(pointer+8));
        }
        memory.Fill(0xa5);
        Throws<ArgumentOutOfRangeException>(()=>codec.Write(pointer+8,"äéx",arena));
        for(int i=0;i<memory.Length;i++) Equal((byte)0xa5,memory[i]);
        Throws<EncoderFallbackException>(()=>codec.Write(pointer+8,"\ud800",arena));
        for(int i=0;i<memory.Length;i++) Equal((byte)0xa5,memory[i]);
    }

    private sealed class CountingIntCodec : Codec<int>
    {
        internal int SizeReads;
        public override int Size { get { SizeReads++; return sizeof(int); } }
        public override int Read(nint pointer) => Marshal.ReadInt32(pointer);
        public override void Write(nint pointer,int value,NativeArena arena) => Marshal.WriteInt32(pointer,value);
    }
}
