# Arrow Puzzle: rules for Claude Code

Unity 6 mobile game (portrait, Android). Whole UI is built **in code**; never edit scenes, prefabs or the Inspector.

- Follow `UI_SPEC.md` exactly (colors, sizes, spacing, texts). It is the approved design.
- Work on **one screen at a time**, only the screen named in the prompt. Do not touch other screens or the game logic unless the prompt says so.
- One screen = one file in `Assets/ArrowPuzzle/Scripts/`: `ScreenXxx.cs`, or `PopupXxx.cs` (override `IsPopup => true`), deriving from `AppScreen`. GameApp finds it automatically; do not register it anywhere.
- Build UI only with `Ui`, `Theme`, `Icons` and `ArrowGraphic`. Sizes are design px (`Ui.PlaceD`, `Theme.D`, the `...D` parameters). Never hard-code canvas units or colors; use `Theme` fields.
- Fonts only through the `Weight` enum (Fredoka files are in `Resources/Fonts`).
- Navigate by name (`GameApp.I.Show("ScreenHome")`, `Open("PopupPause")`) so screens can be built in any order. A missing screen only logs a warning.
- Do not change `Theme`, `Ui`, `Icons`, `GameApp`, `AppScreen` unless a screen truly needs something missing; then make additive changes only and say so.
- You cannot run Unity: re-read your code for compile errors (missing usings, wrong names) before finishing.
- Finish with a short report: file(s) created, what the screen does, anything that differs from the spec.

## Modes
- Three modes, `enum GameMode { Classic, Shapes, Mosaic }`, 50 levels each (`ModeInfo.LevelsPerMode`). `GameApp.I.CurrentMode` + `CurrentLevel` say what is being played.
- Display data per mode: `ModeInfo.Get(mode)` -> `Name`, `Tagline`, `Color`, `Edge`, `Tint`, `UnlockHint`. Use it instead of hard-coding mode colors or texts.
- Level data: always `Levels.For(mode, n)`. Classic = `LevelGen.Generate`; Shapes / Mosaic come from `Resources/Levels/shapes.txt` / `mosaic.txt` via `ShapeLevels` (have `LevelData.mask[x, y]`, y up, and `shape`). An editor self check logs "ShapeLevels: 100 levels OK" on play.
- Progress per mode: `Save.GetUnlocked(mode)` / `SetUnlocked(mode, v)`, `Save.Stars(mode, n)` / `SetStars(mode, n, s)`, `Save.IsModeUnlocked(mode)`, `Save.TotalStars()`. Classic uses the old keys (`Save.Unlocked`, `Save.Stars(n)` still work). `Save.UnlockAllModes` is a dev switch: set it false before release.
