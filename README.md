# Bonsai.VimbaX

A [Bonsai](https://bonsai-rx.org/) library for acquiring images from **Allied Vision** cameras (e.g. Alvium 1800 U) using the modern **Vimba X SDK** via the `VmbNET` .NET API.

This is a port of the original [`bonsai-rx/vimba`](https://github.com/bonsai-rx/vimba) package (`Bonsai.Vimba`, pinned to the legacy Vimba SDK 5.1.0) to **Vimba X**, which is Allied Vision's current GenICam-compliant SDK.

## Why this port

The original `Bonsai.Vimba` (v0.1.0, 2023):

- Requires the discontinued **Vimba SDK 5.1.0** (only Vimba X is distributed now).
- Does **not** work with newer Alvium cameras that need Vimba X (see bonsai-rx discussion [#1426](https://github.com/orgs/bonsai-rx/discussions/1426)).
- Is **Windows-only** — `VimbaNET.dll` is C++/CLI and fails on Linux/Mono.
- Requires launching Bonsai with `--noboot` to avoid an `AppDomain` crash.

`Bonsai.VimbaX` fixes all of the above:

- Uses **Vimba X** via the managed **`VmbNET`** API (`netstandard2.0`).
- Works on **Windows, Linux64 and Linux ARM** (Vimba X has native support).
- No `--noboot` hack — `VmbNET` is a clean managed wrapper, no `AppDomain` issue.

## Requirements

- **Vimba X SDK** must be installed on the machine running the camera. The `VmbNET`
  NuGet package contains only the managed wrapper — the GenTL **transport layers**
  (`.cti`), drivers and tools come from the SDK install.
  Download: https://www.alliedvision.com/en/products/software/vimba-x-sdk/
- Bonsai 2.8+ (the package targets `netstandard2.0`, compatible with .NET Framework 4.6.1+).

## The `VimbaCapture` source

Emits a sequence of `VimbaDataFrame` (an OpenCV.Net `IplImage` plus `FrameID` and `Timestamp`).

Properties:

| Property | Description |
|---|---|
| `Index` | Optional index of the camera to open (default 0). |
| `SerialNumber` | Optional camera serial number (dropdown lists connected cameras). |
| `FrameCount` | Optional number of frame buffers for continuous acquisition (0 = SDK default). |
| `SettingsFile` | Optional GenICam feature XML to load on open (`LoadSettings`). |

Supported pixel formats (converted to `IplImage`): `Mono8`, `BGR8`, `RGB8`, `BayerRG8`.
Other formats throw — extend `GetConverter` in `VimbaCapture.cs` to add more.

## Building

```bash
# Windows (default; for use inside Bonsai)
dotnet build Bonsai.VimbaX.sln -c Release

# Linux x64 / ARM (selects the matching VmbNET runtime package)
dotnet build Bonsai.VimbaX/Bonsai.VimbaX.csproj -c Release -p:VmbNetRid=linux-x64
dotnet build Bonsai.VimbaX/Bonsai.VimbaX.csproj -c Release -p:VmbNetRid=linux-arm64
```

The managed `VmbNET.dll` is identical across runtime packages; `VmbNetRid` only
controls which native libraries the NuGet restore pulls in.

## Smoke test (verify the camera + SDK without Bonsai)

`tools/VmbNETSmokeTest` is a small console app that exercises the exact API path the
Bonsai source uses: startup → enumerate → open → acquire frames → print dimensions
and pixel format. Run it on the machine with the camera attached:

```bash
dotnet run --project tools/VmbNETSmokeTest -c Release
```

- With a camera + Vimba X SDK: lists the camera and acquires ~30 frames.
- With no camera but SDK installed: prints `Found 0 cameras` and exits 0.
- Without the Vimba X SDK: fails at startup with `NoTL` (no transport layers) —
  confirming you still need to install the SDK.

## Status

Ported and compiles clean against `VmbNET` 1.3.2. **Runtime acquisition still needs
to be validated on real hardware** (Alvium 1800 U). Use the smoke test first.

## License

MIT (see [LICENSE](LICENSE)). Original work © NeuroGEARS Ltd.
