# Arrow Puzzle: UI spec (from the approved design)

The design is a set of 390 x 844 phone artboards. All numbers below are **design px**.
In Unity the canvas is 1080 units wide, so **1 design px = Theme.K = 2.769 units**.
Use `Ui.PlaceD(...)`, `Theme.D(...)` and the `...D` parameters of the Ui helpers; never type raw canvas units.
y is measured from the top edge unless it says "from bottom".

## Foundation (already written, reuse it)

| Need | Use |
|---|---|
| Colors, sprites, fonts | `Theme` |
| Image / Text / Button / Switch / layout groups | `Ui` |
| Icons (bulb, star, heart, grid, gear, back, pause, restart, home, play, sound, vibrate, lock, arrow right) | `Icons.Make(parent, Icon.X, color, sizeD)` |
| Arrow lines with a head | `ArrowGraphic` (`SetPoints`, `thickness`, `headSize`, `drawHead`, `widthFactor`) |
| Screen switching | `GameApp.I.Show("ScreenName")`, `Open("PopupName")`, `Close("PopupName")` |
| Progress and settings | `Save` (`Unlocked`, `Stars(level)`, `Hints`, `Sound`, `Haptics`) |

Screens are classes deriving `AppScreen` (file `ScreenXxx.cs`, popups `PopupXxx.cs` with `IsPopup => true`).
Popups get the dim backdrop from GameApp; their `Root` is the card holder, so build only the card.

### Colors
| Name | Hex | Theme field |
|---|---|---|
| Background cream | #F4EFE6 | Bg |
| Ink (text) | #26323F | Ink |
| Soft text | #5A6675 | InkSoft |
| White card | #FFFFFF | Card |
| Primary coral | #E4572E (edge #B23F1D) | Accent / AccentShadow |
| Teal (rewards, hint) | #2EC4B6 (edge #1F8E84) | Teal / TealShadow |
| Neutral edge | #E3DACB | CardShadow / Locked |
| Heart red / lost | #E63946 / #D9D3C7 | Error / HeartLost |
| Star gold | #FFB703 | Gold |
| Grid dot | #DDD5C6 | Dot |
| Switch off | #BDB5A6 | SwitchOff |
| Arrow colors | #FF6B6B #2EC4B6 #5C6BC0 #F4A300 #9B5DE5 #52B788 #F15BB5 #00A6FB | Palette[0..7] |

### Fonts (Fredoka, bundled)
`Weight.Medium` (500), `Weight.Semi` (600), `Weight.Bold` (700). Sizes below are the CSS font-size in design px.

### Shared parts
- **Raised button**: rounded rect, solid color, plus a bottom "edge" of the same shape shifted down by the edge size (use `Ui.Btn`, which adds it with a Shadow component). Pressed = scale 0.95 and the click sound (already in `Ui.Btn`).
- **Chip**: white, height H, radius H/2, content centered with gap 8.
- **Switch**: 52 x 30 pill, teal when on, #BDB5A6 when off, white knob 24 (`Ui.Switch`).
- **Safe area**: screens live inside the safe area. Top padding of a screen = 24 design px below the safe area (the design's 56 included a status bar). Side padding 24.

---

## 1. Splash (`ScreenSplash`)
- Background: cream with a faint dot grid over the **whole canvas** (`UseSafeArea => false`): dot tile 34 design px, dot radius 3, color Dot. Use an `Image` with `Theme.DotTile`, `Image.Type.Tiled`, `pixelsPerUnitMultiplier = 64 / (34 * Theme.K)`.
- Content is one centered column, 392 high: logo 190, gap 30, title 118, gap 30, tagline 24. Column centered on the screen.
- **Logo** 270 x 190 (svg coords, y down; convert to local `x = px - 135`, `y = 95 - py`, then times K), all with round joins/caps:
  - coral #E4572E: (30,160) (30,96) (124,96) (124,42) (220,42); thickness 22; head size 50
  - teal #2EC4B6: (70,30) (126,30); thickness 14; head size 32
  - indigo #5C6BC0: (236,168) (236,114); thickness 14; head size 32 (points up)
- **Title**: two lines, Bold 62, line height 59: "ARROW" (Ink) and "PUZZLE" (Accent), centered.
- **Tagline** "Tap. Slide. Escape." Medium 20, InkSoft.
- **Loading**: from bottom 110: bar 220 x 12 (radius 6, track #E3DACB, fill Accent), 12 below it the text "Loading…" Medium 15 InkSoft. Bar is the bottom of this block's text; the block is centered horizontally.
- **Animation**: the coral logo arrow grows along its path from the tail (ease in/out) for 1.8 s while the bar fills 0 to 100 %; the two small arrows are visible from the start. Hold 0.35 s, then `GameApp.I.Show("ScreenHome")`. Use unscaled time.

## 2. Home (`ScreenHome`)
- Padding: top 24, sides 24, bottom 44. Column layout.
- **Top row** (height 40, space between): left chip (padding left 10 / right 14): bulb icon 22 (Ink) + "{Save.Hints} hints" Semi 17. Right chip: star icon 22 (Gold) + total stars Semi 17 (sum of `Save.Stars(i)` for i = 1..Save.Unlocked-1).
- **Title** 26 below the row: "ARROW" Bold 54 Ink, "PUZZLE" Bold 54 Accent, line height 51, centered.
- **Art card** 26 below the title: white, radius 28, padding 12, inner art 318 x 212 centered. Inner art: dot grid (tile 32, radius 3, Dot) and arrows with thickness 12 (svg coords, y down, origin top-left of the 318 x 212 art):
  - #FF6B6B: (32,188) (32,112) (96,112) (96,48), head size 32 (up)
  - #2EC4B6: (144,188) (144,92) (244,92), head size 32 (right)
  - #F4A300: (144,32) (214,32), head size 30 (right)
  - #5C6BC0: (290,32) (290,156) (228,156), head size 30 (left)
  - #9B5DE5: stub (96,188) (110,188), no head
- **Bottom group** (pinned to the bottom padding), gap 16:
  - PLAY button: full width (342), height 104, radius 34, Accent, edge 6 AccentShadow. Two lines centered: "PLAY" Bold 40 white, "Level {Save.Unlocked}" Medium 18 white. Tap: `GameApp.I.CurrentLevel = Save.Unlocked; Show("ScreenGame")`.
  - Row of two buttons, gap 16, each half width, height 72, radius 24, white, edge 4 CardShadow, Ink text Semi 20, icon 26 + 10 gap + label: Grid icon "Levels" (Show "ScreenLevels") and Gear icon "Settings" (Open "PopupSettings").
- `OnShow`: refresh hint count, level and stars.

## 3. Level select (`ScreenLevels`)
- Padding: top 24, sides 24. Gap 24 between header and grid.
- **Header** (height 48): back button left (48 circle, white, edge 3 CardShadow, back icon 24 Ink stroke 2.6, goes to ScreenHome), title "Levels" Bold 32 centered.
- **Grid** (scrolls vertically): 4 columns, gap 10, card 78 x 96 (full width 342), radius 20. Show `max(24, Save.Unlocked + 12)` rounded up to a multiple of 4. Padding bottom 24.
- Card content: number Bold 28 centered 11 above the card center; stars/lock centered 17.5 below the card center.
  - Done (level < Unlocked): white, edge 4 CardShadow, number Ink, 3 stars of 15 (gap 3): earned Gold, others #E3DACB.
  - Current (level == Unlocked): Accent, edge 4 AccentShadow, number white, 3 stars white at 50 % alpha.
  - Locked: #E3DACB, no edge, number InkSoft, lock icon 20 in InkSoft instead of stars. Not tappable.
  - Tap open card: `CurrentLevel = n; Show("ScreenGame")`.

## 4. Game (`ScreenGame`)
- Padding: top 24, sides 24.
- **Header** (height 48, 3 columns: 1fr, auto, 1fr): left pause button (48 circle, white, edge 3 CardShadow, pause icon 22 Ink, opens "PopupPause"); center "Level {n}" Bold 30; right 3 hearts 28 gap 4 (Error for lives left, HeartLost for lost).
- **Chip** 14 below the header, centered: height 36, radius 18, white, padding 0 18: "Arrows left" Medium 18 InkSoft + gap 8 + count Bold 18 Ink.
- **Board card** 16 below the chip: white, radius 28, padding 12, centered in the free space down to the bottom buttons (keep 24 gap above them). Board cell size = fit the free space; board drawn by `BoardView`: dots 0.152 x cell (Dot), arrow thickness 0.196 x cell, head 0.6 x cell, colors from Palette.
- **Bottom buttons** (40 from bottom, side 24, gap 14, each half width, height 72, radius 24):
  - Hint: Teal, edge 4 TealShadow, bulb icon 26 Ink + "Hint" Bold 22 Ink. Badge: circle 30 Ink, white Bold 16 hint count, top-right corner offset (-6 right, -10 up) i.e. sticking out. Behaviour: `Save.Hints > 0` uses one (`BoardView.ShowHint`), otherwise `Ads.ShowRewarded` gives +2.
  - Restart: white, edge 4 CardShadow, restart icon 26 + "Restart" Bold 22 Ink.
- Flow: lives 3; blocked tap costs one; board `Won` sets `GameApp.I.ResultLives = board.Lives`, saves stars (= lives, min 1) and unlock, calls `Ads.LevelFinished()`, opens "PopupWin"; `Lost` opens "PopupLose".

## Popups (shared)
Dim backdrop is automatic (#26323F at 62 %). Card: width 326, white, radius 32, centered, raised edge 10 of rgba(0,0,0,0.12). Content is a vertical column, gap 14, items stretch to the card inner width.

## 5. Pause (`PopupPause`)
Card padding 30 / 24 / 26 (top / sides / bottom). Items:
1. Title "Paused" Bold 36 centered (extra 4 below).
2. RESUME: height 72, radius 24, Accent, edge 5 AccentShadow, play icon 24 white + "RESUME" Bold 24 white.
3. Restart: height 60, radius 20, bg #F4EFE6, Ink Semi 20, restart icon 24.
4. Sound row: height 60, radius 20, bg #F4EFE6, padding left 20 / right 18: sound icon 24 + "Sound" Semi 20 on the left, switch on the right.
5. Home: height 60, radius 20, bg #F4EFE6, home icon 24 + "Home".
Opening pauses the game (`Time.timeScale = 0`), closing restores it.

## 6. Win (`PopupWin`)
Card padding 34 / 24 / 26. Items centered:
1. "Level complete!" Bold 34.
2. Star row (margin 10 top / 4 bottom), items aligned to the bottom, gap 6: star 76 rotated +10° (left one rotated -10°), star 96 in the middle raised 14, star 76. Earned = Gold, else #E3DACB. Earned count = `GameApp.I.ResultLives` (min 1).
3. "Level {n} · {lives} lives left" Medium 20 InkSoft.
4. NEXT LEVEL (margin-top 10): height 72, radius 24, Accent, edge 5, "NEXT LEVEL" Bold 24 white + arrow-right icon 24 white (stroke 3). Starts level n+1.
5. Home: height 60, radius 20, bg #F4EFE6, home icon 24 + "Home".
Confetti: 36 small circles (18 to 34 px, Palette colors) burst up from the card top and fall for 2.2 s (unscaled).

## 7. Out of lives (`PopupLose`)
Card padding 34 / 24 / 26. Items centered:
1. "Out of lives" Bold 34.
2. 3 hearts of 52, gap 8, all HeartLost.
3. "Watch a short video to keep going with 1 extra life." Medium 19 InkSoft, centered, line height 1.35.
4. Watch ad (margin-top 8): height 72, radius 24, Teal, edge 5 TealShadow, play icon 26 Ink + "Watch ad · +1 life" Bold 22 Ink. Calls `Ads.ShowRewarded`, then closes and revives with 1 life.
5. Restart level: height 60, radius 20, bg #F4EFE6, restart icon 24.
6. Home: text only, height 52, "Home" Semi 19 InkSoft.

## 8. Settings (`PopupSettings`)
Card padding 30 / 24 / 22. Items:
1. "Settings" Bold 36 centered.
2. Sound row: height 64, radius 20, bg #F4EFE6, padding 20 / 18: sound icon 24 + "Sound" Semi 20, switch bound to `Save.Sound`.
3. Vibration row: same, vibrate icon 24 + "Vibration", switch bound to `Save.Haptics`.
4. CLOSE (margin-top 8): height 72, radius 24, Accent, edge 5 AccentShadow, "CLOSE" Bold 24 white.
5. "Version {Application.version}" Medium 16 InkSoft centered.
