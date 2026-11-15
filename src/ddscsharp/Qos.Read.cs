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

namespace Org.Eclipse.CycloneDDS;

public sealed partial record Qos
{
    internal static Qos FromNative(nint p) => new()
    {
        Reliability = Native.dds_qget_reliability(p, out var r, out var blocking) ? new(r, new(blocking)) : null,
        History = Native.dds_qget_history(p, out var h, out var depth) ? new(h, depth) : null,
        Durability = Native.dds_qget_durability(p, out var durability) ? durability : null,
        ResourceLimits = Native.dds_qget_resource_limits(p, out var samples, out var instances, out var perInstance) ? new(samples, instances, perInstance) : null,
        Presentation = Native.dds_qget_presentation(p, out var scope, out var coherent, out var ordered) ? new(scope, coherent, ordered) : null,
        Lifespan = Native.dds_qget_lifespan(p, out var lifespan) ? new(lifespan) : null,
        Deadline = Native.dds_qget_deadline(p, out var deadline) ? new(deadline) : null,
        LatencyBudget = Native.dds_qget_latency_budget(p, out var latency) ? new(latency) : null,
        Ownership = Native.dds_qget_ownership(p, out var ownership) ? ownership : null,
        OwnershipStrength = Native.dds_qget_ownership_strength(p, out var strength) ? strength : null,
        Liveliness = Native.dds_qget_liveliness(p, out var liveliness, out var lease) ? new(liveliness, new(lease)) : null,
        TimeBasedFilter = Native.dds_qget_time_based_filter(p, out var separation) ? new(separation) : null,
        Partitions = ReadStrings(p, Native.dds_qget_partition),
        TransportPriority = Native.dds_qget_transport_priority(p, out var priority) ? priority : null,
        DestinationOrder = Native.dds_qget_destination_order(p, out var order) ? order : null,
        AutodisposeUnregisteredInstances = Native.dds_qget_writer_data_lifecycle(p, out var autodispose) ? autodispose : null,
        ReaderDataLifecycle = Native.dds_qget_reader_data_lifecycle(p, out var noWriters, out var disposed) ? new(new(noWriters), new(disposed)) : null,
        WriterBatching = Native.dds_qget_writer_batching(p, out var batching) ? batching : null,
        DurabilityService = Native.dds_qget_durability_service(p, out var cleanup, out var serviceHistory, out var serviceDepth,
            out var serviceSamples, out var serviceInstances, out var servicePerInstance)
            ? new(new(cleanup), new(serviceHistory, serviceDepth), new(serviceSamples, serviceInstances, servicePerInstance)) : null,
        IgnoreLocal = Native.dds_qget_ignorelocal(p, out var ignore) ? ignore : null,
        Properties = ReadProperties(p),
        BinaryProperties = ReadBinaryProperties(p),
        TypeConsistency = Native.dds_qget_type_consistency(p, out var consistency, out var sequences, out var strings,
            out var names, out var widening, out var validation) ? new(consistency, sequences, strings, names, widening, validation) : null,
        DataRepresentation = ReadRepresentations(p),
        EntityName = ReadString(p, Native.dds_qget_entity_name),
        PsmxInstances = ReadStrings(p, Native.dds_qget_psmx_instances),
        UserData = ReadBytes(p, Native.dds_qget_userdata),
        TopicData = ReadBytes(p, Native.dds_qget_topicdata),
        GroupData = ReadBytes(p, Native.dds_qget_groupdata)
    };

    private delegate bool StringsGetter(nint qos, out uint count, out nint values);
    private delegate bool BytesGetter(nint qos, out nint value, out nuint size);
    private delegate bool StringGetter(nint qos, out nint value);

    private static string TakeString(nint value)
    {
        try { return Marshal.PtrToStringUTF8(value) ?? string.Empty; }
        finally { Native.dds_free(value); }
    }

    private static string? ReadString(nint qos, StringGetter getter) => getter(qos, out var value) ? TakeString(value) : null;

    private static byte[] TakeBytes(nint value, nuint size)
    {
        try
        {
            var result = new byte[checked((int)size)];
            if (result.Length > 0) Marshal.Copy(value, result, 0, result.Length);
            return result;
        }
        finally { Native.dds_free(value); }
    }

    private static byte[]? ReadBytes(nint qos, BytesGetter getter) => getter(qos, out var value, out var size) ? TakeBytes(value, size) : null;

    private static string[]? ReadStrings(nint qos, StringsGetter getter)
    {
        if (!getter(qos, out var count, out var values)) return null;
        try
        {
            var result = new string[checked((int)count)];
            for (var i = 0; i < result.Length; i++) result[i] = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(values, checked(i * IntPtr.Size))) ?? string.Empty;
            return result;
        }
        finally
        {
            for (var i = 0u; i < count; i++) Native.dds_free(Marshal.ReadIntPtr(values, checked((int)i * IntPtr.Size)));
            Native.dds_free(values);
        }
    }

    private static DataRepresentationKind[]? ReadRepresentations(nint qos)
    {
        if (!Native.dds_qget_data_representation(qos, out var count, out var values)) return null;
        try
        {
            var raw = new short[checked((int)count)];
            if (raw.Length > 0) Marshal.Copy(values, raw, 0, raw.Length);
            return raw.Select(value => (DataRepresentationKind)value).ToArray();
        }
        finally { Native.dds_free(values); }
    }

    private static Property[]? ReadProperties(nint qos)
    {
        var names = ReadStrings(qos, Native.dds_qget_propnames);
        if (names is null) return null;
        return names.Select(name =>
        {
            if (!Native.dds_qget_prop_propagate(qos, name, out var value, out var propagate))
                throw new InvalidOperationException("Cannot read native property: " + name);
            return new Property(name, TakeString(value), propagate);
        }).ToArray();
    }

    private static BinaryProperty[]? ReadBinaryProperties(nint qos)
    {
        var names = ReadStrings(qos, Native.dds_qget_bpropnames);
        if (names is null) return null;
        return names.Select(name =>
        {
            if (!Native.dds_qget_bprop_propagate(qos, name, out var value, out var size, out var propagate))
                throw new InvalidOperationException("Cannot read native binary property: " + name);
            return new BinaryProperty(name, TakeBytes(value, size), propagate);
        }).ToArray();
    }
}
