..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Subscribing
===========

``DataReader<T>.Read()`` keeps samples in the reader; ``Take()`` removes them.
Both return managed copies with ``Data`` and ``Info``:

.. code-block:: csharp

   foreach (var sample in reader.Take())
       if (sample.Info.ValidData)
           Console.WriteLine(sample.Data.text);

Pass ``maxSamples``, ``state`` or ``instanceHandle`` to limit access.
The default maximum is 1024 samples per call. Read/take can also use a read or
query condition. Check ``Info.ValidData`` before accessing ordinary data fields;
invalid-data notifications carry instance keys and lifecycle information.

``WaitDataAsync(cancellationToken)`` awaits unread samples without consuming them.
It returns ``false`` if the reader is disposed while waiting.

``MatchedPublications`` lists currently matched writer handles. Typed events and
status properties expose matching, deadline violations and liveliness changes;
see :doc:`../howtos/status`.
