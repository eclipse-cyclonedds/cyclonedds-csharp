# C# binding for Eclipse Cyclone DDS

[![Website](https://img.shields.io/badge/web-cyclonedds.io-blue)](https://cyclonedds.io)
[![Community](https://img.shields.io/badge/discord-join%20community-5865f2)](https://discord.gg/BkRYQPpZVV)

The official C# binding for [Eclipse Cyclone DDS](https://github.com/eclipse-cyclonedds/cyclonedds).

- [Quick Start](#quick-start)
- [Example](#example)

## Quick Start

Add the package from your project's directory:

```sh
dotnet add package Org.Eclipse.CycloneDDS
```

The package includes the C# API, native runtimes and automatic C# type generation
from IDL.

Then see [the example](#example) and [the docs](docs/manual/index.rst) to get started.

## Example

This example generates C# types from IDL and exchanges a DDS sample in one process.

### 1. Create a project and reference the package

```sh
dotnet new console --name DdsHello --framework net9.0
cd DdsHello
```

Replace `DdsHello.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Org.Eclipse.CycloneDDS" Version="11.1.0" />
    <CycloneDDSIdl Include="Types/**/*.idl" />
  </ItemGroup>
</Project>
```

The package supplies both runtime and build support. Compiler tools stay in the
NuGet cache and are not copied into the published application. Projects without
`CycloneDDSIdl` items do not invoke the compiler. Keep the normal package reference
so runtime dependencies can flow to projects consuming your library.

### 2. Describe your data in IDL

Create a `Types` directory and save this as `Types/Hello.idl`:

```idl
module Demo {
  @final struct Message {
    @key long id;
    string text;
  };
};
```

This generates the C# class `Demo.Message`. The IDL module determines its
namespace, and `@key` identifies the field used to distinguish DDS instances.

### 3. Write normal C# application code

Replace `Program.cs` with this small publisher and subscriber in one process:

```csharp
using Org.Eclipse.CycloneDDS;
using Demo;

using var participant = new DomainParticipant(0);
using var topic = participant.CreateTopic("Hello", Message.TypeSupport);
var qos = new Qos
{
    Reliability = new(ReliabilityKind.Reliable, Duration.FromSeconds(3))
};
using var reader = participant.CreateReader(topic, qos);
using var writer = participant.CreateWriter(topic, qos);
using var condition = reader.CreateReadCondition();
using var waitset = participant.CreateWaitSet();
waitset.Attach(condition);

writer.Write(new Message { id = 1, text = "Hello from C#!" });

if (waitset.Wait(Duration.FromSeconds(3)).Count == 0)
    throw new TimeoutException("No DDS sample received.");

foreach (var sample in reader.Take())
    if (sample.Info.ValidData)
        Console.WriteLine($"{sample.Data.id}: {sample.Data.text}");
```

`Message.TypeSupport` is generated alongside the data class and connects it to
the DDS runtime. Dispose DDS entities with `using`; disposing a participant also
disposes its children.

### 4. Build and run

```sh
dotnet build
dotnet run --no-build
```

Expected output:

```text
1: Hello from C#!
```

The build restores the package, converts the IDLs to `.cs` files under
`obj/Debug/net9.0/cyclonedds`, and compiles them with your application. There is
no separate generator command or generated native library to manage. Edit your
IDL and build again; included files and compiler-option changes are tracked.
`dotnet clean` removes generated output.

Keep generated files under `obj` and out of source control. Include every IDL
that defines types in `CycloneDDSIdl`, including definition files referenced by
other IDLs, and use unique IDL basenames within a project. See the
[IDL build guide](src/buildtools/README.md) for include paths, preprocessor
definitions and other build options.

More examples can be found under the folder `examples`.

## Build the package from source

Requires **.NET SDK 9**, **CMake 3.20+**, a **C11 compiler** and **Python 3.9+**.
Run from a clean checkout:

```sh
bash scripts/bootstrap-native.sh
cmake -S . -B build \
  -DCMAKE_PREFIX_PATH="$PWD/build/_deps/cyclonedds-install" \
  -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release --parallel
ctest --test-dir build -C Release --output-on-failure
python3 scripts/pack-package.py \
  --prefix build/_deps/cyclonedds-install --build build
```

The NuGet package for your platform is written to `artifacts/packages/`.
Its version comes from [VERSION](VERSION). To verify it:

```sh
python3 scripts/test-package.py \
  --package artifacts/packages/Org.Eclipse.CycloneDDS.11.1.0.nupkg \
  --build build
```

The bootstrap clones Cyclone DDS C `master`, or the matching C tag for a release.
See [the build guide](docs/manual/building.rst) for dependency selection and packaging
and [documentation build instructions](docs/how-to-build.md) to preview the manual.

## Repository structure

```bash
📂 cyclonedds-csharp
├── 📁 .github/             # CI workflows and platform matrix
├── 📂 src/
│   ├── 📂 ddscsharp/       # C# API
│   │   └── 📁 tests/       # integration tests and upstream IDL fixtures
│   ├── 📂 native/          # native C bridge
│   │   └── 📁 tests/       # C interoperability peer
│   ├── 📁 idlcsharp/       # C# type generator for IDL
│   └── 📁 buildtools/      # NuGet and MSBuild integration
├── 📂 examples/            # runnable publisher/subscriber examples
│   ├── 📁 HelloWorld/      # keyed message exchange
│   ├── 📁 Throughput/      # throughput measurements
│   └── 📁 RoundTrip/       # latency measurements
├── 📁 cmake/               # CMake integration for type generation
├── 📁 scripts/             # dependency setup, builds and package verification
├── 📁 docs/                # Sphinx documentation (API guide and how-tos)
├── 📄 CMakeLists.txt       # native build and test configuration
├── 📄 CycloneDDS.sln       # .NET solution
├── 📄 VERSION             # package version
├── 📄 LICENSE             # repository licensing terms
└── 📄 README.md           # introduction and getting started
```
