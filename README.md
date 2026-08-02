# dv-wagon-destination

A mod for [Derail Valley](https://store.steampowered.com/app/588030/Derail_Valley/).

## Requirements

- Derail Valley (build 99.x)
- [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)

## Installation

Install through Unity Mod Manager like any other DV mod: point UMM at the game, then drop the release zip onto its Mods tab (or unpack into `Derail Valley/Mods/WagonDestination`).

## Building

`Directory.Build.targets` points MSBuild at your Derail Valley install so the game assembly references resolve. Adjust `DvInstallDir` for your machine, then:

```powershell
dotnet build          # build
.\pack.ps1            # build + package release zip
```

## License

MIT
