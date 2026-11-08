# Arrow Puzzle: final audit

Date: 2026-10-30. Unity 6000.6.7f1, Android.
Backup: git commit `ce4546f` (Assets, Packages, ProjectSettings, docs). `Library/`, `Temp/` and `test.apk` are not in it.

**How this was checked**
- Every script was read in full.
- Compile: Unity's own Roslyn with the project's real references, as Editor and as Player. Baseline: 0 errors, 3 warnings.
- Levels: the game's real `LevelGen.cs`, `ShapeLevels.cs` and `Levels.cs` were run outside Unity.
- Current build: `test.apk` was inspected with `aapt`.
- **Not run:** nothing has been played on a device. Flow findings in section D come from reading the code.

---

## A. Code

### A1. Script usage (28 files)

| File | Status | How it is reached |
|---|---|---|
| `GameApp.cs` | USED | `[RuntimeInitializeOnLoadMethod]` boot |
| `AppScreen.cs` | USED | Base class of every screen |
| `ScreenSplash`, `ScreenHome`, `ScreenLevels`, `ScreenGame`, `ScreenSettings` | USED | **Reflection only** (`GameApp.CreateScreens`) plus name strings |
| `PopupPause`, `PopupWin`, `PopupLose`, `PopupHints` | USED | **Reflection only** plus name strings |
| `Ads.cs` | USED | `GameApp.Awake` and screens |
| `AppLinks.cs` | USED | `ScreenSettings` |
| `BannerPlaceholder.cs` (with `BannerDebug`) | USED | `ScreenHome`, `ScreenGame`, `ScreenSettings` |
| `BoardView.cs`, `BoardZoom.cs` | USED | `ScreenGame` |
| `ArrowGraphic.cs` (with `Poly`) | USED | Everywhere |
| `PathGraphic.cs` | USED | `ScreenLevels` |
| `Icons.cs`, `Ui.cs`, `Theme.cs` | USED | Everywhere |
| `Levels.cs`, `LevelGen.cs`, `ShapeLevels.cs`, `ModeInfo.cs` | USED | Level loading |
| `Save.cs`, `Sfx.cs` | USED | Everywhere |
| **`Silhouettes.cs`** | **UNUSED** | No references anywhere |

**Stripping trap:** because the screens are only found by reflection, Medium stripping would delete them unless the game assembly is preserved. The game would then show the "foundation ready" text instead of the splash. A `link.xml` that covers only Google Mobile Ads and Firebase would break the game.

### A2. Unused code

| Item | Where | Decision |
|---|---|---|
| `Silhouettes` class | `Silhouettes.cs` | Remove |
| `SetNudge`, `SetGrey`, `NudgeRoutine` and fields ("Home pager" leftovers) | `BoardView.cs:745-788` | Remove |
| `BoardView.Remaining` | `BoardView.cs:58` | Remove |
| `Theme.SilhouetteLocked` | `Theme.cs:36` | Remove |
| `Icon.Grid` and its case | `Icons.cs` | Remove |
| Empty section comment | `BoardView.cs:662` | Remove |
| Doc comment on the wrong class (`PressScale`) | `Ui.cs:269` | Move |
| Unused usings | See WHAT WAS CHANGED | Remove |
| `Ads.ReportsFailure`, one-argument `ShowRewarded` | `Ads.cs` | Report only: Ads.cs is off limits |
| `GameApp.Show<T>`, `Open<T>`, `Close<T>` | `GameApp.cs` | Keep: small public API named in `AppScreen`'s docs |
| `ShowFoundationCheck` smoke test | `GameApp.cs:316` | Keep: the only visible sign if screens ever fail to load |

No commented-out code blocks and no TODO or FIXME comments were found. Test and debug code is limited to `Ads.TestMode`, `Ads.ShowBannerDebug` and `Save.UnlockAllModes` (see RELEASE CHECKLIST).

### A3. Logging

Totals: 8 `Debug.Log` calls, 6 `Debug.LogWarning`, 4 `Debug.LogError`, and 68 `Ads.Log()` call sites.

| Calls | Verdict |
|---|---|
| All `LogError` (4) | Useful, keep |
| `[Ads]` log calls (68 sites, 2 warnings, 1 in `PopupWin`) | Useful per your rule, keep. They also run in release, about one line per banner refresh. |
| `[ArrowPuzzle]` missing-screen warnings (2) | Useful, keep |
| `[AppLinks]` (6) | Mostly Editor-only or failure paths, keep |
| `ShapeLevels` self check (1) | Already Editor-only |
| **`[ArrowPuzzle] Solve time` (`ScreenGame.cs:437`)** | **Noise.** Builds a long string on every win. Wrap it. |

### A4. Per-frame cost

Measured against each `Update`, `LateUpdate` and coroutine:

| Item | Finding | Severity |
|---|---|---|
| `OnGUI`, LINQ, `GameObject.Find`, `GetComponent` in loops | None. `FindFirstObjectByType` runs only at boot. | OK |
| `ScreenLevels.Sequence` | Builds 2 new strings every frame for 4.4 s (`"LEVEL n COMPLETE!"`) | Low: fix |
| `ScreenLevels.PulseAt` | Rebuilds a 49-point ring mesh every frame on the whole map canvas, so all 50 nodes re-batch. Clouds and the pin also move each frame. No allocations, but real CPU on low-end phones. | Medium: decision |
| `BoardView.Load` | Creates a tile and a dot per cell. The largest boards are 30x36 and 37x33, so **up to about 2,400 GameObjects per load**, all destroyed and rebuilt on every Restart or Next. | Medium: decision |
| `ScreenLevels.BuildNodes` | Destroys and rebuilds 50 nodes (about 1,000 GameObjects) on every open, and again when the unlock sequence ends. | Medium: decision |
| `BoardView.SparksRoutine`, `RingRoutine` | 10 GameObjects created per wrong move. Rare. | Low: leave |
| `Sfx.Build` | Allocates a `float[4]` per audio sample for the win sound: about 30,000 small arrays at startup. | Low: fix |
| `Sfx.Update` | `PlayerPrefs` read per frame once music exists. There is no music today, so it exits at once. | Note |
| `Shimmer`, `HomeTicker`, `BannerPlaceholder`, `TimerLoop` | No allocations; each runs only while its screen is active. | OK |
| Theme, cloud and flash sprites | Generated at startup, once. All screens are built eagerly in `Awake`. | Low: test startup on a cheap phone |
| Coroutines that never stop | None. `TimerLoop` and the `PopupLose` pulse stop in `OnHide`; ad retries are bounded. | OK |

### A5. Memory leaks

| Item | Finding |
|---|---|
| `Ads.BannerHeightChanged` | Balanced in `BannerPlaceholder`, `ScreenHome`, `ScreenGame`, `ScreenSettings` |
| `AppStateEventNotifier` | One subscription for the app's life, guarded by `initStarted`. Not a leak. |
| Button listeners | Added once at build. `ScreenLevels` nodes are destroyed with their listeners. |
| `Board.*` events in `ScreenGame` | Same lifetime as the screen. Fine. |
| Code-made textures and sprites | All cached for the app's life, none rebuilt repeatedly. No leak. |
| Screens and popups | Built once and reused. The exceptions are the rebuilds noted in A4. |
| `AppLinks` JNI objects | Return values of `intent.Call<AndroidJavaObject>` are not disposed. Tiny, and only on Share. |

No growing leak was found.

### A6. Crash and hang risks

| Risk | Finding | Severity |
|---|---|---|
| **Rewarded request while another is busy** | `Ads.ShowRewarded` silently ignores the call, so neither callback runs. Path: Hints "Watch", then "No thanks", then lose and tap "Watch ad" within 6 s. `PopupLose` then locks all its buttons and back **forever**. | **High, rare: fix in the callers** |
| Pause opened during the last arrow's exit or the lose collapse | Win or Lose opens over Pause. After Next or a revive, **the Pause popup is still there**. No crash. | Medium: fix |
| Fingers stuck after the app is paused mid-pinch | `BoardZoom` keeps stale finger IDs and can stay in pinch mode, so **board taps stop working** until the level is left. | Medium: fix |
| Ad callbacks after a screen change | Guarded by request IDs and `IsOpen`. | OK |
| `Time.timeScale` stuck at 0 | Every path goes through `PopupPause.OnHide`; `StartLevel` also resets it. | OK |
| Back key | Popups first, then the screen. All popups handle it. | OK |
| Pause or resume during an ad | Handled by watchdogs, `JustReturned` and `IsFullScreenAdShowing`. | OK |
| Missing or corrupt save | PlayerPrefs falls back to defaults and values are clamped on use. Hints can display negative only if edited by hand. | OK |
| Division by zero | All divisors are guarded. | OK |
| Level loading index errors | Parser plus verified data (section D15). `BoardView` trusts the data, and a 0-direction arrow would hang `Check`; none exists in the shipped files. | OK |
| Coroutine on an inactive object | Checked `ReviveLife`, `PlayCollapse`, `ResetZoom`, `GrantHints`. All safe. | OK |

---

## B. Assets and project size

### B7. Largest files under Assets

Only three groups reach an Android build: `ArrowPuzzle/Resources` (fonts and levels), the `GoogleMobileAdsSettings` asset, and the Android plugin and DLLs.

| KB | File | Status |
|---:|---|---|
| 1324 | `Plugins/iOS/.../simulator/unity-plugin-library` | iOS only, not in the Android build. Protected. |
| 649 | `Plugins/iOS/.../ios-arm64/unity-plugin-library` | iOS only. Protected. |
| 250 | `Plugins/Android/googlemobileads-unity.aar` | USED |
| 234 | `GoogleMobileAds/Editor/Resources/Ump/ConsentForm.png` | Editor only |
| 202 | `GoogleMobileAds/Editor/Resources/PlaceholderAds/AdImages/1024x768.png` | Editor only |
| 178 | `.../AdImages/768x1024.png` | Editor only |
| 136 | `ArrowPuzzle/Scripts/.code-review-graph/graph.db` | Tool data. Ignored by Unity (dot folder), not in the build. |
| 125 | `GoogleMobileAds/GoogleMobileAds.Android.dll` | USED |
| 105 | `ArrowPuzzle/Resources/Levels/mosaic.txt` | USED. Protected. |
| 105 | `GoogleMobileAds/GoogleMobileAds.iOS.dll` | iOS only |
| 104 | `ArrowPuzzle/Resources/Levels/shapes.txt` | USED. Protected. |
| 66 | `.../AdImages/480x320.png` | Editor only |
| 63 | `.../AdImages/320x480.png` | Editor only |
| 57 | `ArrowPuzzle/Scripts/ScreenLevels.cs` | USED |
| 55 | `GoogleMobileAds/GoogleMobileAds.Unity.dll` | Editor placeholder client |
| 54 | `GoogleMobileAds/CHANGELOG.md` | Not in build |
| 51 | `ArrowPuzzle/Scripts/Ads.cs` | USED |
| 49 | `ArrowPuzzle/Resources/Fonts/Fredoka_500Medium.ttf` | USED |
| 49 | `ArrowPuzzle/Resources/Fonts/Fredoka_600SemiBold.ttf` | USED |
| 49 | `ArrowPuzzle/Resources/Fonts/Fredoka_700Bold.ttf` | USED |
| 48 | `GoogleMobileAds/GoogleMobileAds.dll` | USED |
| 46 | `.../AdImages/300x250.png` | Editor only |
| 38 | `GoogleMobileAds/GoogleMobileAds.Common.dll` | USED |
| 33 | `ArrowPuzzle/Scripts/BoardView.cs` | USED |
| 33 | `.../AdImages/AdInspectorHome.png` | Editor only |
| 32 | `.../AdImages/728x90.png` | Editor only |
| 30 | `ArrowPuzzle/Scripts/ScreenGame.cs` | USED |
| 28 | `ArrowPuzzle/Scripts/PopupWin.cs` | USED |
| 27 | `.../AdImages/320x100.png` | Editor only |
| 26 | `ArrowPuzzle/Scripts/ScreenHome.cs` | USED |

- **Sample or demo folders:** none. No TextMesh Pro, no Google samples.
- **Unused scenes:** `Assets/test.unity` and `Assets/_Recovery/` (3 autosaves). Each holds only the default Main Camera and Directional Light. They are not in the build.
- **Total Assets:** about 4.9 MB, mostly iOS and Editor files. **Assets are not why the APK is 27 MB.**

**What the 27 MB `test.apk` is made of (compressed):**

| MB | Part | Can it shrink? |
|---:|---|---|
| 8.3 | `libunity.so` | Little. Engine stripping is already on. |
| 6.8 | `libil2cpp.so` | Yes: managed stripping |
| 6.1 | `classes.dex`, `classes2.dex` (Google Ads SDK) | Yes: R8 minify (decision) |
| 1.5 | `global-metadata.dat` | Yes: managed stripping |
| 1.0 | `data.unity3d` (fonts, levels, scene) | No |
| 0.8 | `unity default resources` | Slightly: removing Vulkan |
| 0.5 | `resources.arsc` | Yes: R8 resource shrinking |

### B8. Audio

**There are no audio files.** `Resources/Audio` does not exist. All sounds are generated in `Sfx.Build`, and music does not play because `Audio/music` is missing. Nothing to compress. If you add files later: Vorbis, mono for SFX, Streaming for music.

### B9. Textures

**No game textures in Assets.** All sprites are generated in code, without mipmaps except the heart, which needs them for small sizes. There is no Read/Write or atlas setting to change. The only imported images are Editor-only Google placeholders, which never reach the build and are protected.

### B10. Packages

| Package | Needed? |
|---|---|
| `com.unity.ugui` | YES. Pulls in ui, imgui, audio, physics, physics2d. |
| `com.unity.external-dependency-manager` | YES (AdMob). Pulls in uielements. |
| `com.unity.ide.rider`, `com.unity.ide.visualstudio` | Editor only, not in the build |
| androidjni, jsonserialize, unitywebrequest, imageconversion | YES (Google Mobile Ads DLLs reference them) |
| audio, ui, imgui, uielements, physics, physics2d, physicscore2d, animation | YES (required by the packages above) |
| ai, cloth, vehicles, wind, terrain, terrainphysics, particlesystem, tilemap, video, xr, director, screencapture, unityanalytics, adaptiveperformance, assetbundle, unitywebrequestassetbundle, unitywebrequestaudio, unitywebrequesttexture, unitywebrequestwww | Not used by the game or by anything it depends on: **remove** |
| accessibility, tetgen, timelinefoundation, umbra, vectorgraphics | Not used by game code, but the engine may use them internally: **keep** (decision) |

**Physics, Physics2D and Animation cannot be removed**: uGUI and UIElements declare them as dependencies. **Expected build saving from removing modules is about 0 MB**, because Strip Engine Code already drops unused engine code. The benefit is a cleaner project.

### B11. Build contents

| Item | Value | Note |
|---|---|---|
| Scenes in Build Settings | **Empty** | The build uses whichever scene is open. Fix: list `Assets/Main.unity`. |
| Resources | 3 fonts, `OFL-Fredoka.txt`, 2 level files, AdMob settings | All needed. The font licence should ship with the fonts. |
| StreamingAssets | None | |
| Always Included Shaders | 7, Unity defaults | Includes UI/Default. Leave. |
| Preloaded Assets | None | |
| `Main.unity` | Main Camera (**clear = Skybox**) and a Directional Light | Skybox is drawn every frame behind opaque UI. Scene edits need your approval. |

---

## C. Build and release settings

| Setting | Current | Recommendation | Action |
|---|---|---|---|
| Scripting backend | Default = **IL2CPP** (confirmed: `libil2cpp.so` in the APK) | IL2CPP | None |
| Target architectures | **ARM64 only** (`arm64-v8a` in the APK) | See note below | Report |
| Managed stripping | Default (Minimal for IL2CPP) | Medium, with `link.xml` | Fix |
| `link.xml` | GMA's own, covering only Android, iOS and UMP client DLLs | Add the game assembly and all GMA DLLs | Fix |
| API compatibility | .NET Standard 2.1 (`apiCompatibilityLevel: 6`) | Fine | None |
| Strip Engine Code | On | On | None |
| Optimize Mesh Data | Off (`StripUnusedMeshComponents: 0`) | No meshes in the game, no effect | None |
| Build App Bundle | Last build was an **APK** | AAB, required by Play | You, at release |
| Compression | Not readable from text settings | LZ4HC | Check in Build Profiles |
| Texture compression | Default (ASTC) | No textures imported | None |
| Graphics API | **Auto (Vulkan + GLES3)** | GLES3 only | Fix |
| Quality levels | 6, Android uses Medium | No build-size effect in a UI-only game | Leave |
| Frame rate | `Application.targetFrameRate = 60` in code | OK | None |
| Orientation | **Auto Rotation, all four directions allowed** (APK `screenOrientation=0xd`) | Portrait only | Fix |
| Minimum API | 26 | OK | Report |
| Target API | Automatic = highest installed; **36 in the APK** | Meets the current Play rule of API 36 | Report |
| Package name | **`com.DefaultCompany.ArrowPuzzle`** | Your own. Can never change after publishing. | You |
| Company name | **DefaultCompany** | Your own | You |
| Version | 1.0, code 1 | OK for a first release | You |
| Icons | **None** (Unity default icon) | Add an adaptive icon set | You |
| Unity splash | Off | OK | None |
| Keystore | **Not set** | Create and back it up | You |
| Development build | Off in the APK (no debuggable flag) | Off | Check in Build Profiles |
| R8 minify | Off | Largest remaining saving | Decision |

**Architecture note:** Play requires 64-bit, not ARMv7. ARM64-only is accepted and keeps the app smaller. Adding ARMv7 only adds reach on old 32-bit phones, at a cost of several MB of download on an APK. With an AAB each phone downloads only its own libraries.

**Play target rule:** new apps and updates need API 36 from August 31, 2026 ([Google Play help](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en-419), [Android developers](https://developer.android.com/google/play/requirements/target-sdk)). Check the Play Console before you upload in case this has changed.

---

## D. Gameplay and logic

### D14. Flows (from reading the code)

| Flow | Result |
|---|---|
| First launch | OK. Defaults: 3 hints, level 1, sound and vibration on. |
| Splash to Home | OK. Waits at most 3 s extra for the app open ad, then always continues. |
| Home to each mode | OK. Card opens the level map, Play starts the level. Locked cards shake and show a toast. |
| Mode unlock rules | Correct: Shapes after Classic 15, Mosaic after Shapes 10. **Currently bypassed by `UnlockAllModes = true`.** |
| Win, next level, interstitial | OK. No ad after the very first completed level. **Interstitial after every other win** (gap 0): see decision list. |
| Home bar at 100 % | **Cosmetic bug:** after finishing level 50 the bar shows 49/50 and the text "Level 50 of 50". |
| Lose, rewarded continue | OK except the deadlock in A6. Revive gives 1 life, once per loss. |
| Hints and counts | OK. A hint is only spent when one is really shown. The reward adds 3 and saves at once. |
| Hints: back or "No thanks" while a video loads | **Risk:** the ad can still appear up to 6 s later, over the game. The reward is still paid. Decision. |
| Stars saving | OK. Keeps the best score. Saved before the Win popup. |
| Zoom and pan | OK, except the stuck-finger case in A6. |
| Very large boards | **Risk:** on 30x36 boards a cell is about 10 design px on screen, close to the tap reach. Zoom is there, but test how it feels. |
| Pause and resume | OK, except Pause staying open in A6. |
| Settings | OK. Opens from Home only and returns to Home. |
| Minimise and resume (App Open) | OK. 10 s away minimum, 60 s gap, skipped after other ads and links. |
| No internet | OK. All ad paths time out and the game continues. Rewarded buttons show "Video not ready". |
| Ad load failure | OK. Retries are bounded. |
| Empty real ad IDs | Safe, but **no ads at all** and no error on screen if `TestMode` is false before the IDs are pasted. |
| Save after app kill | OK. Each change calls `PlayerPrefs.Save`. Lives in a level are not saved, by design. |
| Android back with predictive back on | `androidPredictiveBackSupport: 1`. Unverified: test on Android 13 or newer. |

### D15. Levels: all 150 checked by running the real code

| Check | Result |
|---|---|
| Game's own self check | **`ShapeLevels: 100 levels OK`** |
| Independent solver using `BoardView.Check` rules | **All 150 solvable** |
| Cells inside grid and mask, no overlaps, valid direction and colour | All pass |
| Classic generator fallback board used | 0 of 50 |
| Classic determinism | Same level every time |
| Classic 1-cell arrows | 48 across all levels; `BoardView` handles them |
| Out-of-range requests | No exceptions. Classic clamps, others return null, and `ScreenGame` clamps first. |
| Largest boards | Classic 10x14, Shapes 30x36 (118 arrows), Mosaic 37x33 (112 arrows) |

---

## SAFE TO FIX NOW

1. Delete `Silhouettes.cs`, `Assets/test.unity`, `Assets/_Recovery/` (with their `.meta` files).
2. Remove the unused code in A2 and unused usings.
3. Wrap the "Solve time" log.
4. `ScreenLevels`: stop building chip strings every frame.
5. `Sfx`: stop allocating an array per audio sample.
6. Replace the deprecated `FindFirstObjectByType` (3 warnings).
7. Rewarded deadlock: check `Ads.IsRewardedBusy` in `PopupLose` and `PopupHints`. Ads.cs is not touched.
8. Close Pause when Win or Lose opens.
9. `BoardZoom`: clear fingers on app pause.
10. Settings: Portrait only, GLES3 only, Medium stripping with a `link.xml`, `Main.unity` in Build Settings, remove the unused modules listed in B10.

## NEEDS MY DECISION

1. **UMP consent is missing.** No consent code exists, though the SDK includes it. Google requires a consent message for users in the EEA, the UK and Switzerland. Strongly recommended before release; it changes the ad flow, so it was not touched.
2. **R8 minify.** Probably the biggest single saving (estimated 2-4 MB), but needs testing with ads.
3. **Interstitial frequency.** `InterstitialMinGapSeconds = 0` means an ad after almost every win. A gap of 60 s or more is kinder to players and to retention.
4. **Pool board tiles and dots** (up to about 2,400 objects per load), or draw them as one mesh. Would remove the hitch on Restart and Next.
5. **Reuse level map nodes** instead of rebuilding about 1,000 objects, and give the pulse its own canvas.
6. **`Main.unity`:** set the camera to a solid colour and delete the Directional Light. Scene edits are off limits under `CLAUDE.md`.
7. **Late rewarded ad** after "No thanks" or back. Fixing it means cancelling in Ads.cs.
8. **Home progress bar** stops at 49/50 after level 50.
9. **Remove `Ads.ReportsFailure` and the one-argument `ShowRewarded`** (unused, in Ads.cs).
10. **Modules kept because I can't prove they are safe to remove:** accessibility, tetgen, timelinefoundation, umbra, vectorgraphics. Saving about 0 MB.
11. **`[Ads]` logs in release:** keep them for now, wrap them after launch.
12. **Remove extra quality levels:** no measurable gain.

## RELEASE CHECKLIST (values you must change)

| Item | Where | Now | Change to |
|---|---|---|---|
| Test mode | `Ads.cs:41` `Ads.TestMode` | **`true`** | `false` |
| Dev unlock | `Save.cs:46` `Save.UnlockAllModes` | **`true`** | `false` |
| Real ad unit IDs | `Ads.cs:22-25` `AdIds.Real*` | **All empty** | Your 4 IDs |
| AdMob App ID | `GoogleMobileAdsSettings.asset` | **Google's test ID** `ca-app-pub-3940256099942544~3347511713` | Your App ID |
| Privacy policy | `AppLinks.cs:16` | **`https://example.com/arrow-puzzle-privacy`** | Your URL |
| UMP consent | Not present | **Missing** | Add it (decision 1) |
| Banner debug | `Ads.ShowBannerDebug` | `false` | OK as is |
| Interstitial gap | `Ads.InterstitialMinGapSeconds` | `0` | Your choice (decision 3) |
| Package name | Player Settings | **`com.DefaultCompany.ArrowPuzzle`** | Your own, before first upload |
| Company name | Player Settings | **DefaultCompany** | Your own |
| App icon | Player Settings | **None** | Your icon set |
| Keystore | Player Settings | **None** | Your keystore, backed up |
| Output format | Build Profiles | APK | **AAB** |
| Development Build, Script Debugging | Build Profiles | Off in the last APK | Confirm off |

Hard-coded test strings: only the 4 Google test unit IDs in `AdIds`, used only while `TestMode` is true.

---

## WHAT WAS CHANGED

All of this is **uncommitted** on top of baseline commit `ce4546f`, so `git diff` shows every line.

### Code

| File | Change |
|---|---|
| `Silhouettes.cs` + `.meta` | Deleted (unused) |
| `BoardView.cs` | Removed `SetNudge`, `SetGrey`, `NudgeRoutine`, their fields, `Remaining` and an empty section comment |
| `BoardZoom.cs` | Added `OnApplicationPause`: clears held fingers so taps can't stay blocked |
| `GameApp.cs` | `FindFirstObjectByType` to `FindAnyObjectByType` (3 warnings gone; these calls only test for existence) |
| `Icons.cs` | Removed `Icon.Grid` and the unused `using UnityEngine.UI` |
| `PopupLose.cs`, `PopupHints.cs` | If a rewarded request is already busy, show "Video not ready" instead of waiting forever |
| `ScreenGame.cs` | Closes Pause before opening Win or Lose; "Solve time" log wrapped in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| `ScreenLevels.cs` | Chip labels built once per sequence, not per frame |
| `Sfx.cs` | Win notes moved to one static array |
| `ShapeLevels.cs` | `using System.Text` is now Editor-only |
| `Theme.cs` | Removed `SilhouetteLocked` |
| `Ui.cs` | Moved a misplaced doc comment to `PressScale` |

`CLAUDE.md` normally allows only additive changes to `Theme`, `Ui`, `Icons` and `GameApp`. The removals there follow this audit's explicit instruction to remove unused code.

**Not touched:** `Ads.cs`, level files, save keys, `Assets/GoogleMobileAds`, `Assets/Plugins`, `AdIds`, the AdMob App ID, `TestMode`, `UnlockAllModes`.

### Assets and settings

| File | Change |
|---|---|
| `Assets/test.unity`, `Assets/_Recovery/` (+ `.meta`) | Deleted. Default camera and light only, never in the build. |
| `Assets/ArrowPuzzle/link.xml` (new) | Preserves `Assembly-CSharp` and all Android-relevant Google Mobile Ads DLLs |
| `ProjectSettings.asset` | Portrait only; Android graphics API GLES3 only; Android managed stripping Medium |
| `EditorBuildSettings.asset` | `Assets/Main.unity` added to the build |
| `Packages/manifest.json` | Removed 19 unused modules (list in B10) |

**Differs from the request:**
- **Firebase is not in `link.xml`.** The project has no Firebase files or code.
- **`link.xml` also preserves the game assembly.** You did not list it, but without it Medium stripping would remove the screens.
- **Physics, Physics2D and Animation stay,** because uGUI and UIElements require them.
- **Texture and audio import settings: nothing to change.** The project has no game textures or audio.
- **Quality levels: not changed.** No build-size effect here.

### Verification (Phase 3)

| Check | Result |
|---|---|
| Roslyn, Editor and Player, without the removed modules | **0 errors, 0 warnings** (baseline: 0 errors, 3 warnings) |
| `GoogleMobileAds.Editor` scripts without the removed modules | 0 errors |
| **Unity's own compile** of the code changes (`Library/ScriptAssemblies`, 1:14 PM) | Built, and removed code is gone from the DLL |
| `Ads.cs`, `ScreenSplash`, `ScreenHome`, `ScreenGame`, `ScreenSettings`, `PopupWin`, `PopupLose`, `PopupHints` | All present, all references resolve, all navigation names still match |
| All 150 levels after the changes | Pass; `ShapeLevels: 100 levels OK` |
| Protected paths (`git status`) | No changes |

**Not verified, because Unity stayed open and no build was run:**
- Unity has not yet re-read the settings and package changes. They were written after its last compile.
- No APK has been built with Medium stripping or GLES3-only. **This is the main remaining risk.**
- Nothing has been played on a device.

### Build size estimate

| Change | Project size | Expected build saving |
|---|---:|---:|
| Deleted files (11 files, 0.04 MB) | -0.04 MB | About 0 MB (never in the build) |
| Texture and audio import settings | None | 0 MB (nothing to change) |
| Removed modules | None | About 0 MB (Strip Engine Code is already on) |
| Medium stripping | None | 1.5 to 3 MB (estimate) |
| GLES3 only | None | 0.1 to 0.5 MB (estimate) |
| **Total** | | **About 2 to 3.5 MB off 27 MB (estimate, not measured)** |

Further options, not applied: R8 minify (estimated 2-4 MB) and AAB delivery, where Play sends each phone only what it needs.

---

## STILL TO DO BEFORE RELEASE

1. **Restart Unity and confirm the settings survived.** Unity was open during the edits and may have written its own values back. If `git diff ProjectSettings/ProjectSettings.asset` is missing any of these, set them again in Project Settings > Player (Android):
   - Default Orientation: **Portrait**
   - Auto Graphics API: **off**, list contains only **OpenGLES3**
   - Managed Stripping Level: **Medium**
   - Build Profiles scene list contains `Assets/Main.unity`
2. **Check the Console after the restart.** Package Manager will re-resolve the 19 removed modules. Any error: restore with `git checkout -- Packages/manifest.json`.
3. **Build an APK and test it on a phone** (list below). If the game shows "ARROW PUZZLE foundation ready", stripping removed the screens: check that `link.xml` was picked up.
4. Add UMP consent (decision 1).
5. Change every value in RELEASE CHECKLIST.
6. Choose on the other items in NEEDS MY DECISION.
7. Commit the clean-up when happy. The repo has **no `.gitignore`**, so add a Unity one first: `git add .` would otherwise add the 2.8 GB `Library/` folder and `test.apk`.

### Phone test list

- **Start:** fresh install, splash, Home. Expect the splash, not the "foundation ready" text.
- **Rotation:** turn the phone. The game must stay portrait.
- **Ads with Medium stripping:** banner, interstitial after the second win, rewarded on both popups, app open after 10+ s away.
- **Rewarded fix:** Hints > Watch > No thanks, then lose and tap Watch ad. The buttons must not lock.
- **Pause fix:** tap the last arrow, then Pause at once. After Next Level there must be no Pause popup. Repeat during the lose collapse.
- **Pinch fix:** on a big board pinch with two fingers, press Home while holding, come back, tap an arrow.
- **Back key on Android 13+:** every screen and popup; on Home it should quit.
- **No internet:** airplane mode from launch. The game must play normally with "Video not ready".
- **Large boards:** a late Shapes or Mosaic level: tap accuracy, zoom, Restart hitch.
- **Level map:** win a new level and watch the unlock sequence, chips included.
- **Save:** win, kill the app, reopen: stars and level kept.
- **Graphics:** check that the UI draws correctly on GLES3-only, ideally on an older and a newer phone.
