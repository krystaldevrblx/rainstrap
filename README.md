<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://github.com/krystaldevrblx/rainstrap/raw/main/Images/Rainstrap-Dark.png">
  <source media="(prefers-color-scheme: light)" srcset="https://github.com/krystaldevrblx/rainstrap/raw/main/Images/Rainstrap-Light.png">
  <img src="https://github.com/krystaldevrblx/rainstrap/raw/main/Images/Rainstrap-Light.png" alt="rainstrap" width="820">
</picture>

**a lightweight roblox bootstrapper made to make roblox easier to launch, manage, and configure.**

[![Release](https://img.shields.io/github/v/release/krystaldevrblx/rainstrap?style=flat-square\&color=blue)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/krystaldevrblx/rainstrap/total?style=flat-square\&color=green)](https://github.com/krystaldevrblx/rainstrap/releases/latest)
[![License](https://img.shields.io/github/license/krystaldevrblx/rainstrap?style=flat-square)](LICENSE)

</div>

> [!NOTE]
> rainstrap requires **windows 10** or later.

## what is rainstrap?

rainstrap is a roblox bootstrapper for windows.
(ive tried to make it as lightweight as possible)

## features

* roblox player support
* roblox studio support
* roblox installation management
* repair and diagnostics
* update handling
* performance and graphics settings
* allowlisted fastflag configuration
* basic appearance customization
* simple installation and setup

### fastflags

rainstrap only supports fastflags that are currently on roblox's allowlist.

if a fastflag isn't on the allowlist, **rainstrap won't apply it.** there are no workarounds or ways to bypass the allowlist.

the allowlist can change over time, so some settings may stop working if roblox removes them.

## quick start

download the latest release from the [releases](https://github.com/krystaldevrblx/rainstrap/releases/latest) page.

run `setup.bat` and let it install everything rainstrap needs.

once that's done, open `rainstrap.exe` and you're good to go.

## system requirements

| requirement          | version     |
| -------------------- | ----------- |
| windows              | 10 or later |
| .net runtime         | 6.0         |
| vc++ redistributable | 2015–2022   |
| webview2             | required    |

## building

if you want to build rainstrap yourself, you'll need the **.net 6 sdk**.

clone the repository with submodules:

```bash
git clone --recursive https://github.com/krystaldevrblx/rainstrap.git
cd rainstrap
```

then build it with:

```bash
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Bloxstrap\Bloxstrap.csproj
```

or open `Rainstrap.sln` in visual studio and build it from there.

## credits

rainstrap is based on [fishstrap](https://github.com/fishstrap/fishstrap), which is based on [bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by **pizzaboxer**.

a lot of the original work comes from those projects, so please check them out if you're interested.

the original projects and attribution notices are preserved in the repository.

* [rainstrap](https://github.com/krystaldevrblx/rainstrap)
* [fishstrap](https://github.com/fishstrap/fishstrap)
* [bloxstrap](https://github.com/bloxstraplabs/bloxstrap)

---

**rainhub support is not currently included in rainstrap.**
