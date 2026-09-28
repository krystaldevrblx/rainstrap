
<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/krystaldevrblx/rainstrap/main/Images/banner-dark.png">
  <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/krystaldevrblx/rainstrap/main/Images/banner-light.png">
  <img alt="Rainstrap" src="https://raw.githubusercontent.com/krystaldevrblx/rainstrap/main/Images/banner-light.png">
</picture>

<br>

[![License](https://img.shields.io/github/license/krystaldevrblx/rainstrap?style=flat-square)](https://github.com/krystaldevrblx/rainstrap/blob/main/LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/krystaldevrblx/rainstrap/ci-release.yml?branch=main\&style=flat-square\&label=build)](https://github.com/krystaldevrblx/rainstrap/actions)
[![Downloads](https://img.shields.io/github/downloads/krystaldevrblx/rainstrap/latest/total?style=flat-square)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![Release](https://img.shields.io/github/v/release/krystaldevrblx/rainstrap?style=flat-square)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![Stars](https://img.shields.io/github/stars/krystaldevrblx/rainstrap?style=flat-square)](https://github.com/krystaldevrblx/rainstrap)

</div>


 # **the best roblox bootstrapper (trust me bro)**

 note:
> rainstrap is an application for **Windows 10 and above.**

## Features

* detailed roblox server information (powered by RainHub)
* support for Roblox Studio
* fastflags editor

  * configure supported roblox fastflags directly through Rainstrap
  * fastflags not present in roblox's allowlist cannot be applied
  * This restriction does not affect roblox studio
* global roblox settings editor

  * adjustable frame-rate cap
  * graphics quality controls
  * additional client configuration options
* performance-focused improvements and launch optimizations
* custom bootstrapper styles, themes, and icons
* cache cleaner
* roblox channel switching
* RainHub integration and RainHub-powered features
* additional quality-of-life improvements

## Building

building Rainstrap requires the **.NET 6 SDK**.

clone the repository with its submodules:

```bash
git clone --recursive https://github.com/krystaldevrblx/rainstrap.git
cd rainstrap
```

you can build the solution using Visual Studio or build directly from the command line:

```bash
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Bloxstrap\Bloxstrap.csproj
```

the resulting executable will be produced as `Rainstrap.exe`.

## Credits & Attribution

Rainstrap is a fork of [Fishstrap](https://github.com/fishstrap/fishstrap), which is itself based on [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by **pizzaboxer**.

Rainstrap builds upon the work of these projects and their contributors. Credit for the original software, libraries, and contributions belongs to their respective authors.

* **Rainstrap:** https://github.com/krystaldevrblx/rainstrap
* **Fishstrap:** https://github.com/fishstrap/fishstrap
* **Bloxstrap:** https://github.com/bloxstraplabs/bloxstrap

The applicable licensing and attribution notices from the upstream projects are preserved in this repository, including `LICENSE` and `LICENSE.Bloxstrap`.

---

<div align="center">

**Rainstrap**

*A better way to manage Roblox on Windows.*

</div>
