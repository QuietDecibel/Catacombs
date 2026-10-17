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

This is an excerpt of the built-in `x32-default` profile that the service writes
to its `profiles/` folder on first run (the full file has 56 channels and six
layers). It is the format implemented in `XTouchMiddleware/src/XTouch.Core/Profiles`.

```json
{
  "schemaVersion": 1,
  "id": "x32-default",
  "name": "X32 – Inputs, Aux, Buses",
  "target": "x32",
  "channels": [
    { "key": "ch/01",    "label": "Ch 1",  "meter": 0,  "vars": { "solo": "01" } },
    { "key": "ch/02",    "label": "Ch 2",  "meter": 1,  "vars": { "solo": "02" } },
    { "key": "auxin/01", "label": "Aux 1", "meter": 32, "vars": { "solo": "33" } }
  ],
  "strip": {
    "fader": { "address": "{key}/mix/fader",     "converter": "x32-fader" },
    "mute":  { "address": "{key}/mix/on",        "converter": "inverted-toggle", "mode": "Toggle" },
    "solo":  { "address": "-stat/solosw/{solo}", "converter": "toggle",          "mode": "Toggle" },
    "name":  { "address": "{key}/config/name" },
    "color": { "address": "{key}/config/color" }
  },
  "layers": [
    { "id": "pan",   "label": "PAN",    "button": "PAN",
      "encoder": { "address": "{key}/mix/pan",      "converter": "pan",       "ring": "BoostCut", "step": 0.01, "default": 0.5 } },
    { "id": "trim",  "label": "TRIM",   "button": "TRACK",
      "encoder": { "address": "{key}/preamp/trim",  "converter": "x32-trim",  "ring": "BoostCut", "step": 0.01, "default": 0.5 } },
    { "id": "send1", "label": "SEND 1", "button": "SEND",
      "encoder": { "address": "{key}/mix/01/level", "converter": "x32-fader", "ring": "Wrap",     "step": 0.01, "default": 0 } }
  ],
  "defaultLayer": "pan",
  "master": { "address": "main/st/mix/fader", "converter": "x32-fader" },
  "masterLabel": "MAIN LR",
  "buttons": {
    "F1": { "address": "config/mute/1", "converter": "toggle", "mode": "Toggle" },
    "F2": { "address": "config/mute/2", "converter": "toggle", "mode": "Toggle" }
  }
}
```

Field reference:

| Field | Meaning |
|-------|---------|
| `channels[].key` | Unique key; usually the target's address prefix. Available as `{key}`. |
| `channels[].label` | Fallback scribble-strip name when the target sends no name. |
| `channels[].meter` | Index into the target's meter frame (X32 `/meters/1`). Omit for no meter. |
| `channels[].vars` | Extra per-channel placeholders, e.g. `{solo}`. |
| `strip.*` | Per-strip bindings. `rec` is also supported. `color` reads a 0–7 color index (mod 8). |
| `layers[]` | Encoder layers; `button` is the X-Touch button that selects the layer. |
| `master` | Binding for the master fader. |
| `buttons` | Global buttons by name (`F1`…`F8`, `PLAY`, `MARKER`, …; full list at `GET /api/reference`). |
| `mode` | `Toggle` (flip on press), `Momentary` (1 on press, 0 on release) or `Trigger` (sends `value`, default 1). |
| `converter` | `linear`, `toggle`, `inverted-toggle`, `pan`, `x32-fader`, `x32-trim`, `x32-gain`. |
| `ring` | `Dot`, `BoostCut`, `Wrap`, `Spread`. |

Placeholders resolved per channel:

| Placeholder | Example | Meaning |
|-------------|---------|---------|
| `{key}` | `ch/03` | The channel's key |
| `{n}` / `{n:00}` | `3` / `03` | 1-based position in `channels[]` |
| `{name}` / `{name:00}` | `{solo}` → `03` | Any entry in the channel's `vars` (numeric vars accept a format) |

Built-in surface behavior (not configurable in v1): BANK ◄/► page by 8,
CHANNEL ◄/► and the jog wheel shift by 1, NAME/VALUE toggles the bottom LCD line
between fader value and encoder value, encoder push resets to the layer's
`default`, SELECT highlights a channel. The 7-segment display shows the layer's
first two letters and the visible channel range.

## 8.3 Object model

`ProfileDocument` (JSON shape) is compiled by `ProfileCompiler` into an
immutable `CompiledProfile`:

```csharp
public sealed record Binding(ParameterAddress Address, IValueConverter Converter, ButtonMode Mode = ButtonMode.Toggle, double TriggerValue = 1);
public sealed record EncoderBinding(ParameterAddress Address, IValueConverter Converter, RingMode Ring, double Step, double Default);
public sealed record CompiledChannel(int Index, string Key, string Label, int? Meter,
    Binding? Fader, Binding? Mute, Binding? Solo, Binding? Rec, Binding? Name, Binding? Color,
    IReadOnlyDictionary<string, EncoderBinding> Encoders);
```

**Compile once, resolve fast.** Every placeholder is expanded at load time, and
a reverse index `address → channel indexes` (`ChannelsByAddress`) routes target
changes to strips. The engine's hot path does only dictionary lookups.

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
