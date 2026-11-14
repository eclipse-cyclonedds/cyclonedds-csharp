// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace Org.Eclipse.CycloneDDS;

/// <summary>The XTypes encoding of an aggregate.</summary>
public enum CdrExtensibility { Final, Appendable, Mutable }

/// <summary>Generated wire codec, independent of the native sample layout.</summary>
public abstract class CdrCodec<T>(CdrExtensibility extensibility = CdrExtensibility.Final)
{
    public CdrExtensibility Extensibility { get; } = extensibility;
    internal virtual bool Primitive => false;
    public abstract void Write(CdrWriter writer, T value);
    public abstract T Read(CdrReader reader);
    internal virtual T ReadInto(CdrReader reader, T value) => Read(reader);
}

/// <summary>An operation-local, pooled XCDR output stream.</summary>
public sealed class CdrWriter : IDisposable
{
    private byte[] buffer;
    private readonly HashSet<object> path = new(ReferenceEqualityComparer.Instance);
    internal int Position = 4, Origin = 4;
    public bool Xcdr2 { get; }
    internal byte[] Buffer => buffer;
    internal int Length => Position;
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    public CdrWriter(DataRepresentationKind representation, CdrExtensibility extensibility)
        : this(representation, extensibility, 256) { }
    /// <summary>Starts with a capacity hint; the stream still grows and checks every write.</summary>
    public CdrWriter(DataRepresentationKind representation, CdrExtensibility extensibility, int initialCapacity)
    {
        if (representation is not (DataRepresentationKind.Xcdr1 or DataRepresentationKind.Xcdr2)) throw new ArgumentOutOfRangeException(nameof(representation));
        if (!Enum.IsDefined(extensibility)) throw new ArgumentOutOfRangeException(nameof(extensibility));
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 4);
        buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        Xcdr2 = representation == DataRepresentationKind.Xcdr2;
        buffer.AsSpan(0, 4).Clear();
        buffer[1] = (byte)(Xcdr2 ? extensibility switch { CdrExtensibility.Final => 7, CdrExtensibility.Appendable => 9, _ => 11 } : extensibility == CdrExtensibility.Mutable ? 3 : 1);
    }
    private void Ensure(int size)
    {
        ObjectDisposedException.ThrowIf(buffer.Length == 0, this);
        int length = checked(Position + size);
        if (length <= buffer.Length) return;
        var next = ArrayPool<byte>.Shared.Rent(Math.Max(length, checked(buffer.Length * 2)));
        buffer.AsSpan(0, Position).CopyTo(next);
        ArrayPool<byte>.Shared.Return(buffer); buffer = next;
    }
    internal void Align(int size)
    {
        int padding = (-(Position - Origin)) & (Math.Min(size, Xcdr2 ? 4 : 8) - 1);
        Ensure(padding); buffer.AsSpan(Position, padding).Clear(); Position += padding;
    }
    public void Scalar<T>(T value) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        int padding = (-(Position - Origin)) & (Math.Min(size, Xcdr2 ? 4 : 8) - 1);
        Ensure(checked(padding + size));
        buffer.AsSpan(Position, padding).Clear(); Position += padding;
        MemoryMarshal.Write(buffer.AsSpan(Position, size), in value);
        if (!BitConverter.IsLittleEndian) buffer.AsSpan(Position, size).Reverse();
        Position += size;
    }
    public void Boolean(bool value) => Scalar(value ? (byte)1 : (byte)0);
    public void Char(char value)
    {
        if (value > 255) throw new ArgumentOutOfRangeException(nameof(value), "IDL char is an 8-bit value.");
        Scalar((byte)value);
    }
    /// <summary>Begins a generated aggregate while preserving cycle checks and framing.</summary>
    public StructureScope BeginStructure(object value, CdrExtensibility extensibility)
    {
        Enter(value);
        try { return new StructureScope(this, value, Xcdr2 && extensibility != CdrExtensibility.Final ? BeginDelimited() : -1); }
        catch { Leave(value); throw; }
    }
    public readonly struct StructureScope : IDisposable
    {
        private readonly CdrWriter writer;
        private readonly object value;
        private readonly int delimiter;
        internal StructureScope(CdrWriter writer, object value, int delimiter) { this.writer = writer; this.value = value; this.delimiter = delimiter; }
        public void Dispose() { try { writer.EndDelimited(delimiter); } finally { writer.Leave(value); } }
    }
    internal void Patch(int position, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(position, 4), value);
    internal int BeginDelimited() { Scalar(0u); return Position; }
    internal int BeginCollection(bool primitive) => Xcdr2 && !primitive ? BeginDelimited() : -1;
    internal void EndDelimited(int start) { if (start >= 0) Patch(start - 4, checked((uint)(Position - start))); }
    public void String(string value, int bound = 0)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0')) throw new ArgumentException("IDL strings cannot contain embedded NUL.", nameof(value));
        int size = Utf8.GetByteCount(value);
        if (bound > 0 && size > bound) throw new ArgumentOutOfRangeException(nameof(value));
        Scalar(checked((uint)size + 1)); Ensure(checked(size + 1));
        Utf8.GetBytes(value.AsSpan(), buffer.AsSpan(Position, size)); Position += size; buffer[Position++] = 0;
    }
    internal void Enter(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (path.Count >= 256 || !path.Add(value)) throw new ArgumentException("DDS samples must be acyclic and at most 256 objects deep.");
    }
    internal void Leave(object value) => path.Remove(value);
    public byte[] ToArray() { ObjectDisposedException.ThrowIf(buffer.Length == 0, this); return buffer.AsSpan(0, Position).ToArray(); }
    public void Dispose() { var old = buffer; buffer = []; if (old.Length != 0) ArrayPool<byte>.Shared.Return(old); }
}

/// <summary>A bounds-checked XCDR input stream. Encapsulation determines byte order and alignment.</summary>
public sealed class CdrReader
{
    private readonly byte[] buffer;
    private readonly bool littleEndian;
    internal int Position = 4, Origin = 4, Limit;
    private int depth;
    public bool Xcdr2 { get; }
    public bool KeyOnly { get; }
    public CdrReader(byte[] data, int length) : this(data, length, false) { }
    internal CdrReader(byte[] data, int length, bool keyOnly)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (length < 4 || length > data.Length || data[0] != 0 || data[1] is not (0 or 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11)) throw new InvalidDataException("Invalid DDS encapsulation.");
        buffer = data; Limit = length; littleEndian = (data[1] & 1) != 0; Xcdr2 = data[1] >= 6; KeyOnly = keyOnly;
        int padding = data[3] & 3;
        if (padding > length - 4) throw new InvalidDataException("Invalid DDS padding.");
        Limit -= padding;
    }
    internal void CheckEncapsulation(CdrExtensibility extensibility)
    {
        int encoding = buffer[1] & ~1;
        int expected = Xcdr2 ? extensibility switch { CdrExtensibility.Final => 6, CdrExtensibility.Appendable => 8, _ => 10 } : extensibility == CdrExtensibility.Mutable ? 2 : 0;
        if (encoding != expected) throw new InvalidDataException("CDR encapsulation does not match the topic's extensibility.");
    }
    internal void Finish() { if (Position != Limit) throw new InvalidDataException("Trailing CDR data."); }
    internal void Require(int size) { if (size < 0 || size > Limit - Position) throw new InvalidDataException("Truncated or invalid CDR data."); }
    internal void Align(int size) { int n = (-(Position - Origin)) & (Math.Min(size, Xcdr2 ? 4 : 8) - 1); Require(n); Position += n; }
    public T Scalar<T>() where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>(); Align(size); Require(size);
        T result;
        if (littleEndian == BitConverter.IsLittleEndian) result = MemoryMarshal.Read<T>(buffer.AsSpan(Position, size));
        else { Span<byte> bytes = stackalloc byte[size]; buffer.AsSpan(Position, size).CopyTo(bytes); bytes.Reverse(); result = MemoryMarshal.Read<T>(bytes); }
        Position += size; return result;
    }
    public bool Boolean() => Scalar<byte>() switch { 0 => false, 1 => true, _ => throw new InvalidDataException("Invalid boolean.") };
    public char Char() => (char)Scalar<byte>();
    /// <summary>Bounds and nesting scope for a generated aggregate.</summary>
    public Scope BeginStructure(CdrExtensibility extensibility) => Enter(Xcdr2 && extensibility != CdrExtensibility.Final);
    internal uint PeekUInt32() { int saved = Position; uint n = Scalar<uint>(); Position = saved; return n; }
    internal int Count(int bound)
    {
        uint count = Scalar<uint>();
        if (count > int.MaxValue || (bound > 0 && count > bound) || count > Limit - Position) throw new InvalidDataException("Invalid sequence length.");
        return (int)count;
    }
    public string String(int bound = 0)
    {
        uint n = Scalar<uint>();
        if (n == 0 || n > int.MaxValue || (bound > 0 && n - 1 > bound)) throw new InvalidDataException("Invalid string length.");
        int size = (int)n; Require(size);
        if (buffer[Position + size - 1] != 0 || buffer.AsSpan(Position, size - 1).Contains((byte)0)) throw new InvalidDataException("Invalid IDL string.");
        string result = CdrWriter.Utf8.GetString(buffer, Position, size - 1); Position += size; return result;
    }
    internal Scope Collection(bool primitive) => Enter(Xcdr2 && !primitive);
    internal Scope Enter(bool delimited)
    {
        if (++depth > 256) throw new InvalidDataException("CDR nesting exceeds 256 levels.");
        int parent = Limit;
        if (delimited) { uint n = Scalar<uint>(); if (n > int.MaxValue) throw new InvalidDataException("Invalid delimiter."); Require((int)n); Limit = checked(Position + (int)n); }
        return new Scope(this, parent, delimited);
    }
    public readonly struct Scope : IDisposable
    {
        private readonly CdrReader reader;
        private readonly int parent;
        private readonly bool delimited;
        internal Scope(CdrReader reader, int parent, bool delimited) { this.reader = reader; this.parent = parent; this.delimited = delimited; }
        public void Dispose() { if (delimited) reader.Position = reader.Limit; reader.Limit = parent; reader.depth--; }
    }
}

internal sealed class CdrBooleanCodec : CdrCodec<bool>
{
    internal override bool Primitive => true;
    public override void Write(CdrWriter writer, bool value) => writer.Scalar(value ? (byte)1 : (byte)0);
    public override bool Read(CdrReader reader) => reader.Scalar<byte>() switch { 0 => false, 1 => true, _ => throw new InvalidDataException("Invalid boolean.") };
}
internal sealed class CdrCharCodec : CdrCodec<char>
{
    internal override bool Primitive => true;
    public override void Write(CdrWriter writer, char value) { if (value > 255) throw new ArgumentOutOfRangeException(nameof(value)); writer.Scalar((byte)value); }
    public override char Read(CdrReader reader) => (char)reader.Scalar<byte>();
}
internal sealed class CdrEnumCodec<T>(int bitBound, bool bitmask = false) : CdrCodec<T> where T : struct, Enum
{
    private readonly int width = bitBound is > 0 and <= 64 ? bitBound <= 8 ? 1 : bitBound <= 16 ? 2 : bitBound <= 32 ? 4 : 8 : throw new ArgumentOutOfRangeException(nameof(bitBound));
    private static readonly ulong AllowedBits = Enum.GetValues<T>().Aggregate(0UL, (mask, value) => mask | (Enum.GetUnderlyingType(typeof(T)) == typeof(ulong) ? Convert.ToUInt64(value) : unchecked((ulong)Convert.ToInt64(value))));
    private bool Valid(T value) => bitmask ? (Convert.ToUInt64(value) & ~AllowedBits) == 0 : Enum.IsDefined(value);
    public override void Write(CdrWriter writer, T value)
    {
        if (!Valid(value)) throw new ArgumentOutOfRangeException(nameof(value), "Undefined IDL enum or bitmask value.");
        ulong n = bitmask ? Convert.ToUInt64(value) : unchecked((ulong)Convert.ToInt64(value));
        switch (width) { case 1: writer.Scalar((byte)n); break; case 2: writer.Scalar((ushort)n); break; case 4: writer.Scalar((uint)n); break; default: writer.Scalar(n); break; }
    }
    public override T Read(CdrReader reader)
    {
        ulong n = width switch { 1 => reader.Scalar<byte>(), 2 => reader.Scalar<ushort>(), 4 => reader.Scalar<uint>(), _ => reader.Scalar<ulong>() };
        T value = (T)Enum.ToObject(typeof(T), n);
        if (!Valid(value)) throw new InvalidDataException("Undefined IDL enum or bitmask value.");
        return value;
    }
}

/// <summary>A generated member's wire metadata.</summary>
public abstract class CdrMember<T>(uint id, bool key, bool optional, bool mustUnderstand)
{
    internal uint Id => id;
    internal bool Key => key;
    internal bool Optional => optional;
    internal bool MustUnderstand => mustUnderstand;
    internal abstract bool Present(T value);
    internal abstract void Write(CdrWriter writer, T value);
    internal abstract void Read(CdrReader reader, T value);
}
public sealed class CdrMember<T, TField>(uint id, bool key, bool optional, bool mustUnderstand, Codec<TField> codec, Func<T, TField> get, Action<T, TField> set)
    : CdrMember<T>(id, key, optional, mustUnderstand)
{
    internal override bool Present(T value) => get(value) is not null;
    internal override void Write(CdrWriter writer, T value) => codec.WriteCdr(writer, get(value));
    internal override void Read(CdrReader reader, T value) => set(value, codec.ReadCdr(reader));
}

/// <summary>Generated aggregate encoding with XTypes member IDs and optionality.</summary>
public sealed class CdrStructCodec<T>(Func<T> create, CdrExtensibility extensibility, params CdrMember<T>[] members) : CdrCodec<T>(extensibility) where T : class
{
    private readonly Dictionary<uint, CdrMember<T>> byId = members.ToDictionary(m => m.Id);
    private readonly bool hasKeys = members.Any(m => m.Key);
    public override void Write(CdrWriter writer, T value)
    {
        writer.Enter(value);
        try
        {
            int delimiter = writer.Xcdr2 && Extensibility != CdrExtensibility.Final ? writer.BeginDelimited() : -1;
            foreach (var member in members)
            {
                bool present = !member.Optional || member.Present(value);
                if (Extensibility == CdrExtensibility.Mutable && !present) continue;
                bool parameter = Extensibility == CdrExtensibility.Mutable || (member.Optional && !writer.Xcdr2);
                int lengthOffset = -1, savedOrigin = writer.Origin;
                if (parameter)
                {
                    writer.Align(4);
                    if (writer.Xcdr2) writer.Scalar((member.MustUnderstand ? 0x80000000u : 0u) | 0x40000000u | member.Id);
                    else { writer.Scalar((ushort)0x7f01); writer.Scalar((ushort)8); writer.Scalar((member.MustUnderstand ? 0x40000000u : 0u) | member.Id); }
                    writer.Scalar(0u); lengthOffset = writer.Position;
                    if (!writer.Xcdr2) writer.Origin = writer.Position;
                }
                else if (member.Optional) writer.Scalar(present ? (byte)1 : (byte)0);
                if (present) member.Write(writer, value);
                writer.EndDelimited(lengthOffset); writer.Origin = savedOrigin;
            }
            if (Extensibility == CdrExtensibility.Mutable && !writer.Xcdr2) { writer.Align(4); writer.Scalar((ushort)0x7f02); writer.Scalar((ushort)0); }
            writer.EndDelimited(delimiter);
        }
        finally { writer.Leave(value); }
    }
    public override T Read(CdrReader reader) => ReadInto(reader, create());
    internal override T ReadInto(CdrReader reader, T value)
    {
        using var scope = reader.Enter(reader.Xcdr2 && Extensibility != CdrExtensibility.Final);
        if (Extensibility == CdrExtensibility.Mutable)
        {
            var seen = new HashSet<uint>();
            bool ended = reader.Xcdr2;
            while (reader.Position < reader.Limit)
            {
                var parameter = ReadParameter(reader);
                if (parameter.EndList) { ended = true; break; }
                if (!seen.Add(parameter.Id)) throw new InvalidDataException("Duplicate CDR member ID.");
                ReadParameterValue(reader, value, byId.GetValueOrDefault(parameter.Id), parameter);
            }
            if (!ended) throw new InvalidDataException("Missing XCDR1 parameter-list terminator.");
        }
        else
        {
            foreach (var member in members)
            {
                if (reader.KeyOnly && hasKeys && !member.Key) continue;
                if (reader.Position == reader.Limit && Extensibility == CdrExtensibility.Appendable) break;
                if (member.Optional && !reader.Xcdr2) { var parameter = ReadParameter(reader); if (parameter.Id != member.Id || parameter.EndList) throw new InvalidDataException("Invalid optional member ID."); ReadParameterValue(reader, value, member, parameter); }
                else if (!member.Optional || reader.Scalar<byte>() switch { 0 => false, 1 => true, _ => throw new InvalidDataException("Invalid optional presence flag.") }) member.Read(reader, value);
            }
        }
        return value;
    }
    private readonly record struct Parameter(uint Id, int Length, bool MustUnderstand, bool EndList);
    private static Parameter ReadParameter(CdrReader reader)
    {
        reader.Align(4);
        if (reader.Xcdr2)
        {
            uint header = reader.Scalar<uint>(); int lc = (int)((header >> 28) & 7);
            uint length = lc switch { < 4 => 1u << lc, 4 => reader.Scalar<uint>(), 5 => checked(4 + reader.PeekUInt32()), 6 => checked(4 + 4 * reader.PeekUInt32()), _ => checked(4 + 8 * reader.PeekUInt32()) };
            if (length > int.MaxValue) throw new InvalidDataException("Invalid member length.");
            return new(header & 0x0fffffffu, (int)length, (header & 0x80000000u) != 0, false);
        }
        ushort id = reader.Scalar<ushort>(); uint size = reader.Scalar<ushort>();
        uint mid = (uint)(id & 0x3fff); bool mu = (id & 0x4000) != 0;
        if (mid == 0x3f02) return new(0, 0, false, true);
        if (mid == 0x3f01) { if (size != 8) throw new InvalidDataException("Invalid extended member header."); uint extended = reader.Scalar<uint>(); mid = extended & 0x0fffffffu; mu = (extended & 0x40000000u) != 0; size = reader.Scalar<uint>(); }
        if (size > int.MaxValue) throw new InvalidDataException("Invalid member length.");
        return new(mid, (int)size, mu, false);
    }
    private static void ReadParameterValue(CdrReader reader, T value, CdrMember<T>? member, Parameter parameter)
    {
        reader.Require(parameter.Length);
        int end = checked(reader.Position + parameter.Length), savedOrigin = reader.Origin, limit = reader.Limit;
        reader.Limit = end;
        if (!reader.Xcdr2) reader.Origin = reader.Position;
        try
        {
            if (member is null) { if (parameter.MustUnderstand) throw new InvalidDataException("Unknown must-understand member."); }
            else if (parameter.Length != 0) member.Read(reader, value);
            else if (!member.Optional) throw new InvalidDataException("Empty required member.");
        }
        finally { reader.Position = end; reader.Limit = limit; reader.Origin = savedOrigin; }
    }
}

/// <summary>Generated union body; discriminator and branch encoding stay in managed code.</summary>
public sealed class CdrUnionCodec<T>(CdrExtensibility extensibility, Action<CdrWriter, T> write, Func<CdrReader, T> read) : CdrCodec<T>(extensibility) where T : class
{
    public override void Write(CdrWriter writer, T value)
    {
        writer.Enter(value);
        try { int end = writer.Xcdr2 && Extensibility == CdrExtensibility.Appendable ? writer.BeginDelimited() : -1; write(writer, value); writer.EndDelimited(end); }
        finally { writer.Leave(value); }
    }
    public override T Read(CdrReader reader) { using var scope = reader.Enter(reader.Xcdr2 && Extensibility == CdrExtensibility.Appendable); return read(reader); }
}

/// <summary>An active mutable union branch.</summary>
public sealed class CdrUnionMember<T, TField>(uint id, Codec<TField> codec, Func<T, TField> get, Action<T, TField> set, Func<T, bool> active)
    : CdrMember<T>(id, false, true, false)
{
    internal override bool Present(T value) => active(value);
    internal override void Write(CdrWriter writer, T value) => codec.WriteCdr(writer, get(value));
    internal override void Read(CdrReader reader, T value) => set(value, codec.ReadCdr(reader));
}

/// <summary>Reuses the generated result's constructor-owned struct child.</summary>
public sealed class CdrStructMember<T, TField>(uint id, bool key, bool optional, bool mustUnderstand, Codec<TField> codec, Func<T, TField> get, Action<T, TField> set)
    : CdrMember<T>(id, key, optional, mustUnderstand) where TField : class
{
    internal override bool Present(T value) => get(value) is not null;
    internal override void Write(CdrWriter writer, T value) => codec.WriteCdr(writer, get(value));
    internal override void Read(CdrReader reader, T value)
    {
        TField child = get(value);
        set(value, child is null ? codec.ReadCdr(reader) : codec.ReadCdrInto(reader, child));
    }
}

/// <summary>Generated field-specific operations with the generic codec retained for key-only samples.</summary>
public sealed class GeneratedCdrCodec<T>(CdrCodec<T> fallback, Action<CdrWriter, T> write, Func<CdrReader, T?, T> read)
    : CdrCodec<T>(fallback.Extensibility) where T : class
{
    public override void Write(CdrWriter writer, T value) => write(writer, value);
    public override T Read(CdrReader reader) => reader.KeyOnly ? fallback.Read(reader) : read(reader, null);
    internal override T ReadInto(CdrReader reader, T value) => reader.KeyOnly ? fallback.ReadInto(reader, value) : read(reader, value);
}
