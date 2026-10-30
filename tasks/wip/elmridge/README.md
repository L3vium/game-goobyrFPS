# Elmridge 1 km² rebuild — work in progress

The rebuild agent was stopped before it wrote the last partial (`MapSuburb.Between.cs`:
residential streets, park and pond, school, golf / country club, creek bridges, expressway).
These files are kept out of `Assets/` so the project compiles; the shipped Elmridge is still the
first-pass map.

To resume: copy the four `MapSuburb*.cs` into `Assets/_Game/Editor/Maps/`, apply `MapKit.patch`
(`git apply tasks/wip/elmridge/MapKit.patch`), write `PlanBetween()` / `Between()`, then build
`Goobyr/Build/Map Suburb` and run the verification loop of `docs/LevelDesign.md`.
