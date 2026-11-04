# Fluid-device SFX — sourced candidates

Status: selected 2026-11-04
Scope: chemical purification, heating, cooling. Ice SFX unchanged.

## Final assets

| File | Purpose | Source duration | Peak |
|---|---|---:|---:|
| `assets/sfx_cooler.wav` | cooling / evaporation hiss | 0.711 s | −6 dBFS |
| `assets/sfx_heater.wav` | heating / bubbles | 0.258 s | −6 dBFS |
| `assets/sfx_filter_chemical.wav` | chemical purification | 0.225 s | −6 dBFS |

All are mono 48 kHz 16-bit PCM. Source audio was converted from the public preview MP3, peak-normalized to −6 dBFS, and given 3 ms terminal fades. No synthesis was used. Leading low-level attack was trimmed (cooling 10 ms, heating 40 ms, chemical 20 ms) so the audible onset lands closer to the device event; each trimmed clip starts with a 1 ms fade.

## Sources and licenses

### Cooling — Cigarette Sizzle 02

- Author: chaosportal
- Source: <https://freesound.org/people/chaosportal/sounds/133449/>
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
- Source description: “created by pouring water over a stove, the second of two”
- Page comments include: “Exactly the hiss/sizzle sound I needed! Fantastic.”
- Original: 0.721 s, 44.1 kHz, 16-bit, stereo.

### Heating — Bubbles_Single1.wav

- Author: BMacZero
- Source: <https://freesound.org/people/BMacZero/sounds/96124/>
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
- Source description: “A bubbling sound. Several bubbles bursting on the surface of a liquid in rapid succession.”
- Page rating: 18 ratings; comments include “Nice works!” and “fire”.
- Original: 0.321 s, 44.1 kHz, 16-bit, mono.

### Chemical purification — Menu FX 03 (ascending).wav

- Author: Nightflame
- Source: <https://freesound.org/people/Nightflame/sounds/422516/>
- License: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
- Source description: “The recording came from tapping on a wire sculpture. This one has a slight upward tone. Processed in Audacity.”
- Page rating: 30 ratings; comments include “Nice sound...” and “That one is cool, thank you!”
- Original: 0.245 s, 44.1 kHz, 16-bit, mono.

## Runtime mapping

`DeviceAudioClips` embeds and registers these WAVs; `DeviceAudioService` maps:

```text
filter.cleaned → fluxforge.filter.chemical
temperature.heated       → fluxforge.heater
temperature.cooled       → fluxforge.cooler
```

The original preview files used for audition are in `artifacts/audio-candidates/`.
