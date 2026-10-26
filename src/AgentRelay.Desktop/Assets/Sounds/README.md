# Notification cues

The three PCM WAV cues are project-owned audio under the repository's MIT license.
They are embedded in the desktop application and do not use Windows sound-scheme aliases.

| File | Pattern | Duration |
| --- | --- | --- |
| `default.wav` | One bright 1046.5 Hz note | 200 ms |
| `information.wav` | Two rising notes, 698.46 Hz then 1046.5 Hz | 370 ms |
| `warning.wav` | Three low 330 Hz pulses | 520 ms |

All files use mono, 22050 Hz, 16-bit PCM with short fade envelopes. Playback respects
Windows volume controls. The error option retains the Windows critical-error sound.
