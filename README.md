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

## ECS for the XCGF.Data models

XCGF.Arena is an Entity Component System that instantiates the archetypes described by
[XCGF.Data](https://github.com/mastertnt/XCGF.Data).

| Namespace | Content |
| --- | --- |
| `XCGF.Arena.Ecs` | `World` (entities with generations, one component of each type per entity, queries), `ISystem`, `Simulation`. |
| `XCGF.Arena.Data` | Archetype model, `XcgfDataLoader` (XML with `xi:include`), `ArchetypeLibrary` (derivation, default values, constraints, validation). |
| `XCGF.Arena.Components` | Components built from the archetypes. |
| `XCGF.Arena.Spawning` | `ArchetypeFactory`: spawns entities and units from their archetypes. |
| `XCGF.Arena.Systems` | `KinematicSystem`, `TaskSystem` and the `MoveTo` behaviour. |

How the models map to the ECS:

- An **entity archetype** becomes an entity with `EntityIdentity`, `Kinematic` (position, orientation, speed),
  `KinematicLimits` (stall speed, max speed, ceiling), `EntityParameters`, `Equipments`, `TaskCapabilities` and
  `AssignedTasks`.
- Each **equipment**, the kinematic included, becomes a child entity with `Equipment` (owner, name, definition) and
  `EquipmentData` (technological and initialization data, typed after their HLA data type).
- A **unit archetype** becomes an entity with `Unit`; its members are spawned with it and carry `UnitMembership`.
- A **task** assigned with `TaskSystem.Assign` is a `TaskInstance` that goes from Pending to Running to Ended through
  its activation and exit triggers. `Immediate`, `AtTime`, `OnTaskEnd`, `OnPositionReached` and `OnEvent` are
  evaluated by the system; `OnCondition` and `CustomTrigger` need a handler registered on the system.

```csharp
ArchetypeLibrary library = XcgfDataLoader.Load("external/XCGF.Data/examples/library/library.xml");
World world = new World();
ArchetypeFactory factory = new ArchetypeFactory(world, library);
TaskSystem tasks = new TaskSystem(library);
Simulation simulation = new Simulation(world).AddSystem(tasks).AddSystem(new KinematicSystem());

Entity fighter = factory.SpawnEntity("Fighter", new Dictionary<string, DataValue>
{
    ["Kinematic.Position"] = new GeoPosition(48.7, 2.1, 3000).ToValue(),
});
tasks.Assign(world, fighter, "MoveTo", new Dictionary<string, DataValue>
{
    ["Destination"] = RecordValue.Of(("Latitude", new NumberValue(48.9)), ("Longitude", new NumberValue(2.4))),
});
simulation.Run(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1));
```
