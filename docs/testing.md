# Evolution and stress tests

Build the native bridge, generated test types and managed tests using the normal
CMake build. Run the complete suite (including a separate 20-second stress test):

```sh
ctest --test-dir build -C Release --output-on-failure
```

The runtime suite covers independent IDL schema versions in `EvolutionTests.cs`:

- XCDR1 and XCDR2 mutable models, adding/removing members, stable member IDs across
  reordered declarations, high member IDs, absent/present optionals and nested evolution.
- Appendable old/new models in both directions, including default values for added fields.
- `EnumEvolutionTests.cs` covers explicitly appendable 32-bit and 8-bit enum versions,
  known/new literals in scalar members and sequences, old/new managed readers, the
  native C serializer/decoder and DDS delivery. Old readers discard unknown values
  with the default try-construct policy and continue receiving subsequent valid data.
  Undefined outgoing enum values fail serialization. Nonzero `@default_literal`
  values initialize constructors and missing appended fields; they do not replace
  unknown values under the default DISCARD policy. Explicit USE_DEFAULT/TRIM
  try-construct annotations are outside these tests.
- Unknown must-understand members fail; recognized must-understand members decode.
- Both versions interoperate through the native C serializer/decoder and through
  separate DDS participants with XTypes validation, including key-only dispose notifications.
- Independent little-/big-endian XCDR1 parameter-list fixtures use short and extended
  headers. They check unknown members, reordering, duplicate IDs, every truncated prefix,
  invalid lengths and malformed list terminators.

Known native limitation: the C reference decoder currently fills the missing XCDR1
32-bit appendable enum field with zero instead of its nonzero `@default_literal`;
the 8-bit field has been observed with either zero or the correct default.
The native default test executes and reports SKIP only for these observed results,
while verifying all other decoded fields;
other errors fail. A fixed native decoder automatically turns it into a pass.
This skip is counted separately from passes. The corresponding managed and DDS
default tests remain required for both representations, as does the native XCDR2
default test. Unknown-value rejection and all other native enum tests are required.

Filter the managed test executable to run only evolution tests:

```sh
CYCLONEDDS_CSHARP_NATIVE="$PWD/build/lib" \
  dotnet src/ddscsharp/tests/bin/Release/net9.0/Org.Eclipse.CycloneDDS.Tests.dll Evolution
```

On Windows or multi-configuration builds, select the corresponding native output
directory, for example `build/lib/Release`. DDS tests require local UDP sockets;
the CI workflow contains a loopback-only `CYCLONEDDS_URI` for restricted multicast environments.

The stress test alternates XCDR1/XCDR2 and repeatedly creates endpoints, validates
all received fields with payload sizes from 0 through 65,536 characters, replaces
listeners, cancels/reuses async waits, and races writes and cancellation against
parent/child disposal. Native parent/child queries verify that the entire captured
entity tree was deleted, including implicit publishers/subscribers and conditions.
Weak references verify collection of endpoints, listeners and waiter tasks.

After warmup, the test reports retained managed bytes, process private memory and
OS handle deltas. Broad growth guards are 16 MiB managed, 128 MiB process private
memory and fewer than 32 additional OS handles. These guards detect gross retention;
they are not a native heap leak profiler. Process memory also includes runtime,
thread-pool and allocator caches. Native entities and weak references are checked
on every cycle or checkpoint independently of those growth limits.

Run only the default soak:

```sh
ctest --test-dir build -C Release -L stress --output-on-failure -V
```

For a five-minute soak, invoke the executable directly (the CTest watchdog is
180 seconds). The duration setting accepts 1–3,600 seconds:

```sh
CYCLONEDDS_CSHARP_NATIVE="$PWD/build/lib" \
CYCLONEDDS_CSHARP_STRESS_SECONDS=300 \
  dotnet src/ddscsharp/tests/bin/Release/net9.0/Org.Eclipse.CycloneDDS.Tests.dll Stress
```

The normal runtime process excludes stress tests; CTest registers a dedicated,
serial process named `ddscsharp_stress`. A watchdog bounds hangs in CI.
