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

namespace Org.Eclipse.CycloneDDS;

public sealed partial class DataReader<T> : Entity where T : class
{
    private DataAvailableListener<T>? listener;
    public Topic<T> Topic { get; }
    internal DataReader(Entity parent, Topic<T> topic, Qos? qos) : base(parent, () =>
    {
        parent.RequireParticipant(topic);
        return Create(qos, q => Native.dds_create_reader(parent.Handle, topic.Handle, q, 0));
    }) => Topic = topic;

    public IReadOnlyList<Sample<T>> Read(int maxSamples = 1024, DataState? state = null, ulong instanceHandle = 0)
        => Access(false, maxSamples, state ?? DataState.Any, instanceHandle, null);
    public IReadOnlyList<Sample<T>> Take(int maxSamples = 1024, DataState? state = null, ulong instanceHandle = 0)
        => Access(true, maxSamples, state ?? DataState.Any, instanceHandle, null);
    public IReadOnlyList<Sample<T>> Read(ReadCondition condition, int maxSamples = 1024)
        => Access(false, maxSamples, condition.State, 0, condition);
    public IReadOnlyList<Sample<T>> Take(ReadCondition condition, int maxSamples = 1024)
        => Access(true, maxSamples, condition.State, 0, condition);

    private IReadOnlyList<Sample<T>> Access(bool take, int maximum, DataState state, ulong instance, ReadCondition? condition)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        lock (Gate)
        {
            _ = Handle;
            if (condition is not null && !ReferenceEquals(condition.Parent, this))
                throw new ArgumentException("The condition belongs to another reader.", nameof(condition));
            int reader = condition?.Handle ?? Handle;
            condition?.CheckError();
            bool serialized = Topic.Support.Codec.HasCdr;
            nint batch;
            int count = DdsException.Check(serialized
                ? Native.cs_read_cdr(reader, (uint)maximum, state.Mask, take ? 1u : 0u, instance, out batch)
                : Native.cs_read(reader, (uint)maximum, state.Mask, take ? 1u : 0u, instance, out batch), take ? "Take" : "Read");
            try
            {
                var result = new Sample<T>[count];
                for (uint i = 0; i < count; i++)
                {
                    var data = Native.cs_batch_get(batch, i, out var info);
                    // For lifecycle notifications only key fields are meaningful.
                    if (serialized)
                    {
                        int size = checked((int)Native.cs_cdr_size(batch, i));
                        var bytes = ArrayPool<byte>.Shared.Rent(size);
                        try
                        {
                            DdsException.Check(Native.cs_cdr_copy(batch, i, bytes, (uint)bytes.Length, out uint keyOnly), "ReadSerialized");
                            result[i] = new(keyOnly != 0 && size == 4 ? Topic.Support.Create() : Topic.Support.Codec.ReadCdr(new CdrReader(bytes, size, keyOnly != 0)), info);
                        }
                        finally { ArrayPool<byte>.Shared.Return(bytes); }
                    }
                    else result[i] = new(Topic.Support.Codec.Read(data), info);
                }
                return result;
            }
            finally { DdsException.Check(Native.cs_batch_free(batch), "ReturnLoan"); }
        }
    }
    public ReadCondition CreateReadCondition(DataState? state = null) => new(this, state ?? DataState.NewData);
    /// <summary>The filter executes on native DDS threads and must not call DDS operations.</summary>
    public QueryCondition<T> CreateQueryCondition(Func<T, bool> filter, DataState? state = null) => new(this, state ?? DataState.NewData, filter);
    /// <summary>Installs a native data-available listener; user callbacks run on the managed thread pool.</summary>
    public DataAvailableListener<T> Listen(Action<DataReader<T>> onDataAvailable)
    {
        ArgumentNullException.ThrowIfNull(onDataAvailable);
        lock (Gate)
        {
            _ = Handle;
            listener?.Dispose();
            listener = new(this, onDataAvailable);
            EnsureDataCondition();
            UpdateListenerMask();
            QueueAvailableData();
            return listener;
        }
    }
    internal override void BeforeDelete()
    {
        closed = true;
        listener?.Dispose();
        CompleteWaiters(false);
        endpointListener?.Dispose();
    }
    public IReadOnlyList<ulong> MatchedPublications
    {
        get
        {
            lock (Gate)
            {
                var size = DdsException.Check(Native.dds_get_matched_publications(Handle, null, 0), "MatchedPublications");
                if (size == 0) return [];
                var handles = new ulong[size];
                var count = DdsException.Check(Native.dds_get_matched_publications(Handle, handles, (nuint)size), "MatchedPublications");
                return handles.Take(Math.Min(size, count)).ToArray();
            }
        }
    }
}
