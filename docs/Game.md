# Goobyr FPS — design

Multiplayer (1-8) stealth-assassination FPS with a wobbly low-poly look. Every NPC is hostile.
A target is drawn from the map's pool of 15; kill it, take the proof from its body, carry it to an
active extraction point. PvP is on: killing the carrier drops the proof.

## Modes
- **Free-for-all** — everyone for themselves.
- **Teams** — 2 to 4 teams of any size; whoever extracts makes their team win.
- **Co-op** — one team against the AI; lost if everyone is eliminated.

Death = eliminated, then spectate (teammates, or anyone in FFA). The match ends on an extraction,
when every player is eliminated, or at the time limit (draw).

## Maps (three zones each: town, lair, mansion/tower)
- **Pinecrest Hollow** — forest, logging town, fortified commune + military outpost, lakeside lodge.
- **Mirage Bay** — Gulf coast city, souk, smugglers' docks, luxury hotel tower.
- **Elmridge** — American suburb, downtown strip, biker junkyard, hilltop mansion.

_Sections below are filled in as systems land._
