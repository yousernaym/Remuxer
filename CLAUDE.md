# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Role

`Remuxer` converts tracker modules and SID files into MIDI + WAV so the rest of **Visual Music** can treat
every source as MIDI. It is a git submodule (separate repo: `yousernaym/Remuxer`) with its own nested
submodule, `libRemuxer`.

Crucially, Visual Music invokes Remuxer **as a separate `remuxer.exe` process**, not as a linked library —
because the MonoGame and libsidplayfp licenses conflict. The launch site is
[../../VisualMusic/Project.cs](../../VisualMusic/Project.cs) (it builds a command line and `Process.Start`s
`remuxer/remuxer.exe`).

## Two layers

1. **Remuxer/** — a C# **console** front-end (`Remuxer.exe`, **.NET 10**). [Remuxer/Program.cs](Remuxer/Program.cs)
   parses the command line and drives conversion through P/Invoke into `libRemuxer.dll`
   ([Remuxer/LibRemuxer.cs](Remuxer/LibRemuxer.cs): `initLib`, `beginProcessing(ref Args)`, `process`,
   `endProcessing`, `closeLib`).
2. **libRemuxer/** (nested submodule) — the native C++ engine wrapping the vendored format libraries
   **libsidplayfp** 3.0.1 (+ its split-out ReSIDfp engine **libresidfp** 1.0.2) and **libopenmpt** (which in
   turn pulls in mpg123/ogg/vorbis). libopenmpt handles both tracker-module note extraction and audio
   rendering. All of these are included as projects in the repo-root `VisualMusic.sln`.

## CLI contract

`remuxer <input file> [-flags]` (see `showUsage` in [Remuxer/Program.cs](Remuxer/Program.cs)):

- `-m[path]` MIDI output (default `<input>.mid`), `-a[path]` WAV output (default `<input>.wav`); omitting both
  implicitly sets both.
- `-i` one MIDI track per instrument instead of per channel.
- `-t[base]` also render per-channel solo WAVs as `<base>-chCC.wav` (same filenames with or without `-i`).
- `-c[path]` cancel when this signal file exists.
- `-s<n>` SID sub-song, `-l<seconds>` SID song length, `-e` suppress conversion errors.

The `Args` struct (bottom of `Program.cs`) is marshaled to the native side; `numSubSongs` is an out value so
the app can prompt the user to pick a SID sub-song. Visual Music determines the sub-song separately and passes
it back in via `-s`.

## stdout contract (parsed by Visual Music)

Remuxer prints, in order:

1. one description line, e.g. `Extracting notes and audio from foo.mod`;
2. `Progress: N%` updated as `N` changes — rewritten in place with `\r` when stdout is a terminal, or emitted
   as separate newline-terminated lines when stdout is redirected (`Console.IsOutputRedirected`);
3. after processing (when `-t` was used), zero or more track-audio lines:
   - `TrackAudio: <miditrack>|<path>` — per-channel MIDI mode (`Filename` assign); path is `<base>-chCC.wav`
   - `TrackVoiceAudio: <miditrack>|<channel>|<path>` — per-instrument mode; same `-chCC.wav` path shared by
     instrument tracks on that channel (the app gates by note ownership)

Errors go to **stderr** and a **non-zero exit code** signals failure (no GUI message boxes). Visual Music
launches it with `RedirectStandardOutput`, scrapes the `Progress: N%` lines with a regex, and drives the
shared `ProgressWindow` (see [../../VisualMusic/Project.cs](../../VisualMusic/Project.cs) `ImportSong` and
[../../VisualMusic/ViewModels/MainViewModel.cs](../../VisualMusic/ViewModels/MainViewModel.cs) `DoImport`).

## Build & output

- Solution: [Remuxer.sln](Remuxer.sln) (or build via the repo-root `VisualMusic.sln`). `Remuxer.exe` is a
  .NET 10 console app (SDK-style csproj, output flattened to `bin\<Config>\` via
  `AppendTargetFrameworkToOutputPath=false` so Visual Music's copy step still finds it); `libRemuxer` and the
  vendored libs are C++/x64.
- Remuxer's post-build assembles `roms/` and the native DLLs (libRemuxer, libopenmpt) next to
  `Remuxer.exe`; VisualMusic's post-build then copies the whole Remuxer output into `<app output>\remuxer\`.

See [../../CLAUDE.md](../../CLAUDE.md) for the repo-wide picture. Note: most of `libRemuxer/` (openmpt,
sidplayfp and their dependencies) is vendored third-party code — treat it as upstream.
