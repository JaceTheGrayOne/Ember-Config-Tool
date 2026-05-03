# Ember Config Tool

Ember Config Tool is a config editor for the Enshrouded mod: Ember

This tool saves user configs to the User Config Override file supplied by Ember:

```
src\User_Config_Overrides.lua
```

It preserves pre-existing manually configured Lua and writes a managed block at the bottom of the config file.

## How To Use

Run the Ember Config Tool from `mods\Ember`. If for some reason `src\mod.lua` or `src\Config` cannot be found, the tool will show an embedded
default set of configs, disable saving, and display an error.

Each section of the config will only be written if the master "Enabled" toggle is switched on and each configured value within each section will only be written if its individual "Enabled" toggle is switched on, values that do not have individual toggles will be written as long as the master toggle is switched on.

The Terraforming `Terrain Material Replacer`, `Block Material Replacer`, and
`Terraforming Properties` subsections are intentionally hidden for now as they require specialized schema and handling which I have not had the time to sort out yet.

I am not providing build instructions although everything needed to build this app is included in this repo because I have no intention of approving pull requests or partnering in development as this tool is heavily dependent on the source shape of Ember itself. Feel free to build and even release your own version of this if you want but that's not something I'm going to actively support.

## Presets

Presets are saved as JSON files stored under:

```
%AppData%\Ember\ConfigTool\Presets
```

Each saved preset will contain the configured settings at the time of preset creation and loading a preset will restore each config to that state but will not write to the config override file until an actual save is performed.

## Safety Notes

- Managed blocks with older compatible schema versions are loaded with a
  warning but should work.
- Duplicate or incorrect configurations inside the managed block will block saving until
  the file is manually fixed. (I plan to introduce auto-repair)
- Supported pre-existing manual configurations are imported when no managed block exists and are
  moved into the managed block on Save.
- Unsupported pre-existing manual configurations outside the managed block are preserved with a warning.

## Trust Statement

This app has no telemetry, records no user data, and requires no network access. It uses the .NET runtime and Windows desktop APIs only. Release builds are produced from the checked-in source, and published artifacts include `SHA256SUMS.txt` for checksum verification.

Release executables are unsigned.

## Dependencies

- .NET 8 Windows Desktop runtime.