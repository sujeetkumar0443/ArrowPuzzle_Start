# Arrow Puzzle: start package

What is inside
- `Assets/ArrowPuzzle/Scripts`: foundation (Theme, Ui, Icons, ArrowGraphic, AppScreen, GameApp) and the game logic (LevelGen, BoardView, Save, Sfx, Ads). No screens yet.
- `Assets/ArrowPuzzle/Resources/Fonts`: Fredoka Medium / SemiBold / Bold (SIL Open Font License, see OFL-Fredoka.txt).
- `UI_SPEC.md`: the approved design as exact numbers. `CLAUDE.md`: rules Claude Code follows. `PROMPTS.md`: the prompts, one per screen.

Setup
1. Unity 6: new **2D** project (new folder, not the old Arrow Puzzle or starter-kit projects).
2. Copy the `Assets/ArrowPuzzle` folder into the project's `Assets`, and `UI_SPEC.md`, `CLAUDE.md`, `PROMPTS.md` next to `Assets` (the project root).
3. Edit > Project Settings > Player: Default Orientation = Portrait. Set your package name.
4. Press Play. You should see a cream screen with "ARROW PUZZLE foundation ready" in Fredoka.
5. Open a terminal in the project root, run `claude`, and send prompts from `PROMPTS.md` one at a time.

Not compiled in Unity yet: if the Console shows a red error, paste it to Claude Code.
