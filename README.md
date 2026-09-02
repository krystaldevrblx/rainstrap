<div align="center">

<img src="Images/icon.png" alt="Rainstrap" width="256">

# Rainstrap

**A performance-focused Roblox bootstrapper built for customization, optimization, and RainHub integration.**

[![Release](https://img.shields.io/github/v/release/krystaldevrblx/rainstrap?style=flat-square&color=blue)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/krystaldevrblx/rainstrap/total?style=flat-square&color=green)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![License](https://img.shields.io/github/license/krystaldevrblx/rainstrap?style=flat-square)](LICENSE)

</div>

> [!NOTE]
> Rainstrap requires **Windows 10** or later.

## Features

- Detailed Roblox server information
- Roblox Studio support
- FastFlags editor with full configuration
- Global Roblox settings (frame-rate cap, graphics quality, and more)
- Performance-focused launch optimizations
- Custom bootstrapper styles, themes, and icons
- Cache cleaner
- Roblox channel switching
- RainHub integration
- One-click installer with automatic dependency setup

## Quick Start

Download the latest release from the [Releases](https://github.com/krystaldevrblx/rainstrap/releases/latest) page, then run `setup.bat` to install all required dependencies. After setup completes, launch `Rainstrap.exe`.

### System Requirements

| Requirement | Version |
|---|---|
| Windows | 10 or later |
| .NET Runtime | 6.0 (auto-installed by setup) |
| VC++ Redistributable | 2015-2022 (auto-installed by setup) |
| WebView2 Runtime | Latest (auto-installed by setup) |

## Building

Building from source requires the **.NET 6 SDK**.

Clone the repository with submodules:

```bash
git clone --recursive https://github.com/krystaldevrblx/rainstrap.git
cd rainstrap
```

Build from the command line:

```bash
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Bloxstrap\Bloxstrap.csproj
```

Or open `Rainstrap.sln` in Visual Studio and build from there.

The output will be produced as `Rainstrap.exe`.

## Credits & Attribution

Rainstrap is a fork of [Fishstrap](https://github.com/fishstrap/fishstrap), which is based on [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by **pizzaboxer**.

Credit for the original software, libraries, and contributions belongs to their respective authors. All applicable licensing and attribution notices are preserved in this repository (`LICENSE`, `LICENSE.Bloxstrap`).

- **Rainstrap:** https://github.com/krystaldevrblx/rainstrap
- **Fishstrap:** https://github.com/fishstrap/fishstrap
- **Bloxstrap:** https://github.com/bloxstraplabs/bloxstrap
