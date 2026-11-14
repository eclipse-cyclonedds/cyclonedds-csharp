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
provides DDS discovery type information and managed XCDR1/XCDR2 codecs.
Normal writes and reads serialize directly between C# objects and CDR bytes.
Final structs without optional members use generated field-specific operations;
other types and key-only samples use the common XCDR codec. Both paths retain
bounds and nesting checks. The generator proves when a type graph is acyclic;
recursive and unknown graphs retain reference-cycle checks. Numeric field runs
use checked scalar blocks, and numeric sequences and arrays use endian-aware
bulk transfer. Decoded messages always own their managed data.
Cyclone DDS handles transport and keyhash calculation, including MD5 for long keys.
Native sample marshalling remains for instance-control operations, query predicates
and custom codecs without CDR support.

``TypeSupport<T>.Serialize(sample, representation)`` returns encapsulated CDR bytes;
``Deserialize(bytes)`` decodes them.
Use :doc:`../idlcsharp` to add schemas to your project.
