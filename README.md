<div align="center">

<img src="Images/icon.png" alt="Rainstrap" width="256">

# Rainstrap

**A performance-focused Roblox bootstrapper built for customization, optimization, and RainHub integration.**

</div>

> [!NOTE]
> Rainstrap is an application for **Windows 10 and above**.

## Features

* Detailed Roblox server information
* Support for Roblox Studio
* FastFlags editor

  * Configure supported Roblox FastFlags directly through Rainstrap
  * FastFlags not present in Roblox's allowlist cannot be applied
  * This restriction does not affect Roblox Studio
* Global Roblox settings editor

  * Adjustable frame-rate cap
  * Graphics quality controls
  * Additional client configuration options
* Performance-focused improvements and launch optimizations
* Custom bootstrapper styles, themes, and icons
* Cache cleaner
* Roblox channel switching
* RainHub integration and RainHub-powered features
* Additional quality-of-life improvements

## Building

Building Rainstrap requires the **.NET 6 SDK**.

Clone the repository with its submodules:

```bash
git clone --recursive https://github.com/krystaldevrblx/rainstrap.git
cd rainstrap
```

You can build the solution using Visual Studio or build directly from the command line:

```bash
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Bloxstrap\Bloxstrap.csproj
```

The resulting executable will be produced as `Rainstrap.exe`.

## Credits & Attribution

Rainstrap is a fork of [Fishstrap](https://github.com/fishstrap/fishstrap), which is itself based on [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by **pizzaboxer**.

Rainstrap builds upon the work of these projects and their contributors. Credit for the original software, libraries, and contributions belongs to their respective authors.

* **Rainstrap:** https://github.com/krystaldevrblx/rainstrap
* **Fishstrap:** https://github.com/fishstrap/fishstrap
* **Bloxstrap:** https://github.com/bloxstraplabs/bloxstrap

The applicable licensing and attribution notices from the upstream projects are preserved in this repository, including `LICENSE` and `LICENSE.Bloxstrap`.
