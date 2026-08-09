# Nexus mod page copy

One block per field in the Nexus description form.

## Description

Wagon Destination puts the destination on the car itself.

Every car working a job gets an extra line on its info plates, right above the job id: the track that job has to leave it on. Walk the consist and read where each wagon goes — no booklet, no map, no memorising the cut order.

The same line shows up in the locomotive HUD's car info panel, so you can check a car from the cab.

## Installation instructions

1. Install [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) and point it at your Derail Valley install.
2. Download the zip from the Files tab.
3. Drop the zip onto UMM's Mods tab (or unpack it into `Derail Valley/Mods/`, giving you `Derail Valley/Mods/WagonDestination/`).
4. Start the game. The mod shows up in UMM's mod list as "Wagon Destination".

No new game required — the line appears on any car that already has a job.

## Main features

- Destination track shown on both info plates of every car that a job references, aligned in the job id's column.
- Same destination line in the loco HUD's car info panel.
- Shunting-aware: for a car moved through several tracks, the line shows the last track the job leaves it on, not the first.
- Falls back to the job chain's destination yard when no task names a track.
- Appears and clears with the job id, so cars without work look stock.
- Reads the live job data — no setup, no per-car configuration, nothing to keep in sync.

## Requirements

- Derail Valley, build 99.x
- [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) 0.27 or newer

No other mods needed. The destination is read from whatever job currently owns the car, through the game's own job manager, so job-generating mods are not special-cased.

## Shout outs

- Altfuture, for a game worth modding.
- Andreas Pardeike, for [Harmony](https://github.com/pardeike/Harmony).
- newman55, for Unity Mod Manager.
- The Derail Valley modding community, whose open-source mods are the reference for how to hook into this game.
