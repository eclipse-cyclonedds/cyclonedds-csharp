..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Receive data callbacks
======================

.. code-block:: csharp

   using var listener = reader.Listen(source =>
   {
       foreach (var sample in source.Take())
           if (sample.Info.ValidData)
               Console.WriteLine(sample.Data.text);
   });

Callbacks run on the managed thread pool. Keep the listener alive while receiving;
inspect ``listener.LastException`` for callback failures. Disposing the listener
prevents new notifications; a callback already running may still finish.
