# Changelog

## 0.1.0 — first playable
- Steam / LAN / solo sessions, lobby with mode, map, teams, proximity voice option.
- Three modes (free-for-all, teams up to 4, co-op), elimination and spectating.
- Three generated maps with three zones each, 15 original targets per map.
- Hostile AI with squads, last known positions, cover, flanking, searches, radio reinforcements,
  map-wide alarm, bodyguards, target routines, patrol cars.
- Systemic props: fire and sprinklers, power cuts, chandeliers, drinks and toilets, ventilation
  gas, distractions, hiding spots, ziplines, jump pads, climbing, tall grass, explosive barrels next
  to every patrol route.
- Weapons and gadgets: hush pistol, pistol, SMG, shotgun, sniper, unique rocket launcher and
  flamethrower, coin, brick, smoke, C4 + remote, lethal and emetic poison, punches and takedowns.
- FPS controller with slides, mantling, climbing; viewmodel; juice; HUD, map screen, end screen.
- GD Tool and map validation; EditMode tests for the match rules.

### Known limits (to iterate)
- Online play was verified with one machine only (solo host in the editor); a real two-account Steam
  session still has to be tried.
- Hero props (cars, furniture, weapons) are procedural boxes; Blender models in
  `Resources/Models` (`W_<Kind>`, `Car`) replace them automatically when added.
- NPC and level balance comes from short playtests; expect tuning in the GD Tool.
