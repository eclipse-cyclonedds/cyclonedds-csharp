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

public sealed partial class DataWriter<T> where T : class
{
    private EndpointListener? endpointListener;
    private bool closed;
    private EndpointListener Events => endpointListener ??= new(this, DispatchStatus);
    /// <summary>First exception thrown by a listener or status event handler.</summary>
    public Exception? LastListenerException => endpointListener?.LastException;

    private EventHandler<PublicationMatchedStatus>? publicationMatched;
    public event EventHandler<PublicationMatchedStatus> PublicationMatched
    {
        add { lock (Gate) { _ = Handle; publicationMatched += value; UpdateListenerMask(); } }
        remove { lock (Gate) { publicationMatched -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public PublicationMatchedStatus PublicationMatchedStatus => ConvertPublicationMatched(EndpointListener.GetStatus(this, StatusMask.PublicationMatched));
    private static PublicationMatchedStatus ConvertPublicationMatched(Native.StatusData s) => new(s.TotalCount, s.TotalCountChange, s.CurrentCount, s.CurrentCountChange, s.LastHandle);

    private EventHandler<OfferedDeadlineMissedStatus>? offeredDeadlineMissed;
    public event EventHandler<OfferedDeadlineMissedStatus> OfferedDeadlineMissed
    {
        add { lock (Gate) { _ = Handle; offeredDeadlineMissed += value; UpdateListenerMask(); } }
        remove { lock (Gate) { offeredDeadlineMissed -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public OfferedDeadlineMissedStatus OfferedDeadlineMissedStatus => ConvertOfferedDeadlineMissed(EndpointListener.GetStatus(this, StatusMask.OfferedDeadlineMissed));
    private static OfferedDeadlineMissedStatus ConvertOfferedDeadlineMissed(Native.StatusData s) => new(s.TotalCount, s.TotalCountChange, s.LastHandle);

    private EventHandler<LivelinessLostStatus>? livelinessLost;
    public event EventHandler<LivelinessLostStatus> LivelinessLost
    {
        add { lock (Gate) { _ = Handle; livelinessLost += value; UpdateListenerMask(); } }
        remove { lock (Gate) { livelinessLost -= value; if (!closed) UpdateListenerMask(); } }
    }
    /// <summary>Reads the native status and resets its change counters.</summary>
    public LivelinessLostStatus LivelinessLostStatus => ConvertLivelinessLost(EndpointListener.GetStatus(this, StatusMask.LivelinessLost));
    private static LivelinessLostStatus ConvertLivelinessLost(Native.StatusData s) => new(s.TotalCount, s.TotalCountChange);

    private void UpdateListenerMask()
    {
        if (closed) return;
        var mask = StatusMask.None;
        if (publicationMatched is not null) mask |= StatusMask.PublicationMatched;
        if (offeredDeadlineMissed is not null) mask |= StatusMask.OfferedDeadlineMissed;
        if (livelinessLost is not null) mask |= StatusMask.LivelinessLost;
        if (mask != StatusMask.None || endpointListener is not null) Events.SetMask(mask);
    }

    private void DispatchStatus(Native.StatusData status)
    {
        Action? invoke = null;
        lock (Gate)
        {
            if (closed) return;
            switch ((StatusMask)status.Kind)
            {
                case StatusMask.PublicationMatched:
                    var onPublicationMatched = publicationMatched;
                    invoke = () => Events.Invoke(onPublicationMatched, ConvertPublicationMatched(status));
                    break;
                case StatusMask.OfferedDeadlineMissed:
                    var onOfferedDeadlineMissed = offeredDeadlineMissed;
                    invoke = () => Events.Invoke(onOfferedDeadlineMissed, ConvertOfferedDeadlineMissed(status));
                    break;
                case StatusMask.LivelinessLost:
                    var onLivelinessLost = livelinessLost;
                    invoke = () => Events.Invoke(onLivelinessLost, ConvertLivelinessLost(status));
                    break;
            }
        }
        invoke?.Invoke();
    }
}
