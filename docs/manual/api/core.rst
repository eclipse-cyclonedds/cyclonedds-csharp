..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Core types and lifetime
=======================

* ``Entity`` owns a native DDS handle and implements ``IDisposable``.
* ``Duration`` represents a nonnegative interval; ``Duration.Infinite`` disables a timeout.
* ``Timestamp`` represents nanoseconds since the Unix epoch.
* ``DdsException.Code`` exposes the native DDS return code.

Use ``using`` to release entities. Disposing a parent also disposes its children.

.. code-block:: csharp

   using var participant = new DomainParticipant(0);
   using var topic = participant.CreateTopic("Hello", Demo.Message.TypeSupport);

Set ``DdsRuntime.LibraryDirectory`` before the first DDS operation only when
using a custom native installation. ``CYCLONEDDS_CSHARP_NATIVE`` provides the
same override through the environment.
