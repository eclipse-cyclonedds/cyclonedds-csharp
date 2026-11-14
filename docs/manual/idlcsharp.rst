..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

C# type generation
==================

Declare IDL inputs in your project file:

.. code-block:: xml

   <ItemGroup>
     <CycloneDDSIdl Include="Types/**/*.idl" />
     <CycloneDDSIdlInclude Include="SharedTypes" />
     <CycloneDDSIdlDefine Include="FEATURE=1" />
   </ItemGroup>

``dotnet build`` generates C# under ``obj/<configuration>/<framework>/cyclonedds``
and compiles it automatically. ``dotnet clean`` removes generated output.
Include every IDL that defines types, including files referenced by other IDLs.
Use unique IDL basenames within a project.

Mapping
-------

* Modules become namespaces; structs become classes with ``TypeSupport``.
* Strings, enums, bitmasks, unions, arrays, sequences and optional members are supported.
* Sequences implement ``Omg.Types.ISequence<T>``; typedefs resolve to their underlying type.
* Copy constructors and equality compare member values.

Current limits include struct inheritance, ``@external``, maps, wide strings/chars,
fixed-point and long-double types, and recursive arrays. Outgoing object graphs
must be acyclic and no more than 256 objects deep.

Deployment
----------

Generation uses the build host's tools; native runtime selection follows the
application's target RID. The installed package must contain both required platforms.
Keep the normal package reference in schema libraries so their runtime dependency
reaches consuming applications.
