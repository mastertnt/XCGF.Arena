using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Spawning;

namespace XCGF.Arena.Tests;

public class ArchetypeFactoryTests
{
    private readonly EntityStore mStore = new EntityStore();
    private readonly ArchetypeFactory mFactory;

    public ArchetypeFactoryTests()
    {
        mFactory = new ArchetypeFactory(mStore, TestLibrary.Example);
    }

    [Fact]
    public void SpawnEntity_Fighter_BuildsIdentityKinematicAndLimits()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Assert.Equal(new EntityIdentity("Fighter", "1.2.71.1.0.0.0", EntityEnvironment.Air, "Aircraft/Fighter"), lFighter.GetComponent<EntityIdentity>());
        Assert.Equal(250, lFighter.GetComponent<Kinematic>().Speed);
        Assert.Equal(new KinematicLimits(60, 590, 15000), lFighter.GetComponent<KinematicLimits>());
    }

    [Fact]
    public void SpawnEntity_Fighter_CreatesOneChildEntityPerEquipment()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Assert.Equal(
            new[] { "Kinematic", "Signature", "Radar", "RWR", "IRST", "Missile_1", "Missile_2" },
            lFighter.GetEquipments().Select(pEquipment => pEquipment.GetComponent<Equipment>().Name));
        Assert.Equal(8, mStore.Count);

        Entity lRadar = lFighter.FindEquipment("Radar");
        Assert.Equal(lFighter, lRadar.Parent);
        Assert.Equal(new Equipment("Radar", "FighterRadar", "Radar", false), lRadar.GetComponent<Equipment>());
        EquipmentData lRadarData = lRadar.GetComponent<EquipmentData>();
        Assert.Equal(new NumberValue(150000), lRadarData.Technological.Get("MaxRange"));
        Assert.Equal(new EnumValue("Search", 1), lRadarData.Initialization.Get("Mode"));

        EquipmentData lMissileData = lFighter.FindEquipment("Missile_2").GetComponent<EquipmentData>();
        Assert.Equal(new EnumValue("HLAtrue", 1), lMissileData.Initialization.Get("Armed"));
        Assert.NotSame(lFighter.FindEquipment("Missile_1").GetComponent<EquipmentData>().Initialization, lMissileData.Initialization);
        Assert.True(lFighter.FindEquipment("Missile_3").IsNull);

        Entity lKinematicEquipment = lFighter.FindEquipment("Kinematic");
        Assert.True(lKinematicEquipment.GetComponent<Equipment>().IsKinematic);
        EquipmentData lKinematicData = lKinematicEquipment.GetComponent<EquipmentData>();
        Assert.Equal(new[] { "Position", "Orientation" }, lKinematicData.Initialization.Items.Keys.Take(2));
        Assert.Equal(new NumberValue(250), lKinematicData.Initialization.Get("InitialSpeed"));
        Assert.Null(lKinematicData.Initialization.Get("Position"));
    }

    [Fact]
    public void SpawnEntity_Fighter_HasTheTasksOfItsArchetype()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        TaskCapabilities lCapabilities = lFighter.GetComponent<TaskCapabilities>();
        Assert.Equal(new[] { "FireOnTarget", "FollowFlightPlan", "MoveTo" }, lCapabilities.ByTask.Keys.Order());
        Assert.All(lCapabilities.ByTask.Values, pCapability => Assert.Equal(lFighter, pCapability.Provider));
        Assert.Empty(lFighter.GetComponent<AssignedTasks>().Items);
    }

    [Fact]
    public void SpawnEntity_WithOverrides_AppliesScenarioValues()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue>
        {
            ["Kinematic.Position"] = new GeoPosition(48.7, 2.1, 3000).ToValue(),
            ["Kinematic.Orientation"] = new Orientation(90, 0, 0).ToValue(),
            ["Kinematic.InitialSpeed"] = new NumberValue(300),
            ["Radar.Mode"] = new EnumValue("Off", 0),
        };

        Entity lFighter = mFactory.SpawnEntity("Fighter", lOverrides);

        Assert.Equal(new Kinematic(new GeoPosition(48.7, 2.1, 3000), new Orientation(90, 0, 0), 300), lFighter.GetComponent<Kinematic>());
        Entity lRadar = lFighter.FindEquipment("Radar");
        Assert.Equal(new EnumValue("Off", 0), lRadar.GetComponent<EquipmentData>().Initialization.Get("Mode"));
    }

    [Theory]
    [InlineData("Kinematic.InitialSpeed", 700, "must be <= 590 (MaxSpeed)")]
    [InlineData("Kinematic.InitialSpeed", 50, "must be >= 60 (StallSpeed)")]
    [InlineData("Radar.MinRange", 200000, "must be <= 150000 (MaxRange)")]
    [InlineData("Radar.MaxRange", 0, "must be > 0")]
    public void SpawnEntity_WithValueBreakingConstraint_ThrowsAndCreatesNothing(string pKey, double pValue, string pExpected)
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { [pKey] = new NumberValue(pValue) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Fighter", lOverrides));

        Assert.Contains(pExpected, lError.Message);
        Assert.Equal(0, mStore.Count);
    }

    [Fact]
    public void SpawnEntity_WithValueOutsideListOfValues_Throws()
    {
        ArchetypeLibrary lLibrary = TestLibrary.FromXml("""
            <XcgfData xmlns="urn:xcgf:data:1.0">
              <DataTypes>
                <BasicData name="F64" size="64" interpretation="Double" endian="Big" encoding="IEEE 754"/>
                <BasicData name="I32" size="32" interpretation="Integer" endian="Big" encoding="32-bit two's complement signed integer"/>
                <EnumeratedData name="ModeEnum" representation="I32">
                  <Enumerator name="Off" value="0"/><Enumerator name="Search" value="1"/><Enumerator name="Jamming" value="2"/>
                </EnumeratedData>
                <FixedRecordData name="GeoPosition">
                  <Field name="Latitude" dataType="F64"/><Field name="Longitude" dataType="F64"/><Field name="Altitude" dataType="F64"/>
                </FixedRecordData>
                <FixedRecordData name="Orientation">
                  <Field name="Heading" dataType="F64"/><Field name="Pitch" dataType="F64"/><Field name="Roll" dataType="F64"/>
                </FixedRecordData>
              </DataTypes>
              <EquipmentLibrary>
                <KinematicDefinition name="Kinematic">
                  <InitializationData>
                    <Parameter name="Position" dataType="GeoPosition"/><Parameter name="Orientation" dataType="Orientation"/>
                  </InitializationData>
                </KinematicDefinition>
                <EquipmentDefinition name="Jammer" type="Radar">
                  <InitializationData>
                    <Parameter name="Mode" dataType="ModeEnum">
                      <DefaultValue>Off</DefaultValue>
                      <Constraints><ListOfValues><Value>Off</Value><Value>Search</Value></ListOfValues></Constraints>
                    </Parameter>
                  </InitializationData>
                </EquipmentDefinition>
              </EquipmentLibrary>
              <Entities>
                <Entity name="Pod" disType="1.2.1.1.0.0.0">
                  <Kinematic definition="Kinematic"/>
                  <Equipments><Equipment name="Radar" definition="Jammer"/></Equipments>
                </Entity>
              </Entities>
            </XcgfData>
            """);
        Assert.Empty(lLibrary.Validate());
        ArchetypeFactory lFactory = new ArchetypeFactory(mStore, lLibrary);
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { ["Radar.Mode"] = new EnumValue("Jamming", 2) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => lFactory.SpawnEntity("Pod", lOverrides));

        Assert.Contains("is not one of Off, Search", lError.Message);
        Assert.Equal(0, mStore.Count);
    }

    [Fact]
    public void SpawnEntity_RaisingTechnologicalBound_AcceptsHigherInitialValue()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue>
        {
            ["Kinematic.MaxSpeed"] = new NumberValue(800),
            ["Kinematic.InitialSpeed"] = new NumberValue(700),
        };

        Entity lFighter = mFactory.SpawnEntity("Fighter", lOverrides);

        Assert.Equal(700, lFighter.GetComponent<Kinematic>().Speed);
        Assert.Equal(800, lFighter.GetComponent<KinematicLimits>().MaxSpeed);
    }

    [Fact]
    public void SpawnEntity_WithUnknownOverride_Throws()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { ["Radar.Power"] = new NumberValue(1) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Fighter", lOverrides));

        Assert.Contains("'Radar.Power'", lError.Message);
        Assert.Equal(0, mStore.Count);
    }

    [Fact]
    public void SpawnEntity_UnknownArchetype_Throws()
    {
        Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Tank"));
    }

    [Fact]
    public void SpawnUnit_FighterPatrol_CreatesMembersWithRoles()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");

        Assert.Equal(new Unit("FighterPatrol", "Air"), lPatrol.GetComponent<Unit>());
        Entity[] lMembers = lPatrol.GetMembers().ToArray();
        Assert.Equal(new[] { "Leader", "Wingman" }, lMembers.Select(pMember => pMember.GetComponent<UnitMembership>().Role));
        Assert.All(lMembers, pMember =>
        {
            Assert.Equal("Fighter", pMember.GetComponent<EntityIdentity>().Archetype);
            Assert.Equal(lPatrol, pMember.Parent);
        });
        Assert.Equal(17, mStore.Count);

        TaskCapability lChangeFormation = lPatrol.GetComponent<TaskCapabilities>().ByTask["ChangeFormation"];
        Assert.Equal("Line", lChangeFormation.Defaults.Single().DefaultValue!.Text);
    }

    [Fact]
    public void SpawnUnit_NestedUnits_SpawnsRecursively()
    {
        ArchetypeLibrary lLibrary = TestLibrary.Example;
        ArchetypeLibrary lExtended = new ArchetypeLibrary();
        foreach (DataTypeDefinition lType in lLibrary.DataTypes.Values)
        {
            lExtended.Add(lType);
        }

        foreach (EquipmentDefinition lDefinition in lLibrary.EquipmentDefinitions.Values)
        {
            lExtended.Add(lDefinition);
        }

        foreach (TaskDefinition lTask in lLibrary.Tasks.Values)
        {
            lExtended.Add(lTask);
        }

        lExtended.Add(lLibrary.GetEntity("Fighter"));
        lExtended.Add(lLibrary.GetUnit("FighterPatrol"));
        lExtended.Add(new UnitArchetype("Squadron", null, null, new[] { new UnitMember("FighterPatrol", true, 2, "Patrol") }, Array.Empty<TaskRef>()));
        Assert.Empty(lExtended.Validate());
        ArchetypeFactory lFactory = new ArchetypeFactory(mStore, lExtended);

        Entity lSquadron = lFactory.SpawnUnit("Squadron");

        Assert.Equal(2, lSquadron.GetMembers().Count());
        Assert.Equal(4, mStore.Query<EntityIdentity>().Count);
        Assert.Equal(3, mStore.Query<Unit>().Count);
    }

    [Fact]
    public void DeleteTree_Unit_DeletesMembersAndEquipments()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");
        Entity lOther = mFactory.SpawnEntity("Fighter");

        lPatrol.DeleteTree();

        Assert.Equal(8, mStore.Count);
        Assert.False(lOther.IsNull);
        Assert.All(mStore.Query<Equipment>().Entities.ToArray(), pEquipment => Assert.Equal(lOther, pEquipment.Parent));
    }

}
