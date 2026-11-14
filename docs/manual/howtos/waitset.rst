..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Wait for data
=============

Use a read condition to wait without polling:

.. code-block:: csharp

   using var condition = reader.CreateReadCondition();
   using var waitset = participant.CreateWaitSet();
   waitset.Attach(condition);
   if (waitset.Wait(Duration.FromSeconds(3)).Count > 0)
       foreach (var sample in reader.Take())
           if (sample.Info.ValidData)
               Console.WriteLine(sample.Data.text);

``Wait`` returns an empty list on timeout and accepts a cancellation token.
A guard condition can wake the wait set from application code.

Use ``reader.CreateQueryCondition(sample => sample.id == 1)`` for managed
filtering. Query predicates must not call DDS operations. Predicate errors surface
on query access or through ``ThrowIfFilterFailed()``.
