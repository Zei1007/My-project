# Zombie Wave Shooter — Vertical Slice

Top-down 2D twin-stick survival shooter, pixel-art dungeon style. Unity **6000.6.1f1**, **URP 17.6**
on the **2D Renderer**, building for **Android**.

Open `Assets/Scenes/Arena.unity` and press Play.

**Controls**
- **Touch (mobile):** one floating thumbstick on the left half of the screen to move. **No aim
  control** — weapons auto-target the nearest zombie. Pause button top-right.
- **Desktop:** WASD / arrows move, mouse aims, weapons auto-fire.
- **Gamepad:** left stick moves, right stick aims (optional — auto-aim works without it).
- Level-up: tap a card, or press `1` / `2` / `3`. `R` restarts after death.

---

## What runs today

| System | Status |
|---|---|
| Twin-stick movement, separate aim, arena clamp | Working |
| Auto-fire at nearest target, per-weapon range | Working |
| Weapon categories: Projectile, Hitscan, Area | Working |
| 5-tier weapon upgrades, 4 weapons authored | Working |
| StatModifier system (`+`, `-`, `×`, `÷`) | Working, math verified |
| Buffs, debuffs, timed expiry, curse-for-reward | Working |
| XP, leveling, 3-choice level-up (queued) | Working |
| Mob tiers: Walker, Runner, Brute, Elite, Mini-Boss | Working |
| Elite abilities: Charge, DeathExplosion, SpeedBurst | Working |
| Wave director, authored waves 1–5 + procedural after | Working |
| Object pooling (zombies, projectiles, orbs, FX) | Working |
| HUD, level-up panel, game over, restart | Working |
| Weapon evolution (data + swap logic) | Implemented, no combos authored yet |
| **Multi-phase Boss (3 phases, 5 attack types)** | **Working** |
| **Animated character rig (procedural)** | **Working** |
| **Mobile controls (floating stick, auto-aim)** | **Working** |
| **Game juice (shake, damage numbers, flash, muzzle)** | **Working** |
| **Pixel-art cast, tileset, props and UI** | **Working** |
| **Tilemapped dungeon arena with props + torches** | **Working** |
| **Sprite atlases (5, uncompressed, Point)** | **Working** |
| **Android build target + player settings** | **Working** |
| Meta-progression / persistence | Not started |

## Layout

```
Assets/
  Scripts/
    Core/     StatTypes, StatSheet, GameEvents, PoolManager, Health,
              EnemyRegistry, GameManager, GameLayers, CameraFollow
    Player/   PlayerDefinition (SO), PlayerController, PlayerExperience
    Weapons/  WeaponDefinition (SO), WeaponInstance, WeaponInventory,
              Projectile, SimpleFx
    Mobs/     ZombieDefinition (SO), ZombieController, BossDefinition (SO),
              BossBrain, TelegraphFx, EnemyProjectile, HazardZone
    Waves/    WaveDefinition (SO), WaveManager
    Buffs/    BuffDefinition (SO), BuffController, UpgradeService
    Pickups/  XPOrb
    Art/      CharacterRig, CharacterAnimator, CameraShake, DamageNumbers, SortingBands
    UI/       UIFactory, GameHUD, BossBarUI, MobileControlsUI, VirtualJoystick
  Editor/     PlaceholderArtGenerator, SdfCanvas, CharacterRigBuilder, GameContentBuilder
  Data/       Weapons, Mobs, Buffs, Waves, Player  (ScriptableObject assets)
  Prefabs/    Player, Mobs, Weapons, Pickups
  Art/
    Characters/   generated chibi body parts, per archetype
    Weapons/      generated weapon sprites
    Placeholder/  FX sprites (disc, ring, muzzle, square)
    Shaders/      SpriteFlash.shader + material
  Scenes/Arena.unity
```

## How the stat layering works

One `StatSheet` per character — player and every mob use the same class. Resolution is fixed:

```
(base + additive − subtractive) × (∏ multiplicative ÷ ∏ divisive)
```

Order of application never changes the result. Every modifier carries a `Source` object, so
`RemoveAllFromSource(buff)` revokes exactly one buff's contribution and nothing else.

**Authoring note:** additive/subtractive are *flat*; multiplicative/divisive are the *percentage*
layer. The design doc's example "−10% move speed" is authored as `Multiplicative 0.9`, not
`Subtractive 0.1` — keeping the two layers separate is what makes the ordering deterministic.
`Divisive` is used as intended by `BUF_Ironclad` (`DamageTaken ÷ 1.5`).

Incoming damage flows through the `DamageTaken` stat (base 1), so damage-reduction buffs and
vulnerability curses compose on the same channel. Armor is a separate buffer that soaks before HP.

## Adding content without code

- **Weapon** — `Create > Zombie Shooter > Weapon Definition`, add 5 `levels`, set category and (for
  Projectile) `projectilePrefab`. Drop it into `UpgradeService.weaponPool` on `GameSystems`.
- **Mob** — `Create > Zombie Shooter > Zombie Definition`. Tier multipliers stack on top of the
  wave scalar, so one asset covers wave 1 and wave 30. Add to a wave's entries or to
  `WaveManager.proceduralPool`.
- **Buff** — `Create > Zombie Shooter > Buff Definition`, add modifiers, pick a duration type. Add
  to `UpgradeService.buffPool`. Set `pairedCurse` for a risk/reward pick.
- **Wave** — `Create > Zombie Shooter > Wave Definition`, then add it to `WaveManager.waves` in
  order. Waves past the end of that list are generated from `proceduralPool` automatically.

Difficulty is the `WaveManager.difficultyCurve` (wave number → stat multiplier) plus
`growthBeyondCurve` per wave past the last key. Current shape: w1 ×1.0, w5 ×1.42, w10 ×2.0,
w20 ×3.4, w50 ×8.8.

## Project settings changed

- Both URP assets now point at `Assets/Settings/2D_Renderer.asset` (was the 3D `UniversalRendererData`).
- GPU Resident Drawer disabled on both URP assets — it is 3D-only and warned every repaint under
  the 2D renderer.
- **Run In Background** enabled, so the game loop keeps running when the Editor is unfocused.
- Layers 8–12 assigned: `Player`, `Enemy`, `Projectile`, `Pickup`, `EnemyProjectile`. Collision
  rules are set in code in `GameLayers.ConfigureCollisionMatrix()`, not the Physics 2D matrix.

## Known gaps / next steps

1. **Only one boss is authored.** `BOSS_Devourer` covers waves 10, 20, 30... A second boss is a
   new `BossDefinition` asset — no code needed unless it wants a brand-new attack type.
2. **Weapon evolutions** — `evolvesInto` / `requiredBuff` and the swap logic are in
   `WeaponInventory.TryEvolve`, but no evolution pairs are authored yet.
3. **Pickup drops** — `ZombieDefinition.pickupDropChance` is authored but nothing spawns timed-buff
   pickups yet; only XP orbs drop. `BUF_Frenzy` exists and its Timed path is verified.
4. **HUD is built in code** (`UIFactory` + `GameHUD`) using legacy uGUI `Text`. Deliberate — no
   prefab wiring to maintain while systems move. Move to TextMeshPro before shipping: legacy Text
   looks soft when scaled up on high-DPI phones.
7. **The art is placeholder.** It matches the reference *style* (flat vector, dark outline, chibi
   proportions, flat shading) but it is code-generated, not hand-drawn. Real artwork drops into the
   same part slots — see Art & Animation below.
5. **Balance is untuned.** Numbers are first-pass placeholders; the slice has not been played with
   real input for tuning.
6. **Support weapons** (turret/drone) — `WeaponCategory.Support` exists, no firing path yet.


---

# Boss Tier

`BOSS_Devourer` spawns on every 10th wave (`WaveManager.bossInterval`) and takes the wave over:
the usual mob trickle is cut to 4 so the fight is about the boss. It enters 4.2 units from the
player so the entrance is on-screen, holds still through its intro banner, then fights.

**Phases** switch on health thresholds, each swapping in its own stat multipliers and attack set:

| Phase | At HP | Speed | Damage | Interval | Attacks |
|---|---|---|---|---|---|
| I - The Approach | 100% | x1.0 | x1.0 | 4.0s | Ground Slam, Summon |
| II - The Hunger | 66% | x1.25 | x1.15 | 3.0s | Slam, Radial Burst, Charge Dash, Summon |
| III - Enraged | 33% | x1.55 | x1.35 | 2.1s | Radial Burst (22), Slam, Hazard Field, Dash |

Every phase change roots the boss briefly, shockwaves, shakes the camera and calls in 3 adds, so
the transition is felt rather than just observed.

**Attack types** (`BossAttackType`), all telegraphed before they land:
- **GroundSlam** - AoE circle on the player's position. Dodge by moving.
- **RadialBurst** - ring of `EnemyProjectile`s. Punishes standing at mid range.
- **SummonAdds** - spawns `addDefinition` mobs around the boss.
- **ChargeDash** - line telegraph, then a 5.5x speed dash along it.
- **HazardField** - drops `HazardZone` pools that tick damage and apply a Conditional debuff while
  the player stands in them.

Phases and attacks are data (`BossDefinition`), so a second boss is a new asset, not new code.
Only a genuinely new *attack type* needs a case in `BossBrain.PerformAttack`.

**Boss UI** - nameplate, health bar and one pip per phase, plus the entrance banner (`BossBarUI`).
It tracks whatever boss `GameEvents.BossSpawned` reports, so it needs no wiring.

# Art & Animation

The art is **generated placeholder art in the reference's flat-vector style**, not the reference
art itself. It is built to be replaced.

- `Tools > Zombie Shooter > Generate Placeholder Art` regenerates every sprite from
  `PlaceholderArtGenerator` (SDF shapes -> PNG). Palettes are one dictionary at the top of that file.
- Each archetype has 4 parts: `CHR_<id>_Head/Torso/Arm/Leg.png`, plus a shared shadow.
- **To drop in real artwork:** overwrite those PNGs, or point the `parts` slots on a
  `ZombieDefinition` at your own sprites. Nothing else changes - the rig, animation and flash all
  work off the slots. Keep 64 PPU, chibi proportions (head nearly torso width), pivot centred.

**Animation is procedural** (`CharacterAnimator`), driven by sine curves against move speed rather
than authored clips - no animation assets, one script covers every archetype, and timing follows
actual velocity:
- walk cycle (legs +/-32 deg counter-swung, arms opposed, torso bob, run lean)
- idle breathing, attack recoil kick, hit squash, death spin + fade
- hit flash via `ZombieShooter/SpriteFlash` shader. A SpriteRenderer tint multiplies and can only
  darken, so whitening a sprite needs the fragment replaced - that is what `_FlashAmount` does.

**Render order** lives in `SortingBands`. Characters are Y-sorted inside a reserved band
(3000-7000) that is kept clear of the floor below and projectiles above.

**Juice**: `CameraShake` (trauma-based, composed into `CameraFollow` rather than fighting it),
`DamageNumbers` (pooled TextMesh, fixed budget), muzzle flashes, impact bursts, knockback.

# Mobile

- `MobileControlsUI` builds a **floating** thumbstick - it appears wherever the thumb lands in the
  left 55% of the screen. Fixed sticks make players look down at the screen.
- **No aim control by default.** Weapons already auto-target the nearest zombie, so an aim stick
  would cost a second thumb and a quarter of the screen for no added capability. Manual aim is
  opt-in: tick `enableAimStick` on `MobileControlsUI`, or plug in a gamepad.
- `InputRouter` re-picks the device every frame (touch > gamepad > keyboard), so `PlayerController`
  never branches on platform.
- `GameHUD.forceTouchControls` shows the touch layout on desktop for checking it in the Editor.
- Pause button (top-right) drives `GameManager.TogglePause`.

Still to do for a real mobile build: switch platform to Android/iOS, safe-area insets for notched
devices, and a pass on draw calls (the parts are unatlased - run them through a SpriteAtlas).


---

# Pixel Art Pipeline

The art is **generated pixel art** in the Soul Knight register - 32 pixels per unit, Point
filtered, uncompressed, hard outlines. It replaced the flat-vector art from the previous pass.

Two menu items regenerate everything, and both are safe to re-run:

- `Tools > Zombie Shooter > Generate Pixel Art (Characters + FX)` - body parts per archetype,
  weapons, FX, soul gems.
- `Tools > Zombie Shooter > Generate Pixel Art (Environment + UI)` - floor tiles, walls, props,
  torch frames, 9-sliced UI frames.

`PixelCanvas` is the drawing surface: no antialiasing anywhere, and a seeded RNG so regenerating
produces byte-identical files instead of churning the repo.

**To drop in real pixel art:** overwrite the PNGs in `Assets/Art/{Characters,Environment,Weapons,FX,UI}`
keeping the file names, then run `Tools > Zombie Shooter > Rebuild All Content`. Author at 32 PPU
with Point filtering. Character parts are Head / Torso / Arm / Leg per archetype.

**Animation is pixel-aware.** `CharacterAnimator.pixelMode` replaces limb *rotation* with
whole-pixel vertical offsets, snapped to the 32px grid, and drops the run lean. Rotating a
9-pixel-wide limb resamples it every frame, which is exactly what makes pixel art look wrong in a
3D engine.

# Arena

`Tools > Zombie Shooter > Rebuild Arena` builds a 32x20-tile dungeon:

- **Floor** is a `Tilemap` with 4 slab variants, weighted 68/12/10/10 toward the plain slab - an
  even mix reads as noise rather than as a floor. 640 cells in a few draw calls; loose
  SpriteRenderers would not batch.
- **Walls** are a two-course border with grass caps, merged into one `CompositeCollider2D`.
- **Props** (trees, bushes, rocks, crates, fences) are scattered with a minimum spacing and a clear
  centre, and carry `YSortSprite` so the player walks behind the ones above them.
- **Torches** cycle three flame frames and breathe a glow sprite. The glow is a sprite, not a
  `Light2D`: the characters render through an unlit flash shader that lights would not touch, and a
  dozen real lights is not a cost a phone should pay for decoration.

The camera is a `PixelPerfectCamera` at 480x270 / 32 PPU (15 x 8.44 units visible) with pixel
snapping on.

# Sprite Atlases

`Tools > Zombie Shooter > Build Sprite Atlases` creates five atlases, one per art folder, and packs
them. Verified live: sprites resolve to `sactx-...-ATLAS_Characters` at runtime.

Pixel art is the awkward case for atlasing, so the settings are deliberate:
- **no rotation, no tight packing** - both break pixel-perfect sampling
- **padding 4** so a neighbour cannot bleed across a sprite edge at a UV boundary
- **Point filter, no mipmaps**
- **RGBA32 uncompressed on Android** - ETC2/ASTC would smear flat pixel colours. These atlases are
  512-1024px, so the memory cost is small and the quality win is the whole point.

# Android

Active build target is **Android**. Configured:

| Setting | Value |
|---|---|
| Package | `com.zber.zombiewaveshooter` |
| Orientation | Landscape only (left/right) |
| Scripting backend | IL2CPP |
| Architecture | ARM64 |
| Min SDK | 26 |
| Graphics | Vulkan, GLES3 fallback |
| Safe area | `renderOutsideSafeArea = false` (respects notches) |

**Two things happened during the switch that you should know about:**

1. Android Build Support was not installed; it was installed via the Unity CLI. One optional child
   module failed to download, but Android, SDK & NDK Tools and OpenJDK all landed and the target
   switched cleanly.
2. **`com.unity.ai.assistant` and `com.unity.ai.inference` were removed.** `ai.inference` pulls in
   App UI 2.1.11, whose `AppUIAndroidProjectFilesModifier.cs` references an `AndroidProjectFilesModifier`
   API that does not exist in 6000.6.1f1. With them installed the project **cannot compile for
   Android at all**. Neither is used by the game. Re-adding them will re-break the Android build
   until Unity ships a compatible App UI.

Still to do before shipping: a keystore, an app icon, and a real device test - none of the above has
run on hardware.

# Orbs and Upgrade UI

- **Soul gems** (`OrbVisual`) bob on the pixel grid, breathe a cyan halo, and flare brighter the
  moment the pickup radius grabs them.
- **Upgrade cards** use a 9-sliced ornate gold frame over an arcane panel, with a rarity aura and a
  gem icon behind each: **gold** = new weapon, **orange** = weapon tier, **blue** = blessing,
  **red** = curse. The panel carries a bloom and a gold rule under the title.
- The HUD bars sit on a matching 9-sliced plate.


---

# Weapons, Loot and Ranged Enemies

## Weapon slots

The player carries **one ranged weapon and one melee weapon** at a time (`WeaponInventory`).
Taking a different ranged weapon swaps it in, but **every weapon's tier is remembered for the run**:
upgrade the Pistol to LV3, swap to the SMG, swap back later and the Pistol returns at LV3.
**Holstered weapons can be upgraded too**: their tier cards read `HOLSTERED  LV n`, and the new tier
is waiting when you equip them. A card to swap back to an owned weapon reads `EQUIP  LV n` and says
what it replaces.

## Aiming

The gun points **where it fires**, not at a cursor - weapons auto-target, so a pointer-driven gun
showed one direction while bullets left in another. Priority: the direction of the shot just fired
(held for 0.35s), then the nearest zombie inside the gun's reach (the next thing it will shoot),
then an opt-in aim stick / gamepad right stick, then the walking direction. Mouse aim is off
(`InputRouter.DesktopMouseAim = false`). The melee sweep never turns the gun. Measured: barrel within
2 degrees of both target and bullet in all four quadrants.

## Range classes

Every weapon has a `WeaponRangeClass`. Reach is enforced by projectile lifetime, so a short-range
shot visibly stops short, and auto-aim will not fire at targets outside reach.

| Weapon | Class | Base reach | Role |
|---|---|---|---|
| Shotgun | Short | 4.5 | pellet cone, point blank |
| Pistol | Medium | 7.0 | steady single shots |
| SMG | Medium | 7.0 | high rate spray (now a visible projectile, was hitscan) |
| Rifle | Long | 10.5 | slow, heavy, pierces 2-4 - the answer to Spitters |
| Machete | Melee | area | the melee slot |

Tier reach grows 6% per level from the class base.

## Weapon scarcity

`UpgradeService` makes weapon cards an event rather than the default: weapon weight 0.35 vs 1.0
for buffs, at most **one** weapon card per roll, and only 55% of rolls may contain one at all.
Measured over 400 rolls: weapon cards are ~7% of all cards and appear in ~22% of level-ups
(previously the majority). All three knobs are on the `UpgradeService` component.

## Ranged enemies

`MOB_Spitter` holds a distance band around its preferred range (5.5), strafes, and lobs acid.
Enemy projectile speed scales by stage through `WaveManager.enemyProjectileSpeedCurve`:

| Wave | 1 | 5 | 10 | 20 | 30 |
|---|---|---|---|---|---|
| Speed x | 0.55 | 0.80 | 1.05 | 1.50 | 1.90 |

capped at 2.2x. The boss's radial burst uses the same curve. Spitters appear from wave 2 and are in
the procedural pool. Any mob can be made ranged via the `Ranged` block on `ZombieDefinition`.

## Loot

Mobs carry a loot table (`ZombieDefinition.loot`); each line rolls independently on death and
drops a `LootPickup` that is pulled in like a soul gem:

- **Health Potion** - restores 35% of max health
- **Shield** - `BUF_Shield`, +40 armor for 20s (armor soaks damage before health)
- **Blessing** - an extra buff: opens a buffs-only choice screen titled BLESSING
- **Frenzy** - `BUF_Frenzy`, double fire rate for 8s
- **Soul Magnet** - pulls every soul gem on the floor to the player

| Source | Drops |
|---|---|
| Regular mobs | ~1-4% potion |
| Elites | potion 45%, shield 30%, frenzy 25%, magnet 20%, blessing 12% (measured: 19 drops / 10 kills) |
| Mini-boss | potion + magnet guaranteed, shield 70%, blessing 60%, frenzy 50% |
| Boss | 2 potions, shield, blessing, magnet, frenzy - every time |

## Max-health buffs

Raising max health now raises current health by the same amount: +25 at 100/100 gives 125/125, and
at 60/125 gives 85/150. Losing max health only clamps. Armor follows the same rule.

## Agent scripts

`AgentScripts/` (outside `Assets/`, so editing it triggers no reimport) holds the entry points
used to drive the Editor over the Unity CLI: `Build.Content`, `Build.Scene`, and the play-mode
checks in `PlayTests`. Run with
`unity command run_script --file AgentScripts/Build.cs --entry Build.Content`.
