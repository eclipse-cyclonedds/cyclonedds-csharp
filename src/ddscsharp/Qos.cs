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

public enum ReliabilityKind { BestEffort, Reliable }
public enum DurabilityKind { Volatile, TransientLocal, Transient, Persistent }
public enum HistoryKind { KeepLast, KeepAll }
public enum LivelinessKind { Automatic, ManualByParticipant, ManualByTopic }
public enum OwnershipKind { Shared, Exclusive }
public enum DestinationOrderKind { ByReceptionTimestamp, BySourceTimestamp }
public enum DataRepresentationKind : short { Xcdr1 = 0, Xcdr2 = 2 }
public enum PresentationAccessScopeKind { Instance, Topic, Group }
public enum IgnoreLocalKind { None, Participant, Process }
public enum TypeConsistencyKind { DisallowTypeCoercion, AllowTypeCoercion }
public sealed record Reliability(ReliabilityKind Kind, Duration MaxBlockingTime);
public sealed record History(HistoryKind Kind, int Depth = 1);
public sealed record ResourceLimits(int MaxSamples = -1, int MaxInstances = -1, int MaxSamplesPerInstance = -1);
public sealed record Liveliness(LivelinessKind Kind, Duration LeaseDuration);
public sealed record ReaderDataLifecycle(Duration AutopurgeNoWriterSamplesDelay, Duration AutopurgeDisposedSamplesDelay);
public sealed record Presentation(PresentationAccessScopeKind AccessScope, bool CoherentAccess = false, bool OrderedAccess = false);
public sealed record DurabilityService(Duration ServiceCleanupDelay, History History, ResourceLimits ResourceLimits);
public sealed record TypeConsistency(TypeConsistencyKind Kind, bool IgnoreSequenceBounds = true, bool IgnoreStringBounds = true,
    bool IgnoreMemberNames = false, bool PreventTypeWidening = false, bool ForceTypeValidation = false);
public sealed record Property(string Name, string Value, bool Propagate = false);
public sealed record BinaryProperty(string Name, byte[] Value, bool Propagate = false);

/// <summary>Policies omitted from this value use the Cyclone DDS defaults.</summary>
public sealed partial record Qos
{
    public Reliability? Reliability { get; init; }
    public bool? WriterBatching { get; init; }
    public DurabilityKind? Durability { get; init; }
    public History? History { get; init; }
    public ResourceLimits? ResourceLimits { get; init; }
    public IReadOnlyList<string>? Partitions { get; init; }
    public Duration? Deadline { get; init; }
    public Duration? LatencyBudget { get; init; }
    public Duration? Lifespan { get; init; }
    public Liveliness? Liveliness { get; init; }
    public OwnershipKind? Ownership { get; init; }
    public int? OwnershipStrength { get; init; }
    public int? TransportPriority { get; init; }
    public DestinationOrderKind? DestinationOrder { get; init; }
    public bool? AutodisposeUnregisteredInstances { get; init; }
    public ReaderDataLifecycle? ReaderDataLifecycle { get; init; }
    public byte[]? UserData { get; init; }
    public byte[]? TopicData { get; init; }
    public byte[]? GroupData { get; init; }
    public IReadOnlyList<DataRepresentationKind>? DataRepresentation { get; init; }
    public Presentation? Presentation { get; init; }
    public Duration? TimeBasedFilter { get; init; }
    public DurabilityService? DurabilityService { get; init; }
    public IgnoreLocalKind? IgnoreLocal { get; init; }
    public IReadOnlyList<Property>? Properties { get; init; }
    public IReadOnlyList<BinaryProperty>? BinaryProperties { get; init; }
    public TypeConsistency? TypeConsistency { get; init; }
    public string? EntityName { get; init; }
    public IReadOnlyList<string>? PsmxInstances { get; init; }

    internal QosHandle ToNative()
    {
        if (History is { } history) ValidateHistory(history, nameof(History));
        if (ResourceLimits is { } limits) ValidateLimits(limits, nameof(ResourceLimits));
        var q = new QosHandle();
        try
        {
            var p = q.DangerousGetHandle();
            if (Presentation is { } presentation)
            {
                Validate(presentation.AccessScope);
                Native.dds_qset_presentation(p, presentation.AccessScope, presentation.CoherentAccess, presentation.OrderedAccess);
            }
            if (TimeBasedFilter is { } filter) Native.dds_qset_time_based_filter(p, filter.Nanoseconds);
            if (DurabilityService is { } service)
            {
                ArgumentNullException.ThrowIfNull(service.History);
                ArgumentNullException.ThrowIfNull(service.ResourceLimits);
                Validate(service.History.Kind);
                ValidateHistory(service.History, nameof(DurabilityService));
                ValidateLimits(service.ResourceLimits, nameof(DurabilityService));
                Native.dds_qset_durability_service(p, service.ServiceCleanupDelay.Nanoseconds, service.History.Kind, service.History.Depth,
                    service.ResourceLimits.MaxSamples, service.ResourceLimits.MaxInstances, service.ResourceLimits.MaxSamplesPerInstance);
            }
            if (IgnoreLocal is { } ignore) { Validate(ignore); Native.dds_qset_ignorelocal(p, ignore); }
            if (TypeConsistency is { } consistency)
            {
                Validate(consistency.Kind);
                Native.dds_qset_type_consistency(p, consistency.Kind, consistency.IgnoreSequenceBounds, consistency.IgnoreStringBounds,
                    consistency.IgnoreMemberNames, consistency.PreventTypeWidening, consistency.ForceTypeValidation);
            }
            if (EntityName is { } name) { ValidateString(name, nameof(EntityName)); Native.dds_qset_entity_name(p, name); }
            if (Properties is not null || BinaryProperties is not null)
            {
                // Set/unset marks an explicitly empty Property policy using only public C APIs.
                Native.dds_qset_prop_propagate(p, "", "", false);
                Native.dds_qunset_prop(p, "");
            }
            if (Properties is { } properties)
                foreach (var property in properties)
                {
                    ArgumentNullException.ThrowIfNull(property);
                    ValidateString(property.Name, nameof(Properties));
                    ValidateString(property.Value, nameof(Properties));
                    Native.dds_qset_prop_propagate(p, property.Name, property.Value, property.Propagate);
                }
            if (BinaryProperties is { } binaryProperties)
                foreach (var property in binaryProperties)
                {
                    ArgumentNullException.ThrowIfNull(property);
                    ValidateString(property.Name, nameof(BinaryProperties));
                    ArgumentNullException.ThrowIfNull(property.Value);
                    Native.dds_qset_bprop_propagate(p, property.Name, property.Value, (nuint)property.Value.Length, property.Propagate);
                }
            if (PsmxInstances is { } instances) SetStrings(p, instances, Native.dds_qset_psmx_instances, nameof(PsmxInstances));
            if (DataRepresentation is { } representations)
            {
                if (representations.Count == 0) throw new ArgumentException("At least one data representation is required.", nameof(DataRepresentation));
                var values = new short[representations.Count];
                for (var i = 0; i < values.Length; i++) { Validate(representations[i]); values[i] = (short)representations[i]; }
                Native.dds_qset_data_representation(p, checked((uint)values.Length), values);
            }
            if (Reliability is { } r) { Validate(r.Kind); Native.dds_qset_reliability(p, r.Kind, r.MaxBlockingTime.Nanoseconds); }
            if (WriterBatching is { } batching) Native.dds_qset_writer_batching(p, batching);
            if (Durability is { } d) { Validate(d); Native.dds_qset_durability(p, d); }
            if (History is { } h) { Validate(h.Kind); Native.dds_qset_history(p, h.Kind, h.Depth); }
            if (ResourceLimits is { } l) Native.dds_qset_resource_limits(p, l.MaxSamples, l.MaxInstances, l.MaxSamplesPerInstance);
            if (Deadline is { } deadline) Native.dds_qset_deadline(p, deadline.Nanoseconds);
            if (LatencyBudget is { } latency) Native.dds_qset_latency_budget(p, latency.Nanoseconds);
            if (Lifespan is { } lifespan) Native.dds_qset_lifespan(p, lifespan.Nanoseconds);
            if (Liveliness is { } liveliness) { Validate(liveliness.Kind); Native.dds_qset_liveliness(p, liveliness.Kind, liveliness.LeaseDuration.Nanoseconds); }
            if (Ownership is { } ownership) { Validate(ownership); Native.dds_qset_ownership(p, ownership); }
            if (OwnershipStrength is { } strength) Native.dds_qset_ownership_strength(p, strength);
            if (TransportPriority is { } priority) Native.dds_qset_transport_priority(p, priority);
            if (DestinationOrder is { } order) { Validate(order); Native.dds_qset_destination_order(p, order); }
            if (AutodisposeUnregisteredInstances is { } autodispose) Native.dds_qset_writer_data_lifecycle(p, autodispose);
            if (ReaderDataLifecycle is { } lifecycle) Native.dds_qset_reader_data_lifecycle(p, lifecycle.AutopurgeNoWriterSamplesDelay.Nanoseconds, lifecycle.AutopurgeDisposedSamplesDelay.Nanoseconds);
            if (UserData is { } user) Native.dds_qset_userdata(p, user, (nuint)user.Length);
            if (TopicData is { } topic) Native.dds_qset_topicdata(p, topic, (nuint)topic.Length);
            if (GroupData is { } group) Native.dds_qset_groupdata(p, group, (nuint)group.Length);
            if (Partitions is { } partitions)
                SetStrings(p, partitions, Native.dds_qset_partition, nameof(Partitions));
            return q;
        }
        catch { q.Dispose(); throw; }
    }

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ValidateString(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Contains('\0')) throw new ArgumentException("Strings cannot contain NUL.", name);
    }

    private static void ValidateHistory(History value, string name)
    {
        if (value.Kind == HistoryKind.KeepLast && value.Depth <= 0) throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateLimits(ResourceLimits value, string name)
    {
        static bool Invalid(int n) => n == 0 || n < -1;
        if (Invalid(value.MaxSamples) || Invalid(value.MaxInstances) || Invalid(value.MaxSamplesPerInstance))
            throw new ArgumentOutOfRangeException(name);
    }

    private static void SetStrings(nint qos, IReadOnlyList<string> values, Action<nint, uint, nint[]> setter, string name)
    {
        var strings = new List<nint>();
        try
        {
            foreach (var value in values)
            {
                ValidateString(value, name);
                strings.Add(Marshal.StringToCoTaskMemUTF8(value));
            }
            setter(qos, checked((uint)strings.Count), strings.ToArray());
        }
        finally { foreach (var value in strings) Marshal.FreeCoTaskMem(value); }
    }
}

internal sealed class QosHandle : SafeHandle
{
    internal QosHandle() : base(0, true)
    {
        SetHandle(Native.dds_create_qos());
        if (IsInvalid) throw new OutOfMemoryException();
    }
    public override bool IsInvalid => handle == 0;
    protected override bool ReleaseHandle() { Native.dds_delete_qos(handle); return true; }
}
