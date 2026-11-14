..
   Copyright(c) 2026 Sven Trittler

   This program and the accompanying materials are made available under the
   terms of the Eclipse Public License v. 2.0 which is available at
   http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
   v. 1.0 which is available at
   http://www.eclipse.org/org/documents/edl-v10.php.

   SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

Build from source
=================

Requires .NET SDK 9, CMake 3.20+, a C11 compiler and Python 3.9+.
From a clean checkout:

.. code-block:: bash

   bash scripts/bootstrap-native.sh
   cmake -S . -B build \
     -DCMAKE_PREFIX_PATH="$PWD/build/_deps/cyclonedds-install" \
     -DCMAKE_BUILD_TYPE=Release
   cmake --build build --config Release --parallel
   ctest --test-dir build -C Release --output-on-failure
   python3 scripts/pack-package.py \
     --prefix build/_deps/cyclonedds-install --build build

The package for the current host is written to ``artifacts/packages``.
CI combines desktop tools and desktop, Android and iOS runtimes into one package.

The bootstrap clones Cyclone DDS C ``master`` for development. A release tag
(``11.1.0`` or ``v11.1.0``) must match ``VERSION`` and selects the matching C tag.
The clone destination must be absent or empty; there is no update logic.
The native bridge must be built against the same C source as its runtime.

The official C tag ``11.1.0`` currently stalls when generating metadata for the
integration-test ``Types.idl``. Release compatibility with that tag is not verified.
