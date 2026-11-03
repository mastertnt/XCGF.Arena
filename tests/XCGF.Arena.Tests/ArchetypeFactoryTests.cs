using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Ecs;
using XCGF.Arena.Spawning;

namespace XCGF.Arena.Tests;

public class ArchetypeFactoryTests
{
    private readonly World mWorld = new World();
    private readonly ArchetypeFactory mFactory;

    public ArchetypeFactoryTests()
    {
        mFactory = new ArchetypeFactory(mWorld, TestLibrary.Example);
    }

    [Fact]
    public void SpawnEntity_Fighter_BuildsIdentityKinematicAndLimits()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Assert.Equal(new EntityIdentity("Fighter", "1.2.71.1.0.0.0", EntityEnvironment.Air, "Aircraft/Fighter"), mWorld.Get<EntityIdentity>(lFighter));
        Assert.Equal(250, mWorld.Get<Kinematic>(lFighter).Speed);
        Assert.Equal(new KinematicLimits(60, 600, 15000), mWorld.Get<KinematicLimits>(lFighter));
    }

    [Fact]
    public void SpawnEntity_Fighter_CreatesOneChildEntityPerEquipment()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        Equipments lEquipments = mWorld.Get<Equipments>(lFighter);
        Assert.Equal(new[] { "Kinematic", "Missile_1", "Missile_2", "Radar" }, lEquipments.ByName.Keys.Order());
        Assert.Equal(5, mWorld.EntityCount);

        Entity lRadar = lEquipments.ByName["Radar"];
        Assert.Equal(new Equipment(lFighter, "Radar", "FighterRadar", "Radar", false), mWorld.Get<Equipment>(lRadar));
        EquipmentData lRadarData = mWorld.Get<EquipmentData>(lRadar);
        Assert.Equal(new NumberValue(150000), lRadarData.Technological.Get("MaxRange"));
        Assert.Equal(new EnumValue("Search", 1), lRadarData.Initialization.Get("Mode"));

        EquipmentData lMissileData = mWorld.Get<EquipmentData>(lEquipments.ByName["Missile_2"]);
        Assert.Equal(new EnumValue("HLAtrue", 1), lMissileData.Initialization.Get("Armed"));
        Assert.NotSame(mWorld.Get<EquipmentData>(lEquipments.ByName["Missile_1"]), lMissileData);

        Equipment lKinematic = mWorld.Get<Equipment>(lEquipments.ByName["Kinematic"]);
        Assert.True(lKinematic.IsKinematic);
        EquipmentData lKinematicData = mWorld.Get<EquipmentData>(lEquipments.ByName["Kinematic"]);
        Assert.Equal(new[] { "Position", "Orientation", "InitialSpeed" }, lKinematicData.Initialization.Items.Keys);
        Assert.Null(lKinematicData.Initialization.Get("Position"));
    }

    [Fact]
    public void SpawnEntity_Fighter_HasTheTasksOfItsArchetype()
    {
        Entity lFighter = mFactory.SpawnEntity("Fighter");

        TaskCapabilities lCapabilities = mWorld.Get<TaskCapabilities>(lFighter);
        Assert.Equal(new[] { "FireOnTarget", "FollowFlightPlan", "MoveTo" }, lCapabilities.ByTask.Keys.Order());
        Assert.All(lCapabilities.ByTask.Values, pCapability => Assert.Equal(lFighter, pCapability.Provider));
        Assert.Empty(mWorld.Get<AssignedTasks>(lFighter).Items);
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

        Assert.Equal(new Kinematic(new GeoPosition(48.7, 2.1, 3000), new Orientation(90, 0, 0), 300), mWorld.Get<Kinematic>(lFighter));
        Entity lRadar = mWorld.Get<Equipments>(lFighter).ByName["Radar"];
        Assert.Equal(new EnumValue("Off", 0), mWorld.Get<EquipmentData>(lRadar).Initialization.Get("Mode"));
    }

    [Theory]
    [InlineData("Kinematic.InitialSpeed", 700, "must be <= 600 (MaxSpeed)")]
    [InlineData("Kinematic.InitialSpeed", 50, "must be >= 60 (StallSpeed)")]
    [InlineData("Radar.FieldOfView", 400, "must be <= 360")]
    [InlineData("Radar.MaxRange", 0, "must be > 0")]
    public void SpawnEntity_WithValueBreakingConstraint_ThrowsAndCreatesNothing(string pKey, double pValue, string pExpected)
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { [pKey] = new NumberValue(pValue) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Fighter", lOverrides));

        Assert.Contains(pExpected, lError.Message);
        Assert.Equal(0, mWorld.EntityCount);
    }

    [Fact]
    public void SpawnEntity_WithValueOutsideListOfValues_Throws()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { ["Radar.Mode"] = new EnumValue("Jamming", 3) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Fighter", lOverrides));

        Assert.Contains("is not one of Off, Search, Track", lError.Message);
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

        Assert.Equal(700, mWorld.Get<Kinematic>(lFighter).Speed);
        Assert.Equal(800, mWorld.Get<KinematicLimits>(lFighter).MaxSpeed);
    }

    [Fact]
    public void SpawnEntity_WithUnknownOverride_Throws()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { ["Radar.Power"] = new NumberValue(1) };

        XcgfDataException lError = Assert.Throws<XcgfDataException>(() => mFactory.SpawnEntity("Fighter", lOverrides));

        Assert.Contains("'Radar.Power'", lError.Message);
        Assert.Equal(0, mWorld.EntityCount);
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

        Unit lUnit = mWorld.Get<Unit>(lPatrol);
        Assert.Equal("FighterPatrol", lUnit.Archetype);
        Assert.Equal(new[] { "Leader", "Wingman" }, lUnit.Members.Select(pMember => pMember.Role));
        Assert.All(lUnit.Members, pMember =>
        {
            Assert.Equal("Fighter", mWorld.Get<EntityIdentity>(pMember.Instance).Archetype);
            Assert.Equal(new UnitMembership(lPatrol, pMember.Role), mWorld.Get<UnitMembership>(pMember.Instance));
        });
        Assert.Equal(11, mWorld.EntityCount);

        TaskCapability lChangeFormation = mWorld.Get<TaskCapabilities>(lPatrol).ByTask["ChangeFormation"];
        Assert.Equal("Line", lChangeFormation.Defaults.Single().DefaultValue);
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
        ArchetypeFactory lFactory = new ArchetypeFactory(mWorld, lExtended);

        Entity lSquadron = lFactory.SpawnUnit("Squadron");

        Assert.Equal(2, mWorld.Get<Unit>(lSquadron).Members.Count);
        Assert.Equal(4, mWorld.Query<EntityIdentity>().Count);
        Assert.Equal(3, mWorld.Query<Unit>().Count);
    }

    [Fact]
    public void Despawn_Unit_DestroysMembersAndEquipments()
    {
        Entity lPatrol = mFactory.SpawnUnit("FighterPatrol");
        Entity lOther = mFactory.SpawnEntity("Fighter");

        Assert.True(mFactory.Despawn(lPatrol));

        Assert.Equal(5, mWorld.EntityCount);
        Assert.True(mWorld.IsAlive(lOther));
        Assert.All(mWorld.Query<Equipment>(), pEquipment => Assert.Equal(lOther, mWorld.Get<Equipment>(pEquipment).Owner));
        Assert.False(mFactory.Despawn(lPatrol));
    }
}
