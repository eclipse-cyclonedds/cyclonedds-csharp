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

internal static partial class Program
{
    private static T Await<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference AbandonedListeners()
    {
        var p = Participant(); var t = p.CreateTopic(Name(), Message.TypeSupport);
        var w = p.CreateWriter(t, Reliable); var r = p.CreateReader(t, Reliable);
        w.PublicationMatched += (_, _) => { };
        r.SubscriptionMatched += (_, _) => { };
        r.Listen(_ => { });
        _ = r.WaitDataAsync();
        return new WeakReference(p);
    }

    private static void RegisterAsyncListenerTests()
    {
        Run("Async.in-flight-listener-with-concurrent-parent-child-dispose", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            using var release = new ManualResetEventSlim();
            var entered = Completion<bool>(); var finished = Completion<bool>();
            using var listener = r.Listen(source =>
            {
                try
                {
                    var value = source.Take().Single(sample => sample.Info.ValidData);
                    Equal("in flight", value.Data.text); entered.TrySetResult(true);
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Callback was not released.");
                    True(source.IsDisposed);
                    Throws<ObjectDisposedException>(() => source.Take());
                    finished.TrySetResult(true);
                }
                catch (Exception error) { entered.TrySetException(error); finished.TrySetException(error); }
            });
            try
            {
                var available = r.WaitDataAsync(); w.Write(new(7, "in flight", 1));
                True(Await(available)); True(Await(entered.Task));
                var pending = r.WaitDataAsync(); True(!pending.IsCompleted);
                // Deletion must finish while the callback is still blocked outside the entity gate.
                Task.WhenAll(Task.Run(p.Dispose), Task.Run(r.Dispose), Task.Run(w.Dispose))
                    .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                True(!Await(pending)); True(p.IsDisposed && r.IsDisposed && w.IsDisposed);
            }
            finally { release.Set(); }
            True(Await(finished.Task)); True(listener.LastException is null); True(r.LastListenerException is null);
        }));
        Run("Async.wait-does-not-consume", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            var pending = r.WaitDataAsync(); True(!pending.IsCompleted);
            w.Write(new(1, "async", 1)); True(Await(pending));
            True(Await(r.WaitDataAsync())); Equal("async", r.Take().Single().Data.text);
        }));
        Run("Async.read-history-is-not-new-data", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            w.Write(new(1, "read", 1)); True(Await(r.WaitDataAsync())); Equal(1, r.Read().Count);
            var pending = r.WaitDataAsync(); True(!pending.IsCompleted);
            w.Write(new(2, "new", 2)); True(Await(pending)); Equal(2, r.Take().Count);
        }));
        Run("Async.cancellation-and-reuse", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var preCancelled = r.WaitDataAsync(cancelled.Token); True(preCancelled.IsCanceled);
            using var cancellation = new CancellationTokenSource();
            var pending = r.WaitDataAsync(cancellation.Token); var other = r.WaitDataAsync();
            cancellation.Cancel();
            try { Await(pending); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException error) { Equal(cancellation.Token, error.CancellationToken); }
            True(!other.IsCompleted);
            var again = r.WaitDataAsync(); w.Write(new(1, "reuse", 1)); True(Await(other)); True(Await(again));
        }));
        Run("Async.dispose-completes-waiters", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            var first = r.WaitDataAsync(); var second = r.WaitDataAsync();
            r.Dispose(); True(!Await(first)); True(!Await(second));
            Throws<ObjectDisposedException>(() => r.WaitDataAsync());
        }));
        Run("Async.parent-dispose-completes-waiters", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            var pending = r.WaitDataAsync(); p.Dispose(); True(!Await(pending));
        }));
        Run("Async.listener-replacement-preserves-waiters", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            using var first = r.Listen(_ => throw new Exception("Replaced listener was invoked"));
            using var second = r.Listen(_ => { });
            var pending = r.WaitDataAsync(); first.Dispose(); second.Dispose();
            w.Write(new(1, "wait", 1)); True(Await(pending)); True(r.LastListenerException is null);
        }));
        Run("Async.listener-and-wait-share-notifications", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            var received = Completion<string>();
            using var listener = r.Listen(source =>
            {
                foreach (var sample in source.Take()) if (sample.Info.ValidData) received.TrySetResult(sample.Data.text);
            });
            var pending = r.WaitDataAsync(); w.Write(new(1, "both", 1));
            True(Await(pending)); Equal("both", Await(received.Task));
        }));
        Run("Async.registration-and-cancellation-races", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            for (int i = 0; i < 40; i++)
            {
                using var cancellation = new CancellationTokenSource();
                var pending = r.WaitDataAsync(cancellation.Token);
                var write = Task.Run(() => w.Write(new(i, "race", i)));
                var cancel = Task.Run(cancellation.Cancel);
                try { True(Await(pending)); } catch (OperationCanceledException) { }
                Task.WhenAll(write, cancel).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                True(Await(r.WaitDataAsync())); Equal(1, r.Take().Count);
            }
        }));
        Run("Async.status-subscription-changes-preserve-data-listener", () => Endpoints(Message.TypeSupport, (p, t, w, r) =>
        {
            EventHandler<SubscriptionMatchedStatus> handler = (_, _) => { };
            r.SubscriptionMatched += handler;
            var received = Completion<string>();
            using var listener = r.Listen(source =>
            {
                foreach (var sample in source.Take()) if (sample.Info.ValidData) received.TrySetResult(sample.Data.text);
            });
            var pending = r.WaitDataAsync(); r.SubscriptionMatched -= handler;
            w.Write(new(1, "multiplexed", 1)); True(Await(pending)); Equal("multiplexed", Await(received.Task));
        }));
        Run("Listener.native-registration-does-not-root-endpoints", () =>
        {
            var abandoned = AbandonedListeners();
            WaitUntil(() =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                return !abandoned.IsAlive;
            });
        });
        Run("Status.matching-counters-and-handles", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            using var r = p.CreateReader(t, Reliable);
            var connected = Completion<SubscriptionMatchedStatus>(); var disconnected = Completion<SubscriptionMatchedStatus>();
            r.SubscriptionMatched += (sender, status) =>
            {
                True(ReferenceEquals(r, sender));
                if (status.CurrentCount > 0) connected.TrySetResult(status); else disconnected.TrySetResult(status);
            };
            using var w = p.CreateWriter(t, Reliable); var writerHandle = w.InstanceHandle;
            var matched = Await(connected.Task); Equal(1u, matched.TotalCount); Equal(1, matched.CurrentCountChange);
            Equal(writerHandle, matched.LastPublicationHandle);
            var publication = w.PublicationMatchedStatus;
            Equal(1u, publication.CurrentCount); Equal(r.InstanceHandle, publication.LastSubscriptionHandle);
            Equal(0, w.PublicationMatchedStatus.TotalCountChange);
            w.Dispose(); var unmatched = Await(disconnected.Task); Equal(1u, unmatched.TotalCount);
            Equal(-1, unmatched.CurrentCountChange); Equal(writerHandle, unmatched.LastPublicationHandle);
        });
        Run("Status.publication-events-and-unsubscribe", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            using var w = p.CreateWriter(t, Reliable);
            var connected = Completion<PublicationMatchedStatus>(); var disconnected = Completion<PublicationMatchedStatus>();
            EventHandler<PublicationMatchedStatus> handler = (_, status) =>
            {
                if (status.CurrentCount > 0) connected.TrySetResult(status); else disconnected.TrySetResult(status);
            };
            w.PublicationMatched += handler;
            using var r = p.CreateReader(t, Reliable);
            var status = Await(connected.Task); Equal(r.InstanceHandle, status.LastSubscriptionHandle);
            Equal(1u, r.SubscriptionMatchedStatus.CurrentCount);
            r.Dispose(); Equal(-1, Await(disconnected.Task).CurrentCountChange);
            w.PublicationMatched -= handler;
            using var next = p.CreateReader(t, Reliable); Match(w, next);
            Equal(1, w.PublicationMatchedStatus.TotalCountChange);
        });
        Run("Status.deadline-reader-and-writer", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            var qos = Reliable with { Deadline = Duration.FromMilliseconds(100) };
            using var r = p.CreateReader(t, qos); using var w = p.CreateWriter(t, qos);
            var offered = Completion<OfferedDeadlineMissedStatus>(); var requested = Completion<RequestedDeadlineMissedStatus>();
            w.OfferedDeadlineMissed += (_, status) => offered.TrySetResult(status);
            r.RequestedDeadlineMissed += (_, status) => requested.TrySetResult(status);
            Match(w, r); var sample = new Message(7, "deadline", 1); var instance = w.RegisterInstance(sample);
            w.Write(sample); var received = TakeOne(r);
            var ws = Await(offered.Task); var rs = Await(requested.Task);
            True(ws.TotalCount >= 1 && ws.TotalCountChange >= 1); Equal(instance, ws.LastInstanceHandle);
            True(rs.TotalCount >= 1 && rs.TotalCountChange >= 1); Equal(received.Info.InstanceHandle, rs.LastInstanceHandle);
            True(w.OfferedDeadlineMissedStatus.TotalCount >= ws.TotalCount);
            True(r.RequestedDeadlineMissedStatus.TotalCount >= rs.TotalCount);
        });
        Run("Status.liveliness-loss-and-recovery", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            var qos = Reliable with { Liveliness = new(LivelinessKind.ManualByTopic, Duration.FromMilliseconds(500)) };
            using var r = p.CreateReader(t, qos); using var w = p.CreateWriter(t, qos);
            var lost = Completion<LivelinessLostStatus>(); var notAlive = Completion<LivelinessChangedStatus>();
            var recovery = Completion<LivelinessChangedStatus>(); int recovering = 0;
            w.LivelinessLost += (_, status) => lost.TrySetResult(status);
            r.LivelinessChanged += (_, status) =>
            {
                if (status.NotAliveCount > 0) notAlive.TrySetResult(status);
                if (Volatile.Read(ref recovering) != 0 && status.AliveCount > 0) recovery.TrySetResult(status);
            };
            Match(w, r); w.Write(new(1, "alive", 1)); TakeOne(r);
            var ws = Await(lost.Task); var rs = Await(notAlive.Task);
            True(ws.TotalCount >= 1 && ws.TotalCountChange >= 1);
            Equal(0u, rs.AliveCount); Equal(1u, rs.NotAliveCount); Equal(w.InstanceHandle, rs.LastPublicationHandle);
            True(w.LivelinessLostStatus.TotalCount >= 1); Equal(1u, r.LivelinessChangedStatus.NotAliveCount);
            Volatile.Write(ref recovering, 1); w.Write(new(1, "recovered", 2));
            Equal(1u, Await(recovery.Task).AliveCount);
        });
        Run("Status.handler-errors-are-isolated", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            using var w = p.CreateWriter(t, Reliable); var received = Completion<PublicationMatchedStatus>();
            w.PublicationMatched += (_, _) => throw new FormatException("status failure");
            w.PublicationMatched += (_, status) => received.TrySetResult(status);
            using var r = p.CreateReader(t, Reliable); Equal(1u, Await(received.Task).CurrentCount);
            True(w.LastListenerException is FormatException);
        });
        Run("Status.callback-can-dispose-participant", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            using var w = p.CreateWriter(t, Reliable); var disposed = Completion<bool>();
            w.PublicationMatched += (_, _) => { p.Dispose(); disposed.TrySetResult(true); };
            using var r = p.CreateReader(t, Reliable); True(Await(disposed.Task)); True(r.IsDisposed);
        });
        Run("Status.async-wait-does-not-consume-matching-status", () =>
        {
            using var p = Participant(); using var t = p.CreateTopic(Name(), Message.TypeSupport);
            using var r = p.CreateReader(t, Reliable); var pending = r.WaitDataAsync();
            using var w = p.CreateWriter(t, Reliable); Match(w, r);
            Equal(1, r.SubscriptionMatchedStatus.TotalCountChange);
            w.Write(new(1, "status", 1)); True(Await(pending));
        });
    }
}
