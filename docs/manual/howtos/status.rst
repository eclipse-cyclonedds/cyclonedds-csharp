..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Monitor endpoint status
=======================

.. code-block:: csharp

   reader.SubscriptionMatched += (_, status) =>
       Console.WriteLine($"Matched writers: {status.CurrentCount}");
   reader.RequestedDeadlineMissed += (_, status) =>
       Console.WriteLine($"Missed deadlines: {status.TotalCount}");
   reader.LivelinessChanged += (_, status) =>
       Console.WriteLine($"Alive: {status.AliveCount}, unavailable: {status.NotAliveCount}");

Writers expose ``PublicationMatched``, ``OfferedDeadlineMissed`` and
``LivelinessLost`` events. Configure deadline and liveliness in ``Qos`` to detect
missed updates and expired liveliness leases. Matching reports compatible
endpoints, including disconnections.

Handlers run on the managed thread pool and can coexist with ``Listen`` and
``WaitDataAsync``. Inspect ``LastListenerException`` for handler failures; one
failing handler does not prevent others from running. Unsubscribing or disposing
stops future notifications; a handler already dispatched may still finish.

Each event also has a typed status property, such as
``reader.SubscriptionMatchedStatus``. Reading it resets the native change
counters; native listener delivery resets the corresponding counters too.
``MatchedSubscriptions`` and ``MatchedPublications`` list current peer handles.
Use ``SetStatusMask``, ``ReadStatus`` and ``TakeStatus`` for raw status flags.
