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
    private EndpointListener? endpointListener;
    private bool closed;
    private EndpointListener Events => endpointListener ??= new(this, DispatchStatus);
    /// <summary>First exception thrown by a listener or status event handler.</summary>
    public Exception? LastListenerException => endpointListener?.LastException;

    private EventHandler<SubscriptionMatchedStatus>? subscriptionMatched;
    public event EventHandler<SubscriptionMatchedStatus> SubscriptionMatched
    {
        add { lock (Gate) { _ = Handle; subscriptionMatched += value; UpdateListenerMask(); } }
        remove { lock (Gate) { subscriptionMatched -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public SubscriptionMatchedStatus SubscriptionMatchedStatus => ConvertSubscriptionMatched(EndpointListener.GetStatus(this, StatusMask.SubscriptionMatched));
    private static SubscriptionMatchedStatus ConvertSubscriptionMatched(Native.StatusData s) => new(s.TotalCount, s.TotalCountChange, s.CurrentCount, s.CurrentCountChange, s.LastHandle);

    private EventHandler<RequestedDeadlineMissedStatus>? requestedDeadlineMissed;
    public event EventHandler<RequestedDeadlineMissedStatus> RequestedDeadlineMissed
    {
        add { lock (Gate) { _ = Handle; requestedDeadlineMissed += value; UpdateListenerMask(); } }
        remove { lock (Gate) { requestedDeadlineMissed -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public RequestedDeadlineMissedStatus RequestedDeadlineMissedStatus => ConvertRequestedDeadlineMissed(EndpointListener.GetStatus(this, StatusMask.RequestedDeadlineMissed));
    private static RequestedDeadlineMissedStatus ConvertRequestedDeadlineMissed(Native.StatusData s) => new(s.TotalCount, s.TotalCountChange, s.LastHandle);

    private EventHandler<LivelinessChangedStatus>? livelinessChanged;
    public event EventHandler<LivelinessChangedStatus> LivelinessChanged
    {
        add { lock (Gate) { _ = Handle; livelinessChanged += value; UpdateListenerMask(); } }
        remove { lock (Gate) { livelinessChanged -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public LivelinessChangedStatus LivelinessChangedStatus => ConvertLivelinessChanged(EndpointListener.GetStatus(this, StatusMask.LivelinessChanged));
    private static LivelinessChangedStatus ConvertLivelinessChanged(Native.StatusData s) => new(s.CurrentCount, s.NotAliveCount, s.CurrentCountChange, s.NotAliveCountChange, s.LastHandle);

    private void UpdateListenerMask()
    {
        if (closed) return;
        var mask = StatusMask.None;
        if (listener is not null || waiters.Count != 0) mask |= StatusMask.DataAvailable;
        if (subscriptionMatched is not null) mask |= StatusMask.SubscriptionMatched;
        if (requestedDeadlineMissed is not null) mask |= StatusMask.RequestedDeadlineMissed;
        if (livelinessChanged is not null) mask |= StatusMask.LivelinessChanged;
        if (mask != StatusMask.None || endpointListener is not null) Events.SetMask(mask);
    }

    private void DispatchStatus(Native.StatusData status)
    {
        if (status.Kind == (uint)StatusMask.DataAvailable) { DispatchData(); return; }
        Action? invoke = null;
        lock (Gate)
        {
            if (closed) return;
            switch ((StatusMask)status.Kind)
            {
                case StatusMask.SubscriptionMatched:
                    var onSubscriptionMatched = subscriptionMatched;
                    invoke = () => Events.Invoke(onSubscriptionMatched, ConvertSubscriptionMatched(status));
                    break;
                case StatusMask.RequestedDeadlineMissed:
                    var onRequestedDeadlineMissed = requestedDeadlineMissed;
                    invoke = () => Events.Invoke(onRequestedDeadlineMissed, ConvertRequestedDeadlineMissed(status));
                    break;
                case StatusMask.LivelinessChanged:
                    var onLivelinessChanged = livelinessChanged;
                    invoke = () => Events.Invoke(onLivelinessChanged, ConvertLivelinessChanged(status));
                    break;
            }
        }
        invoke?.Invoke();
    }
}
