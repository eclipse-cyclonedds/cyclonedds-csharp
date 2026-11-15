..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Domain participants
===================

``DomainParticipant`` joins a DDS domain and creates topics and endpoints:

.. code-block:: csharp

   using var participant = new DomainParticipant(0);
   using var topic = participant.CreateTopic("Hello", Demo.Message.TypeSupport);
   using var writer = participant.CreateWriter(topic);
   using var reader = participant.CreateReader(topic);

Use matching domain IDs for communicating applications. Discovery also requires
matching topic names, compatible types and compatible QoS.
``CreatePublisher`` and ``CreateSubscriber`` provide explicit endpoint parents.

Cyclone DDS network configuration is supplied through ``CYCLONEDDS_URI``.
