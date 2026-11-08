# Changelog

All notable changes to Hitmen. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions before 0.1.0 belong to the Unity prototype (see git history before the Unreal port).

## [Unreleased]

### Known gaps (next)
- Lobby / session flow not yet tested with real clients (Null in PIE multi-client, Steam in the
  packaged Development build).
- Blueprint event graphs are mostly empty value carriers: `Juice_On…` stubs, colour-coded comment
  groups and Blueprint Assistant formatting are left for the team.
- Placeholder art only (engine shapes, checker material, mannequins); no SFX/VFX assigned yet.

## [0.1.0] - 2026-11-08

### Added
- Unreal Engine 5.7 project replacing the Unity prototype; Unreal `.gitignore` and LFS
  `.gitattributes` (LFS for `.uasset`/`.umap`, media and build binaries).
- `docs/GAME.md` (design + architecture invariants), `docs/Conventions.md` (prefixes, folders,
  Blueprint colour code), this changelog, `CLAUDE.md`.
- Module `Hitmen` (C++): core types, team rules, interfaces, `Project Settings > Hitmen`, player options, data assets (match rules, maps, weapons, abilities, cosmetics, anim sets, catalog); Steam/voice/trace-channel/surface config.
- Framework: menu/lobby/match/gym game modes, replicated game/player state, input-owning player controller with lobby RPCs and spectator modes, HUD widget manager, UI event hub, Steam session subsystem.
- Characters: shared base, predicted movement (sprint, slide, ledge grab, vault, ladders, ziplines), health, status effects, footsteps per surface with AI noise, cosmetics, anim instance + anim set.
- Inventory (2 slots, throw rules, ammo merge, keycards) and data-driven weapons: hitscan/projectile/melee families, projectiles incl. black hole, flashbang, smoke; weapon pickups.
- Ability system (E move / A manipulation) with blink, double jump, jetpack, dash, hook, transposition, pill, invisibility, armor, C4, stone, remote distraction, sleep dart, charm, flashbang, smoke, lighter, multi-kill, trap, wind mine, poison vial.
- Player character: interaction prompt, fists + stealth kill, body carry, Sightroom (Tab), emotes, charm takeover, proximity voice.
- AI: alert state machine, sight perception, capped/occluded hearing, kill witnesses, reinforcement calls, flanking, NPC class, patrol routes, C++ BT tasks and decorator.
- World: doors/keycards, windows, vents + sealed-room poison, ladders, ziplines, jump pads, teleporters, lockers, tall grass, distractions, cameras + control rooms, turrets, breakers/lights/blackouts, barrels, breakable walls, drinks, mounted gun, tank, retrieval/loot/team-start candidates with spread selection.
- UI: title/join, lobby (carousel, options, drag-and-drop teams, roles, watchers), customization + loadout, HUD (compass, crosshair + alert arcs + headshot cross, prompt, inventory, abilities, minimap), alert indicator, pause + map + debug panel, options + key rebinding, emote wheel, spectator grid, results.
- `HitmenEditor` module: Python-callable builder for BB_NPC and the NPC behavior trees (real editable graphs); camera cone floor footprint; remote Python execution, custom depth stencil, runtime navmesh.
- Generated content (`Tools/Editor/hitmen_build_all.py`): input (AZERTY, rebindable), materials incl. Sightroom post-process, data for 12 weapons / 21 abilities / cosmetics / rules / maps, BP_ children of every class, WBP_ widgets, behavior trees, and maps L_TitleScreen, L_Lobby, L_Empty_01/02, L_Gym, L_LDTest.
- Editor tooling: `ue_remote.py` (Python in the open editor), `ue_shot.sh` (PIE capture), `ue_bake_nav.sh`; baked navmeshes; level builder fixes (rotations, in-place rebuild, title lighting, labels, team starts).
- NPC walk speed per alert state (calm/alert/combat) and NPC damage scaled by the difficulty preset

### Removed
- Unity project, template Horror/Shooter variants, marketplace demo maps.
