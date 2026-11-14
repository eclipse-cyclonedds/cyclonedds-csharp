// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
namespace Org.Eclipse.CycloneDDS;

public sealed partial class DataReader<T> where T : class
{
    private readonly List<DataWaiter> waiters = [];
    private ReadCondition? dataCondition;

    /// <summary>Waits for unread samples without consuming them. Returns false if disposed while waiting.</summary>
    /// <remarks>Cancellation cancels the task. Availability does not reserve data against other consumers.</remarks>
    public Task<bool> WaitDataAsync(CancellationToken cancellationToken = default)
    {
        lock (Gate)
        {
            _ = Handle;
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<bool>(cancellationToken);
            EnsureDataCondition();
            if (dataCondition!.Triggered) return Task.FromResult(true);
            var waiter = new DataWaiter(this, cancellationToken);
            waiters.Add(waiter);
            try
            {
                UpdateListenerMask();
                waiter.Register();
                // Close the race between the first check and attaching the native listener.
                if (dataCondition.Triggered) CompleteWaiters(true);
            }
            catch
            {
                waiters.Remove(waiter);
                waiter.Unregister();
                UpdateListenerMask();
                throw;
            }
            return waiter.Source.Task;
        }
    }

    private void EnsureDataCondition() => dataCondition ??= CreateReadCondition(
        new DataState(SampleState.NotRead, ViewState.Any, InstanceState.Any));

    private void QueueAvailableData()
    {
        if (dataCondition!.Triggered) Events.Notify(new Native.StatusData { Kind = (uint)StatusMask.DataAvailable });
    }

    private void DispatchData()
    {
        DataAvailableListener<T>? current;
        lock (Gate)
        {
            if (closed) return;
            if (dataCondition is not null && dataCondition.Triggered) CompleteWaiters(true);
            current = listener;
        }
        current?.Invoke();
    }

    private void CompleteWaiters(bool available)
    {
        var pending = waiters.ToArray();
        waiters.Clear();
        foreach (var waiter in pending)
        {
            waiter.Unregister();
            waiter.Source.TrySetResult(available);
        }
        UpdateListenerMask();
    }

    private void CancelWaiter(DataWaiter waiter)
    {
        lock (Gate)
        {
            if (!waiters.Remove(waiter)) return;
            waiter.Unregister();
            waiter.Source.TrySetCanceled(waiter.Token);
            try { UpdateListenerMask(); }
            catch (Exception error) { RecordListenerException(error); }
        }
    }

    internal void RemoveDataListener(DataAvailableListener<T> registration)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(listener, registration)) return;
            listener = null;
            UpdateListenerMask();
        }
    }

    internal void RecordListenerException(Exception error) => endpointListener?.RecordException(error);

    private sealed class DataWaiter(DataReader<T> owner, CancellationToken token)
    {
        // Keep the reader alive while a caller holds the pending task.
        internal readonly TaskCompletionSource<bool> Source = new(owner, TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token => token;
        private CancellationTokenRegistration registration;
        internal void Register()
        {
            registration = token.UnsafeRegister(static state => ((DataWaiter)state!).Cancel(), this);
            if (Source.Task.IsCompleted) registration.Unregister();
        }
        private void Cancel() => owner.CancelWaiter(this);
        // Dispose could block on a callback waiting for Gate; Unregister never waits.
        internal void Unregister() => registration.Unregister();
    }
}
