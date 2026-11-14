..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Check endpoint matches
======================

.. code-block:: csharp

   var readers = writer.MatchedSubscriptions;
   var writers = reader.MatchedPublications;

Use the matched-handle lists to inspect discovery before exchanging samples.
``Entity.SetStatusMask``, ``ReadStatus`` and ``TakeStatus`` control and inspect
status flags. ``TakeStatus`` also clears the returned status changes.
Typed status counters and listeners for statuses other than data availability
are not yet implemented.
