# Claude Code prompts (send one at a time, check in Unity after each)

Run `claude` in the Unity project folder (the one with `Assets` and `ProjectSettings`).
After each prompt: let Unity recompile, press Play, compare with the design, then send the next prompt.
If Unity shows a red error, paste it to Claude Code and say "fix this compile error".

---
## 0. Check the foundation
```
Read CLAUDE.md and UI_SPEC.md. Then read every script in Assets/ArrowPuzzle/Scripts and check the
foundation (Theme, Ui, Icons, ArrowGraphic, AppScreen, GameApp) for compile errors, since you cannot run Unity.
Fix only real errors. Do not add features or screens. Tell me what you checked and what you fixed.
```
Expected in Play: cream screen with the text "ARROW PUZZLE foundation ready" in the Fredoka font.

---
## 1. Splash
```
Build the Splash screen exactly as in UI_SPEC.md section "1. Splash". Create Assets/ArrowPuzzle/Scripts/ScreenSplash.cs
(class ScreenSplash : AppScreen, UseSafeArea false for the dotted background). Include the dot-grid background, the
logo drawn with ArrowGraphic, the two-line title, the tagline, the loading bar and the animation described in the spec.
When it finishes call GameApp.I.Show("ScreenHome"). Do not touch any other file. Re-read your code for compile errors.
```

## 2. Home
```
Build the Home screen exactly as in UI_SPEC.md section "2. Home" in a new file ScreenHome.cs. Use Save for the level,
hints and star totals. Navigate by name: ScreenGame, ScreenLevels, and open PopupSettings. Do not touch other files.
```

## 3. Level select
```
Build the Level select screen exactly as in UI_SPEC.md section "3. Level select" in ScreenLevels.cs. Scrollable grid,
three card states (done, current, locked), back button to ScreenHome. Do not touch other files.
```

## 4. Game
```
Build the Game screen exactly as in UI_SPEC.md section "4. Game" in ScreenGame.cs. Use the existing BoardView and
LevelGen (do not change their logic; only visual constants if the spec needs it). Load level GameApp.I.CurrentLevel in
OnShow, wire hearts, arrows-left counter, Hint, Restart and Pause (open PopupPause) as in the spec, and open PopupWin /
PopupLose by name on Won / Lost (save stars and unlock the next level on win). Do not touch other screens.
```

## 5. Pause
```
Build PopupPause exactly as in UI_SPEC.md section "5. Pause" in PopupPause.cs (IsPopup true). Resume closes it and
restores Time.timeScale, Restart restarts the level, Sound toggles Save.Sound, Home goes to ScreenHome. Make sure
Time.timeScale is 1 again whenever the popup closes. Do not touch other files.
```

## 6. Win
```
Build PopupWin exactly as in UI_SPEC.md section "6. Win" in PopupWin.cs, including the star row, the text and the
confetti. NEXT LEVEL sets GameApp.I.CurrentLevel to the next level and shows ScreenGame; Home goes to ScreenHome.
```

## 7. Out of lives
```
Build PopupLose exactly as in UI_SPEC.md section "7. Out of lives" in PopupLose.cs. Watch ad uses Ads.ShowRewarded,
then closes the popup and gives the board one life back (BoardView.ReviveLife through ScreenGame). Restart and Home as in the spec.
```

## 8. Settings
```
Build PopupSettings exactly as in UI_SPEC.md section "8. Settings" in PopupSettings.cs with switches bound to
Save.Sound and Save.Haptics. CLOSE closes the popup.
```

## 9. Final pass
```
Read all screens and compare each against UI_SPEC.md. List every difference you find (sizes, colors, text, spacing),
then fix them. Also make sure the Android back button works on every screen and popup, and that Time.timeScale is never left at 0.
```
