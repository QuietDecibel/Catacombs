# 8. Configuration & Mapping

Two kinds of configuration:

| Kind | File | Changed by | Reload |
|------|------|-----------|--------|
| **Settings** (ports, hosts, API, logging) | `appsettings.json` (+ `appsettings.Local.json`, env vars) | Admin / Settings screen | Restart of the affected worker |
| **Profiles** (what controls do) | `profiles/*.json` | Mapping editor | Hot-reload, no restart |

> **⚙ Reconcile** — If your architecture already defines a mapping format,
> keep it and write a loader that produces the `Profile` object model below;
> the engine depends only on the object model.

## 8.1 Profile concepts

```
Profile
├─ channels[]          ordered list of target channels the banks page through
├─ strip template      what each strip's controls do, with {ch} placeholders
├─ layers[]            alternative encoder (and optionally fader) assignments
├─ globals             master fader, transport, F-keys, view buttons
└─ display             what LCD lines and 7-segment show
```

Banking = choose which window of `channels[]` the 8 strips show.
Layers = choose which binding set the encoders use.
"Flip" = a built-in layer that swaps encoder and fader bindings.

## 8.2 Example profile (X32)

```json
{
  "$schema": "./profile.schema.json",
  "id": "x32-default",
  "name": "X32 – Inputs 1-32",
  "target": "x32",
  "channels": [
    { "key": "ch01" }, { "key": "ch02" }, { "key": "ch03" }, { "key": "ch04" },
    { "key": "ch05" }, { "key": "ch06" }, { "key": "ch07" }, { "key": "ch08" },
    { "key": "bus01", "label": "Mon 1" }
  ],
  "strip": {
    "fader":   { "address": "{path}/mix/fader", "converter": "x32-fader" },
    "mute":    { "address": "{path}/mix/on",    "converter": "inverted-toggle", "mode": "toggle" },
    "solo":    { "address": "-stat/solosw/{n:00}", "converter": "toggle", "mode": "toggle" },
    "select":  { "action": "select-channel" },
    "name":    { "address": "{path}/config/name" },
    "color":   { "address": "{path}/config/color", "converter": "x32-color" },
    "meter":   { "source": "input:{n}" },
    "encoderPush": { "action": "reset-to-default" }
  },
  "layers": [
    { "id": "pan",  "button": "PAN",  "encoder": { "address": "{path}/mix/pan",       "converter": "pan",  "ring": "BoostCut", "step": 0.01, "default": 0.5 } },
    { "id": "gain", "button": "TRACK","encoder": { "address": "{path}/preamp/trim",   "converter": "trim", "ring": "BoostCut", "step": 0.01, "default": 0.5 } },
    { "id": "send1","button": "SEND", "encoder": { "address": "{path}/mix/01/level",  "converter": "x32-fader", "ring": "Wrap", "step": 0.01, "default": 0.0 } }
  ],
  "defaultLayer": "pan",
  "globals": {
    "master": { "address": "main/st/mix/fader", "converter": "x32-fader" },
    "buttons": {
      "F1": { "address": "config/mute/1", "converter": "toggle", "mode": "toggle" },
      "F2": { "address": "config/mute/2", "converter": "toggle", "mode": "toggle" }
    }
  },
  "display": {
    "lcdBottom": "value",
    "sevenSegment": "BANK {bank:00}  {layer}"
  }
}
```

`display.lcdBottom` is one of `value`, `name2` or `layer`. (Profiles are
plain JSON; if you want comments in them, load with
`JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip }`.)

The F1/F2 examples toggle X32 mute groups 1 and 2.

Placeholders resolved per strip:

| Placeholder | Example | Meaning |
|-------------|---------|---------|
| `{key}` | `ch03` | Channel key from `channels[]` |
| `{path}` | `ch/03` | Adapter-provided address prefix for the key |
| `{n}` / `{n:00}` | `3` / `03` | 1-based channel number within its type |
| `{bank}` | `1` | Current bank (1-based) |

Button names (`PAN`, `F1`, `PLAY`…) are resolved to MCU notes through a
lookup table generated from `McuConstants`, so profiles stay readable.

## 8.3 Object model

```csharp
public sealed record Profile(
    string Id, string Name, string Target,
    IReadOnlyList<ChannelRef> Channels,
    StripTemplate Strip,
    IReadOnlyList<Layer> Layers, string DefaultLayer,
    Globals Globals, DisplayOptions Display)
{
    public int ChannelCount => Channels.Count;
    // ResolveFader / ResolveEncoder / ResolveButton / BuildStripView / StripsShowing
    // are implemented in ProfileResolver (Core), cached per (bank, layer).
}

public sealed record Binding(
    string AdapterId, ParameterAddress Address, IValueConverter Converter,
    bool IsToggle, double Step = 0.01, double Default = 0);
```

**Compile once, resolve fast.** On load, `ProfileCompiler` expands every
placeholder for every channel into a dictionary
`(channelIndex, controlKind) → Binding` and a reverse index
`ParameterAddress → channelIndex[]`. The engine hot path then does only
dictionary lookups (no string formatting per fader message).

## 8.4 Validation

`ProfileValidator` runs on load and on every save. It must reject:

- Unknown `target`, unknown converter names, unknown button names.
- Addresses not present in the adapter's `TargetCatalog` (warning, not error,
  for adapters with open-ended address spaces).
- Duplicate layer IDs or two layers on the same button.
- `defaultLayer` not in `layers`.
- Zero channels.

An invalid profile never replaces the running one: the engine keeps the old
profile and the UI shows the errors.

## 8.5 Hot reload

```csharp
public sealed class FileProfileProvider : IProfileProvider, IDisposable
{
    private readonly FileSystemWatcher _watcher;
    // On Changed/Created: debounce 250 ms (editors write files in several steps),
    // load + validate, then post UiCommand(new LoadProfileCommand(id)) to the engine channel.
}
```

The engine applies the new profile between input batches and calls
`RepaintAll()`, so the surface updates in one render tick.

## 8.6 Settings precedence

Standard .NET configuration order (later wins):
1. `appsettings.json` (shipped defaults)
2. `appsettings.{Environment}.json`
3. `appsettings.Local.json` (written by the Settings screen; not overwritten on upgrade)
4. Environment variables (`XTOUCH_Surface__InputPortName=...`)
5. Command line (`--Surface:InputPortName=...`)

```csharp
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("XTOUCH_");
```
