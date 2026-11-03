# Checklist 0.7.0

Status: [x] done and checked in the editor or tests · [~] done, not playtested · [ ] not done

## Request: distractions, turrets, cameras, throwables
- [~] More throwables (150+ per map, placed by guard posts and patrol stops)
- [~] Distractions you switch on: pedestal fan, garden hose, hackable office PC; the nearest NPC goes to look
- [~] Sentry guns that target trespassers, warn with a beep and a laser, then fire
- [~] A thick yellow cable from each gun to its hidden control PC, also drawn in Instinct (Tab)
- [~] Hacking the PC turns the guns on the guards (credited to the hacker)
- [~] Security cameras alert guards (8+ per map), drawn in Instinct

## Request: 0.7.0 direction
- [x] The Hitman-like hero is gone: original hero Rook (copper quiff, bomber jacket, scarf; male and female)
- [~] Whole new cast: guards per biome, bodyguard, staff, guests, VIPs, with faces (blink, scared, angry, X eyes)
- [~] Goofy touch: wobble, goofier ragdolls, gibberish barks, surprised faces
- [x] Same height as the NPCs (BodyMetrics: every eye at 1.60 m, headshots follow the head)
- [~] Real grass: GPU blades with wind and trampling on every natural ground, mown lawns, feathered edges
- [~] Better ground and look: HDR grade per biome, terrain detail and anti-tiling, rock and glass shaders, reflection probes
- [x] Hands on the guns: authored poses per weapon, sights on screen centre (FP pose tests)
- [~] New punch: jab/cross on contact with hit-stop; one-hit takedown of unaware enemies
- [x] Enemies attack again: armed trespassers are shot within 3-4 s (AI scenarios 6/6, four runs)
- [~] Patrols everywhere (about half the guards move, an interior loop on every floor of each key building)
- [~] Civilians scream, flee to exits, cower, and witnesses run to report to guards
- [~] Hitman-style AI: suspicion, confrontation, zone memory, searches that open hiding spots
- [~] Map layout matches the features (turrets, cameras, distractions and entries on patrol routes): validator OK on all 3 maps
- [ ] Map judge at 18/20: the last scores were 14/12/16 before rounds 3 and 4; not re-judged (stopped to ship faster)
- [x] Version 0.7.0, player build, release to main

## Known gaps
- Snow glints still sparkle inside tree shadows.
- Map edges are benched, but from the air they can still read as walls.
- The first-person thumb on the pistol and the launcher fingers could use polish.
