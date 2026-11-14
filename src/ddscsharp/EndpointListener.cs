// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Org.Eclipse.CycloneDDS;

// One native listener per endpoint, shared by async waits, data callbacks and status events.
internal sealed class EndpointListener(Entity owner, Action<Native.StatusData> dispatch) : IDisposable
{
    private static readonly Native.StatusCallback Callback = OnNativeStatus;
    private readonly ConcurrentQueue<Native.StatusData> queue = new();
    private GCHandle context;
    private nint native;
    private StatusMask mask;
    private int active = 1, running, pendingData;
    private Exception? failure;
    internal Exception? LastException => Volatile.Read(ref failure);

    // The caller holds owner.Gate. Never take that gate from a native callback.
    internal void SetMask(StatusMask value)
    {
        if (Volatile.Read(ref active) == 0 || mask == value) return;
        if (native == 0)
        {
            // A static callback and weak target allow Entity's finalizer to clean up abandoned endpoints.
            context = GCHandle.Alloc(new WeakReference<EndpointListener>(this));
            try { DdsException.Check(Native.cs_listener_create(Callback, GCHandle.ToIntPtr(context), out native), "CreateListener"); }
            catch { context.Free(); throw; }
        }
        DdsException.Check(Native.cs_listener_set(owner.Handle, native, (uint)value), "SetListener");
        mask = value;
    }

    private static void OnNativeStatus(in Native.StatusData status, nint argument)
    {
        if (GCHandle.FromIntPtr(argument).Target is WeakReference<EndpointListener> weak && weak.TryGetTarget(out var target))
            target.Notify(status);
    }

    internal void Notify(Native.StatusData status)
    {
        if (Volatile.Read(ref active) == 0) return;
        try
        {
            if (status.Kind == (uint)StatusMask.DataAvailable && Interlocked.Exchange(ref pendingData, 1) != 0) return;
            queue.Enqueue(status);
            Schedule();
        }
        catch (Exception error) { RecordException(error); }
    }

    private void Schedule()
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(static state => ((EndpointListener)state!).Drain(), this);
    }

    private void Drain()
    {
        try
        {
            while (Volatile.Read(ref active) != 0 && queue.TryDequeue(out var status))
            {
                if (status.Kind == (uint)StatusMask.DataAvailable) Interlocked.Exchange(ref pendingData, 0);
                try { dispatch(status); }
                catch (Exception error) { RecordException(error); }
            }
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
            if (Volatile.Read(ref active) != 0 && !queue.IsEmpty) Schedule();
        }
    }

    internal void Invoke<T>(EventHandler<T>? handlers, T status)
    {
        if (handlers is null) return;
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            if (Volatile.Read(ref active) == 0) break;
            try { handler(owner, status); }
            catch (Exception error) { RecordException(error); }
        }
    }

    internal void RecordException(Exception error) => Interlocked.CompareExchange(ref failure, error, null);

    internal static Native.StatusData GetStatus(Entity entity, StatusMask kind)
    {
        lock (entity.Gate)
        {
            DdsException.Check(Native.cs_get_status(entity.Handle, (uint)kind, out var status), "GetStatus");
            return status;
        }
    }

    public void Dispose()
    {
        lock (owner.Gate)
        {
            Interlocked.Exchange(ref active, 0);
            if (native != 0)
            {
                // Detaching waits for native callbacks before their context can be freed.
                DdsException.Check(Native.cs_listener_set(owner.Handle, native, 0), "ClearListener");
                Native.cs_listener_free(native);
                native = 0;
                context.Free();
            }
            queue.Clear();
        }
    }
}
