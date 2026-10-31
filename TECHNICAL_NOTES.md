# Technical Notes - Leikpyar Hunters (Spectral Agents) v1.1

Myanmar Game Jam 2026 submission by Round Table Team.
These notes explain how the game, the website and the server are built, what we decided and why, and what is still open.

## 1. Overview

| Part | Technology | Size |
|---|---|---|
| Game | Unity 6000.0.46f1, URP 17, C#, WebGL | 138 C# files, about 30,000 lines (34 are editor tools) |
| Website + admin | Plain HTML, CSS and JavaScript (no framework, no build step) | about 2,500 lines of JS |
| Server | Python 3 standard library only (`http.server`, `sqlite3`, `hashlib.scrypt`) | about 2,150 lines |
| Hosting | One small server, Caddy (automatic HTTPS) in front of the Python server | |

The game is one WebGL build served at `/play/`. The same server gives the website, the accounts, the leaderboard, cloud saves,
and the protected 3D models of the gallery.

## 2. The three scenes

| Scene | What it is |
|---|---|
| `IntroScene` | Main menu over a looping video, Agent Registry (sign in), leaderboard, credits, language and text size buttons |
| `SampleScene` | The game: the estate, the ghosts, the generator, the NPC and the table |
| `TutorialScene` | The training (a quiet house with a list of tasks) |

Almost all user interface is **built by code at run time** (`UiKit.cs`, `HudStyler.cs`, `OnlineUi.cs`) instead of being saved in scenes.
That keeps the scenes small, makes the same look easy to change in one place, and lets phone and PC share the code.
Many small systems create themselves when a scene starts (a `RuntimeInitializeOnLoadMethod` hook adds the component), so
a scene needs no manual setup for them (for example `DayNightAmbience`, `IntroDesign`, `CreditsScreen`, `IntroOnline`).

## 3. Gameplay systems (Assets/Scripts)

| System | Main scripts | Notes |
|---|---|---|
| Ghosts | `GhostSpawner`, `CatchableGhost`, `MirageGhost`, `ButterflyOrb`, `OrbRunner` | Three classes: Orb (Lv 1), Mirage (Lv 2), Solid (Lv 3) |
| Proton Gun | `GhostGun` | Beam with heat, leash, escape dice per ghost class, beam VFX copies along the beam |
| Generator | `BabyGhostGenerator`, `GhostCapsule` | Takes a leashed ghost, works 9 s, produces an MEP capsule |
| Inventory | `Inventory`, `InvItem`, `InvItemData` | Backpack, held items, Faraday cages dropped in the world |
| Drop Table + NPC | `DropTable`, `NpcWorker`, `PlacedCage` | The GCC worker checks a cage and gives points; the NPC is animated with Playables (no Animator Controller) |
| Upgrades | `UpgradeSystem`, `UpgradePanel`, `Hotbar` | Gun, EMF Scanner, EM-Tethering Machine, MEP (a number per night), and the Injector shop |
| Scanner | `GhostDevice` | EMF Scanner with a battery, on/off, direction arrow and distance |
| Survival | `PlayerHealth`, `HideSystem`, `Torch`, `LowHealthWarning` | Hide in a wardrobe or under a bed, torch, injector heals |
| Time | `WallClockController`, `NightFog`, `DayCycleSounds`, `DayNightAmbience` | One game day is shorter than real time; the night runs from 8 PM to 6 AM |
| Audio | `PlayerSfx`, `Sfx`, `DayCycleSounds` | Male or female player voice by the agent's choice, loops by day and by night |

### 3.1 How ghosts are spawned

- **Orbs (Lv 1):** 3 or 4 Orbs hide at random walkable points of the house (a random point on the baked NavMesh, chosen evenly over the floor area,
  away from the player, the other ghosts and the woman, and reachable on foot). When the player takes one to the generator,
  **one new Orb appears at another place**, so the number in the house stays the same. A pool of Orb objects is reused (an Orb that was taken
  is free again after 14 s, because the generator is still using it).
- **Mirages (Lv 2):** a random 1 or 2 each night. A taken Mirage does not come back. The generator needs the EM upgrade to take one.
- **Solid (Lv 3):** the woman patrols the spawn points and chases the player. She is never removed by catching Orbs or Mirages.
- Everything is reset at the start and at the end of each night (`GhostSpawner.ResetNight`).

### 3.2 MEP per night

The MEP upgrade is a number, not a percent: 3 MEP a night to begin with. 3 to 4 costs 10 points, 4 to 5 costs 20, 5 to 6 costs 30 (`UpgradeSystem.mepStart`,
`mepMax`, `mepFirstCost`, `mepCostStep`). `BabyGhostGenerator` counts the MEP made tonight (reset by `GhostSpawner.NightNumber`)
and refuses a ghost when the count is reached: "You don't have enough MEP left".

### 3.3 Ghost models and animation

- Models come from `.glb` files through the glTFast package. `AnimatedModel` plays one clip with the Playables API (no controller needed).
- The Mirage model came with a Mixamo dance as its only animation, so `IdlePose` replaces it with a procedural idle: the T-pose arms are
  rotated down using the bones' positions (independent of the file's axes) and the body breathes and sways with sine waves.
- Materials: `Shaders/MirageGhost.shader` is a see-through unlit URP shader (depth pre-pass + a fresnel rim), used by the butterfly Orb as glass.

## 4. Localization (English and Myanmar / Burmese)

Unity's TextMeshPro cannot shape Myanmar script (it does not reorder vowels, stack consonants or place marks). We solved it **before run time**:

1. `Tools/burmese/translations.json` holds every English text and its Myanmar text.
2. `Tools/burmese/build_burmese.py` cuts each Myanmar string into syllable clusters, shapes every cluster with **HarfBuzz** and a Unicode Myanmar font,
   and turns each shaped cluster into ONE glyph of a generated font (`BurmeseClusters.ttf`) at a private-use code point.
3. The strings are rewritten as those code points (`BurmeseMap.json`). The game loads them and swaps texts and fonts live.
4. `LocalizeRunner` (runs in every scene) translates every TMP text by looking it up (exact text, or a template with `{0}` parts) and applies the Burmese font and size.
   Texts that must never change (names, version) carry `LocalizeIgnore`.

The website has the same idea in plain JavaScript (`js/i18n.js` with `js/i18n-my.js`), and the admin panel can edit every text in both languages.

## 5. WebGL specifics

- **Size:** the build is about 124 MB (74 MB data and 49 MB wasm; the wasm is about 13 MB compressed). `deploy_pro.py` gives every build a version
  (a hash of the big files), adds `?v=<version>` to their addresses and tells the server to keep them for good, so a returning player downloads **once**.
- **JavaScript plugins** (`Assets/Plugins/WebGL`): full screen on phones, device and battery detection, sharing a screenshot, and loading the creators' photos
  (the browser decodes the picture, so WebP works).
- **Phones:** touch controls (joystick, look pad, buttons), full screen prompt with an animated guide for Android and for iPhone Safari ("Hide Toolbar"),
  and a smaller Burmese text scale.
- **Esc cannot be used in a browser:** Settings opens with the gear or `P`, and has a MAIN MENU button (with a second tap to confirm).
- **Audio:** short effects are decompressed on load; the two day and night loops are Vorbis in memory. An mp3 with odd tags made Unity's FMOD importer fail, so all sound files were re-encoded clean.

## 6. Server (Website/server)

A single Python process, no packages to install.

| Part | How |
|---|---|
| Web and API | `ThreadingHTTPServer`, JSON over HTTPS (Caddy terminates TLS and proxies to `127.0.0.1:8000`) |
| Accounts | codename + password + gender; passwords hashed with **scrypt** and a random salt; a session token (stored hashed) in an HttpOnly cookie, or a Bearer token for the game |
| Scores | the game sends the result of a run; the server checks limits and keeps each agent's best night |
| Cloud save | one save per agent (JSON state + a screenshot) |
| Protected 3D models | the `.glb` files are never served as files; the page asks for a short-lived token, then streams the data |
| Admin | a separate admins table, rate limits, an activity log, CSV export, edits kept in the database and injected into the page at serve time |
| Database | SQLite in WAL mode; small migrations at start (for example the `gender` column) |

### Security measures

- HTTPS only (HTTP redirects), HSTS, a Content Security Policy, `X-Frame-Options`, `nosniff`, a Permissions Policy.
- reCAPTCHA v2 on agent creation, and rate limits on sign in, registration and the other account calls.
- Uploads (photos, sounds) are checked by their first bytes, not by their names, and are size limited. Links accept only `http` and `https`.
- No secrets are in this repository: the database, the secret key and the reCAPTCHA keys are created or set on the server only.
- The "no right click / no dev tools" script is only a deterrent; the real protection of the 3D art is that it is never sent as a file.

## 7. Website (Website/site)

Static pages with no framework: the home page (hero, the night, play, gallery with `<model-viewer>`, high scores, agent registry, credits, rights),
the privacy page, the admin panel (`site/admin`), a service worker for the instant start of the game, and a moving full screen guide.
The page is served through `serve_html`, which puts the texts, pictures and credits edited by the admins into the page, so a new deploy keeps every edit.

## 8. Build and upload

```
Unity: File > Build Profiles > Web > Build to D:\PRO
python Deploy/deploy_pro.py     # the game build -> https://leikpyarhunters.org/play/
python Deploy/deploy_web.py     # the website + server (only changed files are sent)
```
The upload tools use an SSH key (`~/.ssh/lh_deploy`) or the `LH_SSH_PASSWORD` variable. No password is stored in a file.

## 9. Performance notes

- UI is created once, not each frame; texts are looked up in a cache.
- Orb and Mirage objects are pooled and reused: a new Orb is not created, a free one is moved to a new place.
- The Orb's blue point light is not created on phones. Shadows of the torch are off by default.

## 10. Known limits and next steps

- The **Solid ghost's** model can look too dark in some lighting; its materials come from the old model because the new file has no textures.
- Frame rate on low-end phones is not measured yet. The first download (124 MB) is heavy on mobile data; a smaller build (texture compression, fewer audio files) is the next optimisation.
- There is no automated test suite. Gameplay was tested by playing; the server was tested with scripted requests.
- A save made in the middle of a night starts the "MEP made tonight" count again from 0.
- Taking Orbs gives points without a limit per night. A cap may be needed to stop leaderboard farming.

## 11. Opening the code in Unity

This repository holds the **project files that are ours and are text**: the C# code, the shaders, the WebGL plugins, the three scenes
(`IntroScene`, `SampleScene`, `TutorialScene`), our prefabs, materials and small ScriptableObjects, the project settings and the package list.
It does **not** hold the binary art (3D models, textures, sounds, music, video) or third-party packages.

To open it in Unity:

1. Make a new empty folder and copy the whole `Unity/` folder into it (it has `Assets`, `Packages`, `ProjectSettings`).
2. Open that folder with Unity 6000.0.46f1. Unity installs the packages from `Packages/manifest.json` (URP, glTFast, AI Navigation, Input System, UGUI).
3. Open `Assets/Scenes/IntroScene.unity`. The scenes, prefabs and materials load, but every model, texture and sound they point to is **missing** until the art
   is added back (Unity shows empty or pink objects and "Missing" slots). The scripts, the wiring between them and their settings are all there to read.
4. To get a playable build, add the art (ask the team for it under licence, or use your own at the same paths) and the third-party packages listed below.

**What is left out and why**

| Left out | Reason |
|---|---|
| Our 3D models, textures, music, voice (`Assets/ASSET`, `ROOM`, `House`, `NPC`, `Prefab`, `SoundFX`, `SFX`, `UI`, `Resources` art) | The art is protected: all rights reserved |
| Free Quick Effects Vol 1 (Gabriel Aguiar), Cartoon FX Remaster (JMO), Realistic Lightning Pack, Zap VFX URP (Vefects), Joystick Pack, Kino Bloom, Toony Colors Pro, TextMesh Pro essentials | Third-party packages: get them from their own stores |
| `.glb` models from Sketchfab (Ghostly Woman, Magical Orb, Soul Eyes Demon, Animated Butterfly) | Their authors' licences; download from the authors' pages (links on the website) |
| Fonts (Audiowide, DM Sans, Inter, Space Grotesk, Cinzel, Rajdhani, Metal Mania, Special Elite, MM) | Download from their sources |
| `Library`, `Temp`, `Logs`, `UserSettings`, builds | Generated by Unity |

## 12. Credits for the technology

Unity 6 and URP, glTFast, TextMesh Pro, HarfBuzz (`uharfbuzz`) and fontTools for the Myanmar shaping, `<model-viewer>` (Google, Apache-2.0) for the 3D gallery,
Caddy for HTTPS, Python standard library for the server, FFmpeg for sound and video preparation.
