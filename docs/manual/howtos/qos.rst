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
``GetQos()`` reads all policies exposed by the binding. It returns managed copies;
absent policies are ``null``. ``GetCoreQos()`` remains available for the original
four-policy subset.

Set ``DataRepresentation = new[] { DataRepresentationKind.Xcdr1 }`` or
``DataRepresentationKind.Xcdr2`` to select the wire format explicitly.

Partitions
----------

Set matching partitions on publishers and subscribers:

.. code-block:: csharp

   using var publisher = participant.CreatePublisher(new Qos { Partitions = ["telemetry"] });
   using var subscriber = participant.CreateSubscriber(new Qos { Partitions = ["telemetry"] });
   using var writer = publisher.CreateWriter(topic, qos);
   using var reader = subscriber.CreateReader(topic, qos);

Participant properties
----------------------

.. code-block:: csharp

   using var participant = new DomainParticipant(0, new Qos
   {
       EntityName = "Telemetry service",
       Properties = [new("app.role", "publisher", Propagate: true)],
       BinaryProperties = [new("app.id", [1, 2, 3], Propagate: true)]
   });

``Propagate`` controls whether a property is sent through discovery. Its default
is ``false``. Names and string values must not contain NUL characters.
String and binary properties form one native Property policy; supply both lists
when replacing a complete property configuration.

Additional policies
-------------------

``Presentation``, ``TimeBasedFilter``, ``DurabilityService``, ``IgnoreLocal`` and
``TypeConsistency`` map to their native DDS policies. ``PsmxInstances`` selects
already configured native PSMX plugins; it does not install or configure plugins.

Use ``SetQos`` to change policies that the native runtime permits changing.
For example, ``participant.SetQos(participant.GetQos() with { UserData = [1, 2] })``
preserves the other policies. Configure partitions and properties at creation;
unsupported or immutable changes raise ``DdsException``.
