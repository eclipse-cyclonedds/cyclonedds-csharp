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

public readonly record struct PublicationMatchedStatus(uint TotalCount, int TotalCountChange, uint CurrentCount, int CurrentCountChange, ulong LastSubscriptionHandle);
public readonly record struct SubscriptionMatchedStatus(uint TotalCount, int TotalCountChange, uint CurrentCount, int CurrentCountChange, ulong LastPublicationHandle);
public readonly record struct OfferedDeadlineMissedStatus(uint TotalCount, int TotalCountChange, ulong LastInstanceHandle);
public readonly record struct RequestedDeadlineMissedStatus(uint TotalCount, int TotalCountChange, ulong LastInstanceHandle);
public readonly record struct LivelinessLostStatus(uint TotalCount, int TotalCountChange);
public readonly record struct LivelinessChangedStatus(uint AliveCount, uint NotAliveCount, int AliveCountChange, int NotAliveCountChange, ulong LastPublicationHandle);
