// Copyright(c) 2026 Sven Trittler
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Org.Eclipse.CycloneDDS;
using Test;

internal static partial class Program
{
    private static readonly int[] StressSizes = [0, 1, 63, 1024, 8192, 65536];

    private static void RegisterStressTests()
    {
        Run("Stress.async-listeners-dispose-and-resources", () =>
        {
            string? configured = Environment.GetEnvironmentVariable("CYCLONEDDS_CSHARP_STRESS_SECONDS");
            int seconds = configured is null ? 20 : int.Parse(configured, System.Globalization.CultureInfo.InvariantCulture);
            if (seconds < 1 || seconds > 3600) throw new ArgumentOutOfRangeException(nameof(seconds), "Stress duration must be 1..3600 seconds.");

            // Warm both wire paths, thread-pool callbacks, native discovery, and the largest pool buckets.
            for (int i = 0; i < 4; i++) StressCycle(i);
            CollectStressGarbage();
            long managedBefore = GC.GetTotalMemory(true);
            using var process = Process.GetCurrentProcess(); process.Refresh();
            long privateBefore = process.PrivateMemorySize64;
            int handlesBefore = process.HandleCount;
            var references = new List<WeakReference>(); var timer = Stopwatch.StartNew(); int cycles = 0;
            do
            {
                references.AddRange(StressCycle(cycles++));
                if (cycles % 16 == 0) CheckStressCollection(references);
            } while (timer.Elapsed < TimeSpan.FromSeconds(seconds) || cycles < 16);
            CheckStressCollection(references);
            long managedAfter = GC.GetTotalMemory(true); process.Refresh(); long privateAfter = process.PrivateMemorySize64;
            int handlesAfter = process.HandleCount;
            Console.WriteLine($"STRESS cycles={cycles}, validated samples={cycles * 24}, duration={timer.Elapsed.TotalSeconds:F1}s, " +
                $"managed retained delta={managedAfter - managedBefore} bytes, process private delta={privateAfter - privateBefore} bytes, " +
                $"OS handle delta={handlesAfter - handlesBefore}");
            // Process memory includes native heaps, JIT, GC segments, thread stacks and allocator caches.
            // A broad guard detects sustained retention; native handles and weak targets are checked precisely below.
            True(managedAfter - managedBefore < 16L * 1024 * 1024);
            True(privateAfter - privateBefore < 128L * 1024 * 1024);
            True(handlesAfter - handlesBefore < 32);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] StressCycle(int cycle)
    {
        using var participant = Participant(); using var topic = participant.CreateTopic(Name(), Message.TypeSupport);
        var qos = Reliable with
        {
            DataRepresentation = [cycle % 2 == 0 ? DataRepresentationKind.Xcdr1 : DataRepresentationKind.Xcdr2],
            ResourceLimits = new(64, 1, 64)
        };
        using var reader = participant.CreateReader(topic, qos); using var writer = participant.CreateWriter(topic, qos);
        // Status events share the native endpoint listener with data notifications and async waits.
        writer.PublicationMatched += (_, _) => { };
        reader.SubscriptionMatched += (_, _) => { };
        Match(writer, reader);
        int received = 0, closing = 0, callbacks = 0;
        var seen = new bool[24];
        void Receive(DataReader<Message> source)
        {
            Interlocked.Increment(ref callbacks);
            try
            {
                foreach (var sample in source.Take())
                {
                    if (!sample.Info.ValidData) continue;
                    if (Volatile.Read(ref closing) != 0)
                    {
                        Equal(7, sample.Data.id); Equal("shutdown", sample.Data.text);
                        continue;
                    }
                    int sequence = sample.Data.value;
                    True(sequence >= 0 && sequence < seen.Length); True(!seen[sequence]); seen[sequence] = true;
                    Equal(7, sample.Data.id);
                    Equal(new string((char)('a' + sequence % 26), StressSizes[sequence % StressSizes.Length]), sample.Data.text);
                    Interlocked.Increment(ref received);
                }
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref closing) != 0) { }
            finally { Interlocked.Decrement(ref callbacks); }
        }
        using var first = reader.Listen(Receive);
        var listener = first;
        for (int batch = 0; batch < 2; batch++)
        {
            var pending = reader.WaitDataAsync(); True(!pending.IsCompleted);
            int start = batch * 12;
            var producer = Task.Run(() =>
            {
                for (int i = start; i < start + 12; i++)
                    writer.Write(new(7, new string((char)('a' + i % 26), StressSizes[i % StressSizes.Length]), i));
            });
            True(Await(pending)); producer.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            WaitUntil(() => Volatile.Read(ref received) == start + 12 || reader.LastListenerException is not null);
            True(reader.LastListenerException is null); True(listener.LastException is null);
            WaitUntil(() => Volatile.Read(ref callbacks) == 0);
            // Exercise cancellation cleanup and listener replacement on a live reader between batches.
            using var cancel = new CancellationTokenSource(); var cancelled = reader.WaitDataAsync(cancel.Token);
            True(!cancelled.IsCompleted); cancel.Cancel();
            Throws<OperationCanceledException>(() => Await(cancelled));
            if (batch == 0) { listener = reader.Listen(Receive); first.Dispose(); }
        }
        True(seen.All(value => value));
        WaitUntil(() => Volatile.Read(ref callbacks) == 0);
        // Start several waiters on an empty reader, then race cancellation, writes, parent and child deletion.
        using var cancellation = new CancellationTokenSource();
        var cancelledWait = reader.WaitDataAsync(cancellation.Token); var disposedWait = reader.WaitDataAsync();
        var handles = new List<int>(); CaptureStressEntities(CdrOracle.Handle(participant), handles);
        True(handles.Contains(CdrOracle.Handle(reader)) && handles.Contains(CdrOracle.Handle(writer)));
        using var startRace = new ManualResetEventSlim();
        Task Race(Action action) => Task.Run(() => { startRace.Wait(); action(); });
        Volatile.Write(ref closing, 1);
        Task[] racers = [
            Race(() => { try { for (int i = 0; i < 16; i++) writer.Write(new(7, "shutdown", i)); } catch (ObjectDisposedException) { } }),
            Race(cancellation.Cancel), Race(participant.Dispose), Race(reader.Dispose), Race(writer.Dispose)
        ];
        startRace.Set(); Task.WhenAll(racers).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        try { _ = Await(cancelledWait); } catch (OperationCanceledException) { }
        _ = Await(disposedWait); // May be true if writing won the race, otherwise false on disposal.
        WaitUntil(() => Volatile.Read(ref callbacks) == 0);
        True(participant.IsDisposed && topic.IsDisposed && reader.IsDisposed && writer.IsDisposed);
        foreach (int handle in handles) True(CdrOracle.oracle_entity_parent(handle) < 0);
        True(listener.LastException is null); True(reader.LastListenerException is null); True(writer.LastListenerException is null);
        return [new(participant), new(topic), new(reader), new(writer), new(first), new(listener), new(disposedWait), new(cancelledWait)];
    }

    private static void CollectStressGarbage()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    private static void CaptureStressEntities(int entity, List<int> handles)
    {
        handles.Add(entity);
        var children = new int[64];
        int count = CdrOracle.oracle_entity_children(entity, children, (uint)children.Length);
        True(count >= 0 && count <= children.Length);
        for (int i = 0; i < count; i++) CaptureStressEntities(children[i], handles);
    }

    private static void CheckStressCollection(List<WeakReference> references)
    {
        WaitUntil(() => { CollectStressGarbage(); return references.All(reference => !reference.IsAlive); });
        references.Clear();
    }
}
