# Reference revisions

| Dependency | Revision | Purpose |
| --- | --- | --- |
| [Cyclone DDS C](https://github.com/eclipse-cyclonedds/cyclonedds) | Release builds: upstream tag matching `VERSION`; branch builds: current upstream `master` | Native runtime, IDL parser, XTypes metadata, runtime descriptors and serialization |
| [Cyclone DDS C++](https://github.com/eclipse-cyclonedds/cyclonedds-cxx) | `c185b86b6b689f8ae30234f583979b049c5dfab3` | API/test reference, license text and IDL fixture provenance |

The C++ repository was cloned temporarily outside this repository for inspection.
It is not a runtime or build dependency. The copied IDL fixtures and the test-name
inventory record the exact reference revision.

`scripts/bootstrap-native.sh` shallow-clones the selected C source into `build/_deps` and
installs a shared runtime there. For a release tag such as `11.0.1` or `v11.0.1`,
it requires `VERSION` to be `11.0.1` and clones the exact upstream C tag `11.0.1`.
This applies to GitHub tag builds and local detached checkouts at a tag. A missing
upstream tag or mismatched version fails the build; it never falls back to the
development branch. Builds without a release tag clone current upstream `master`.
The script always runs `git clone`; it does not reuse, update or delete an existing
checkout. The destination `build/_deps/cyclonedds-src` must be absent or empty.
Desktop, Android and iOS builds all use this same bootstrap script.
The official C tag `11.0.1` differs from the newer development source. Local
macOS ARM64 verification built its native libraries, but both C and C# IDL
generation stalled while producing type metadata for the integration-test
`Types.idl`. Release compatibility with that tag is therefore not yet verified;
the release policy does not bypass tests or substitute a newer C revision.
Builds use the upstream IDL compiler plugin ABI, so
the parser and generator must use matching Cyclone headers/libraries. The common
native bridge also consumes the internal typelib API and private entity
headers to register embedded XTypes metadata in a participant’s domain. Set
`CYCLONEDDS_SOURCE_DIR` to the selected source revision when building the bridge
against a separate installation. Consumers receive prebuilt matching binaries. Newer
native versions need their own verification; a minimum CMake version check alone
is not proof of ABI compatibility.

No external managed packages are required by the runtime, examples or tests.
Native dependency builds in the bootstrap script disable security and SSL;
applications needing those features should supply a suitably configured native
Cyclone build. The wrapper does not implement security plugin configuration APIs.
