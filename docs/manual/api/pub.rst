..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Publishing
==========

``DataWriter<T>`` sends typed samples:

.. code-block:: csharp

   writer.Write(new Demo.Message { id = 1, text = "Hello" });

* ``Write(sample, timestamp)`` supplies a source timestamp.
* ``RegisterInstance``, ``LookupInstance`` and ``GetKey`` manage keyed instances.
* ``DisposeInstance`` and ``UnregisterInstance`` update instance lifecycle state.
* ``WriteDispose`` writes a sample and disposes its instance.
* ``Flush`` sends buffered writes when writer batching is enabled.
* ``WaitForAcknowledgments`` waits for reliable delivery acknowledgments.

``MatchedSubscriptions`` lists currently matched reader handles.

Typed events and status properties expose matching, deadline violations and
liveliness loss; see :doc:`../howtos/status`.
