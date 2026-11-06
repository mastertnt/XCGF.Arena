# XCGF.Arena

## Structure

```
XCGF.Arena.slnx
src/XCGF.Arena/                 Class library
src/XCGF.Arena.Console/         Console host that runs the library
src/XCGF.Arena.Map/             Desktop host (Avalonia + Mapsui): map with play, pause and stop, blueprint graph viewer
tests/XCGF.Arena.Tests/         xUnit tests
data/library/                   Arena library: the scenario archetypes (fighters, tasks, patrol) on top of the
                                XCGF.Data standard library
```

XCGF.Data (schemas and standard library) comes from the [XCGF.Data](https://www.nuget.org/packages/XCGF.Data) NuGet
package, 0.1.2. The build copies its schemas and standard library to `XCGF.Data/` and the Arena library to
`ArenaLibrary/` in the output directory; `ArenaLibrary/library.xml` includes the standard library through
`../XCGF.Data/library/...`.

## Build, test and run

```bash
dotnet build
dotnet test
dotnet run --project src/XCGF.Arena.Console -- --duration 700
```

The console host plays the scripted two-fighter scenario: BLUE1 and RED1 start 150 km apart at 8000 m and fly
towards each other's starting point; their radars, radar warning receivers and IRSTs detect each other, they engage when within missile range, cease
fire 30 seconds later, and lose contact once they have crossed. Options: `--library <path>`, `--duration <s>`,
`--step <s>`, `--realtime`, `--dis <host[:port]>` (DIS 7 Entity State PDUs over UDP, port 3000 by default, one per
entity per second) and `--exercise <id>`.

```bash
dotnet run --project src/XCGF.Arena.Console -- --realtime --duration 700 --dis 127.0.0.1:3000
```

### Map

```bash
dotnet run --project src/XCGF.Arena.Map [-- --scenario scripted|blueprint]
```

The map host shows the same scenario on an OpenStreetMap background (tiles downloaded at run time), with the entities
coloured by side and pointing to their heading. Play starts or resumes the simulation, Pause freezes it, Stop sets the
scenario up again at its start; the speed sets the simulated seconds per wall-clock second (x1, x10, x60). It runs on
Windows, Linux and macOS. The playback logic is `ArenaPlayer` in the library, so other hosts can reuse it.

The scenario list (or `--scenario`) chooses between the scripted two-fighter scenario and
`BlueprintFightersScenario`, where both fighters are driven by blueprint graphs only, around an engagement zone drawn
on the map:

- BLUE1 (`blue-intercept`): radar off, `MoveTo` its patrol point at 280 m/s, `HoldPosition` 120 s, then `MoveTo` back
  to its start at Cruise; `OnZoneEnter` (once) turns its radar to Search;
- RED1 (`red-patrol`): radar off, `FollowCourse` on a looping racetrack whose western leg crosses the zone;
  each `OnZoneEnter` turns its radar to Search for 60 s, then off again.

When the scenario uses blueprints, a panel beside the map shows the graph of the entity chosen in its list, drawn with
[NodeEditorAvalonia](https://github.com/wieslawsoltes/NodeEditor): event, action, latent and pure nodes coloured by
kind, pins and links coloured by type (exec in white), and constants on the unlinked inputs. While the scenario runs, a
halo flashes on each node that just ran and a badge marks the nodes where a fiber waits (with the time left on a
delay); the log of the blueprint runtime is listed below. Graphs without an editor `Layout` are laid out automatically:
one band per event, its exec chain from left to right. The wheel zooms, the middle button pans, "Cadrer" fits the graph.

## XCGF.Data components on Friflo.Engine.ECS

XCGF.Arena implements, as components and systems of [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS),
the archetypes described by [XCGF.Data](https://github.com/mastertnt/XCGF.Data). Geodesic computations use
[GeographicLib.NET](https://github.com/noelex/GeographicLib.NET) on the WGS84 ellipsoid.

| Namespace | Content |
| --- | --- |
| `XCGF.Arena.Data` | Archetype model, `XcgfDataLoader` (XML with `xi:include`), `ArchetypeLibrary` (derivation, default values, constraints, validation). |
| `XCGF.Arena.Components` | Friflo components built from the archetypes, hierarchy helpers. |
| `XCGF.Arena.Spawning` | `ArchetypeFactory`: spawns entities and units into an `EntityStore`. |
| `XCGF.Arena.Systems` | `KinematicSystem`, `TaskSystem`, `SensorSystem` (Friflo `QuerySystem`s), the behaviours of the standard movement tasks (`MoveTo`, `FollowCourse`, `HoldPosition`) and the sensor models. |
| `XCGF.Arena.Dis` | `DisSystem`: entity states for DIS (geocentric location, velocity and orientation, entity type from `disType`), sent to an `IDisPublisher`; `UdpDisPublisher` encodes them as DIS 7 Entity State PDUs with [OpenDisNet](https://github.com/RejectKid/OpenDisNet). |
| `XCGF.Arena.Scenarios` | Scenarios (`IScenario`, scripted `TwoFightersScenario`, blueprint-driven `BlueprintFightersScenario`) and `ArenaJournal`, which logs task transitions and radar contacts. |
| `XCGF.Arena.Blueprints` | Blueprint graphs (Event / Action / Pure nodes) run as fibers by `BlueprintRuntime` and `BlueprintSystem`; `CircularZone` and `ZoneDetectionSystem`; standard nodes in `XCGF.Arena.Blueprints.Nodes`. |
| `XCGF.Arena` | `ArenaSimulation`: store, factory, systems and simulation clock wired together. `ArenaRunner`: loads a library, plays a scenario, writes the journal. |

How the models map to the components:

- An **entity archetype** becomes an entity with `EntityIdentity`, `Kinematic` (position, orientation, speed),
  `KinematicLimits` (stall speed, max speed, ceiling), `EntityParameters`, `TaskCapabilities` and `AssignedTasks`.
- Each **equipment**, the kinematic included, becomes a child entity with `Equipment` (name, definition, type) and
  `EquipmentData` (technological and initialization data, typed after their HLA data type).
- A **unit archetype** becomes an entity with `Unit`; its members are spawned with it as child entities carrying
  `UnitMembership`.
- A **sensor** is an equipment whose `type` has a registered `ISensorModel`. The built-in models follow the sensor
  definitions of the XCGF.Data standard library (`docs/models/sensor.md` and its specialisations). `SensorModel`
  (Sensor) gives the shared coverage: `Operational`, slant range in [`MinRange`, `MaxRange`], target altitude in
  [`MinAltitude`, `MaxAltitude`], `DetectionArcs` relative to the carrier heading (clockwise, with elevation bounds;
  all around when absent) and the domains searched by `Capabilities`. `RadarModel` (Radar) is silent in `Off` mode;
  `EsmModel` (ESM) hears the emitting radars of a target that share one of its `FrequencyBands`; `InfraredModel`
  (Infrared) and `VisualModel` (Visual) detect inside their coverage. A sensor scans every `ScanInterval` and keeps its
  `MaxContacts` nearest contacts in the `SensorDetections` component of its equipment. The signal physics (radar
  equation, ESM sensitivity, infrared and visual signature ranges) is left to more detailed models plugged in with
  `SensorSystem.RegisterModel`.
- A **task** assigned with `TaskSystem.Assign` is a `TaskInstance` that goes from Pending to Running to Ended through
  its activation and exit triggers. `Immediate`, `AtTime`, `OnTaskEnd`, `OnPositionReached` and `OnEvent` are
  evaluated by the system; `OnCondition` and `CustomTrigger` need a handler registered on the system.
  The movement tasks come from the XCGF.Data standard library: an entity inherits the tasks its kinematic references
  (`Kinematic`, then `FixedWingKinematic`), with their parameters bounded by the kinematic's data (a `MoveTo` speed
  within `StallSpeed` and `MaxSpeed`). `MoveTo`, `FollowCourse` and `HoldPosition` have behaviours: speed from the
  `Speed` parameter or else the `PerformanceTable` row of the `Throttle`, altitude captured within `ClimbRate`, and
  loiter circling for a kinematic that cannot stop. `SetAltitude`, `Orbit`, `ReturnToBase`, `TakeOff` and `Land` can
  be assigned but have no behaviour yet.

```csharp
ArchetypeLibrary library = XcgfDataLoader.Load("ArenaLibrary/library.xml");
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

## Blueprints

A blueprint is a graph of nodes attached to an entity, in the manner of visual scripting:

- an **Event** node subscribes when the graph is attached, and starts a **fiber** at each firing (`OnStart`,
  `OnZoneEnter`, `OnTaskEnded`). Its `RetriggerPolicy` tells what a firing does while a fiber of the event runs: `Parallel`, `Ignore`,
  `Restart` or `Once`;
- an **Action** node runs in a fiber, through exec links. It changes the world (`SetSpeed`, `SetHeading`,
  `SetEquipmentParameter`, `AssignTask`, `RaiseTaskEvent`, `LogMessage`), waits (`Delay`, latent) or chooses an exec
  output (`Branch`). `MoveTo`, `FollowCourse` and `HoldPosition` assign the movement task of the same name with
  their parameters as inputs, then wait for its end and follow the exec output of its outcome (`TaskNode.SuccessPin`
  or `FailurePin`), its outcome on the data output;
- a **Pure** node computes values on demand for the data inputs, without changing the world (`Self`, `GetHeading`,
  `GetSpeed`, `GetAltitude`, `GetPosition`, `MakePosition`, `OffsetPosition`, `Add`, `Subtract`, `AddHeading`,
  `GreaterThan`). Positions travel on `Position` pins as `GeoPosition`, courses on `Course` pins as lists of
  `CourseWaypoint`.

A fiber is a Friflo entity (`FiberState`, `FiberWait`, `FiberReady`): the graph is interpreted, its program counter
is the id of the current node. `ArenaSimulation` runs `ZoneDetectionSystem` then `BlueprintSystem` after the sensors,
so the graphs react to the events of the step. New nodes implement `IEventNode`, `IActionNode` or `IPureNode`.

```csharp
Entity zone = arena.Store.CreateEntity(new CircularZone(new GeoPosition(48.9, 2.1, 0), 5000));

BlueprintGraph graph = new BlueprintGraph("radar-patrol");
int onStart = graph.Add(new OnStart());
graph.Exec(onStart, graph.Add(new SetSpeed(), 200.0));

int onEnter = graph.Add(new OnZoneEnter(), zone);
int radarOff = graph.Add(new SetEquipmentParameter(arena.Library), "Radar", "Mode", "Off");
int wait = graph.Add(new Delay(), 30.0);
int radarOn = graph.Add(new SetEquipmentParameter(arena.Library), "Radar", "Mode", "Search");
graph.Exec(onEnter, radarOff);
graph.Exec(radarOff, wait);
graph.Exec(wait, radarOn);

arena.Blueprints.Attach(fighter, arena.Blueprints.RegisterGraph(graph));
```
