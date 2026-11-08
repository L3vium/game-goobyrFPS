# Hitmen — working agreements

Multiplayer Hitman-like on **Unreal Engine 5.7** (`Hitman.uproject`, C++ module `Hitmen`).
This repo is the **code-first base**: systems in C++, Blueprints for values and juice. The team
makes graphics, juice, balancing, LD, PCG, VFX and audio. Design: `docs/GAME.md`.
Conventions (prefixes, folders, BP colour code): `docs/Conventions.md`. History: `docs/CHANGELOG.md`.
Everything (docs, comments, commits) is in English.

## Git
- **Work on `dev`.** Commit and push **every change individually** (`git push origin dev`).
  Update `docs/GAME.md` / `docs/CHANGELOG.md` in the same commit as the change they describe.
- Assets go through Git LFS (`.gitattributes`); `.uasset`/`.umap` are lockable.
- **End of every prompt:** player builds, commit + push them, then release `main` **without
  checking it out** (a checkout under the open editor reloads everything):
  ```
  git update-ref refs/heads/main $(git commit-tree "dev^{tree}" -p main -p dev -m "Release x.y.z")
  git push origin main
  ```
  main always gets its own merge commit (never fast-forward).

## Build
Engine: `C:\Program Files\Epic Games\UE_5.7`.
- Editor build (editor closed):
  `"C:\Program Files\Epic Games\UE_5.7\Engine\Build\BatchFiles\Build.bat" HitmenEditor Win64 Development -Project="%CD%\Hitman.uproject" -WaitMutex`
- Player builds (`RunUAT BuildCookRun`): Shipping → `Builds/Windows`, Development (cheats) →
  `Builds/Windows Dev`. Both are versioned through LFS.
- New `UCLASS`/`UPROPERTY`/`UFUNCTION` need the editor closed and a full build (Live Coding
  cannot add reflected types). Claude may save, close and relaunch the editor for this; batch C++
  changes into milestones.

## Editor access
The Unreal MCP server (`mcp__unreal-engine__*`, McpAutomationBridge plugin) drives the open
editor. Editor Python (`system_control` → `execute_python`) builds assets and levels;
level builders live in `Tools/Editor/` so maps can be regenerated. Check dirty packages before
closing the editor.

## Code rules
- C++ for systems, Blueprints for values, meshes, VFX, SFX and `Juice_On…` events.
- Features are `UActorComponent`s; families use inheritance (character, weapon, ability,
  interactable).
- Objects communicate through delegates bound at BeginPlay; UI has **no hard references** —
  widgets bind to `UHitmenUIEventSubsystem`, PlayerState and GameState delegates.
- No Tick unless continuous; spawn weapons/attachments once and show/hide them.
- All input bindings live in `AHitmenPlayerController`; the pawn only exposes functions.
- Server-authoritative (listen server); movement predicted in `UHitmenCharacterMovementComponent`.
- Tunables: `UPROPERTY(EditDefaultsOnly, Category="Hitmen|…")`, `Project Settings → Hitmen`,
  or data assets. No magic numbers.
- Default keys are AZERTY (ZQSD, `A` manipulation, `E` move ability); crouch and sprint are held.

## Lessons carried from the Unity prototype
- Hearing is graded, occluded, capped to the 1–2 nearest responders; never map-wide.
- Parkour probes handle thin geometry (sills, 30 cm walls) and grab in the air.
- After any AI change, test the attack chain: a seen, armed trespasser is shot within seconds.
- Spectating is a full feature (free fly, follow, multi-view), not a fallback.
- Screenshot every changed screen before calling a UI task done.
