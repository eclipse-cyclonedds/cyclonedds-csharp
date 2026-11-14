..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Topics and type support
=======================

``Topic<T>`` combines a topic name with generated ``TypeSupport<T>``.
It exposes ``Name``, ``TypeName`` and ``Support``.

.. code-block:: csharp

   using var topic = participant.CreateTopic("Hello", Demo.Message.TypeSupport);

IDL ``@key`` members identify instances within a topic. Generated metadata
provides the native sample layout and DDS discovery type information.
Use :doc:`../idlcsharp` to add schemas to your project.
