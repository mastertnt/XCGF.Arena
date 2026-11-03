# XCGF.Arena

## Structure

```
XCGF.Arena.slnx
src/XCGF.Arena/                 Class library
src/XCGF.Arena.Console/         Console host that runs the library
tests/XCGF.Arena.Tests/         xUnit tests
external/XCGF.Data/             XCGF.Data submodule (schema and example library)
```

Clone with the submodule:

```bash
git clone --recurse-submodules https://github.com/mastertnt/XCGF.Arena.git
# or, in an existing clone
git submodule update --init
```

## Build, test and run

```bash
dotnet build
dotnet test
dotnet run --project src/XCGF.Arena.Console
```

## XCGF.Data components on Friflo.Engine.ECS

XCGF.Arena implements, as components and systems of [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS),
the archetypes described by [XCGF.Data](https://github.com/mastertnt/XCGF.Data). Geodesic computations use
[GeographicLib.NET](https://github.com/noelex/GeographicLib.NET) on the WGS84 ellipsoid.

| Namespace | Content |
| --- | --- |
| `XCGF.Arena.Data` | Archetype model, `XcgfDataLoader` (XML with `xi:include`), `ArchetypeLibrary` (derivation, default values, constraints, validation). |
| `XCGF.Arena.Components` | Friflo components built from the archetypes, hierarchy helpers. |
| `XCGF.Arena.Spawning` | `ArchetypeFactory`: spawns entities and units into an `EntityStore`. |
| `XCGF.Arena.Systems` | `KinematicSystem`, `TaskSystem`, `SensorSystem` (Friflo `QuerySystem`s), the `MoveTo` behaviour and the `RadarModel`. |
| `XCGF.Arena` | `ArenaSimulation`: store, factory, systems and simulation clock wired together. |

How the models map to the components:

- An **entity archetype** becomes an entity with `EntityIdentity`, `Kinematic` (position, orientation, speed),
  `KinematicLimits` (stall speed, max speed, ceiling), `EntityParameters`, `TaskCapabilities` and `AssignedTasks`.
- Each **equipment**, the kinematic included, becomes a child entity with `Equipment` (name, definition, type) and
  `EquipmentData` (technological and initialization data, typed after their HLA data type).
- A **unit archetype** becomes an entity with `Unit`; its members are spawned with it as child entities carrying
  `UnitMembership`.
- A **sensor** is an equipment whose `type` has a registered `ISensorModel` (`RadarModel` for `Radar`: `MaxRange`,
  `FieldOfView` around the carrier heading, nothing in `Off` mode). At each step `SensorSystem` writes what it sees
  into the `SensorDetections` component of the sensor equipment. Other sensor types plug in with
  `SensorSystem.RegisterModel`.
- A **task** assigned with `TaskSystem.Assign` is a `TaskInstance` that goes from Pending to Running to Ended through
  its activation and exit triggers. `Immediate`, `AtTime`, `OnTaskEnd`, `OnPositionReached` and `OnEvent` are
  evaluated by the system; `OnCondition` and `CustomTrigger` need a handler registered on the system.

```csharp
ArchetypeLibrary library = XcgfDataLoader.Load("external/XCGF.Data/examples/library/library.xml");
ArenaSimulation arena = new ArenaSimulation(library);

Entity fighter = arena.Factory.SpawnEntity("Fighter", new Dictionary<string, DataValue>
{
    ["Kinematic.Position"] = new GeoPosition(48.7, 2.1, 3000).ToValue(),
});
arena.Tasks.Assign(fighter, "MoveTo", new Dictionary<string, DataValue>
{
    ["Destination"] = RecordValue.Of(("Latitude", new NumberValue(48.9)), ("Longitude", new NumberValue(2.4))),
});
arena.Run(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1));
Entity radar = fighter.FindEquipment("Radar");
```
