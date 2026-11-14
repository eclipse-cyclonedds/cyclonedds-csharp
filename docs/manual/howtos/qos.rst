..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Set quality of service
======================

Pass a ``Qos`` value when creating an endpoint:

.. code-block:: csharp

   var qos = new Qos
   {
       Reliability = new(ReliabilityKind.Reliable, Duration.FromSeconds(3))
   };
   using var writer = participant.CreateWriter(topic, qos);
   using var reader = participant.CreateReader(topic, qos);

A writer must offer QoS compatible with the reader's requirements.
Policies include reliability, durability, history, resource limits, partitions
and lifecycle settings. The native runtime validates policy combinations.
``GetCoreQos()`` returns only history, reliability, durability and resource limits.

Set ``DataRepresentation = new[] { DataRepresentationKind.Xcdr1 }`` or
``DataRepresentationKind.Xcdr2`` to select the wire format explicitly.
