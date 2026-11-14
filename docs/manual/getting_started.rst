..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Getting started
===============

Requires the .NET 9 SDK. Create a console project and add the package:

.. code-block:: bash

   dotnet new console --name DdsHello --framework net9.0
   cd DdsHello
   dotnet add package Org.Eclipse.CycloneDDS

Add this item group inside your project file's ``Project`` element:

.. code-block:: xml

   <ItemGroup>
     <CycloneDDSIdl Include="Types/**/*.idl" />
   </ItemGroup>

Save the following as ``Types/Hello.idl``:

.. literalinclude:: ../../README.md
   :language: idl
   :start-after: ```idl
   :end-before: ```

Replace ``Program.cs`` with:

.. literalinclude:: ../../README.md
   :language: csharp
   :start-after: ```csharp
   :end-before: ```

Build and run:

.. code-block:: bash

   dotnet run

Expected output: ``1: Hello from C#!``.
The build generates ``Demo.Message`` and its ``TypeSupport`` automatically.
For separate processes, match the domain ID, topic name, data type and QoS.
