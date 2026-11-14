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

/// <summary>A data-available registration. Callbacks run on the managed thread pool.</summary>
public sealed class DataAvailableListener<T> : IDisposable where T : class
{
    private readonly DataReader<T> reader;
    private readonly Action<DataReader<T>> action;
    private int active = 1;
    private Exception? failure;
    public Exception? LastException => Volatile.Read(ref failure);
    internal DataAvailableListener(DataReader<T> reader, Action<DataReader<T>> action)
    { this.reader = reader; this.action = action; }

    internal void Invoke()
    {
        if (Volatile.Read(ref active) == 0) return;
        try { action(reader); }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref failure, error, null);
            reader.RecordListenerException(error);
        }
    }

    /// <summary>Stops new notifications. An already running callback may finish after Dispose returns.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref active, 0) == 0) return;
        reader.RemoveDataListener(this);
    }
}
