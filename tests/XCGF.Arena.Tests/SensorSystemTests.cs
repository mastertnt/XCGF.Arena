using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class SensorSystemTests
{
    private static readonly TimeSpan sStep = TimeSpan.FromSeconds(1);

    private readonly ArenaSimulation mSimulation = new ArenaSimulation(TestLibrary.Example);

    [Fact]
    public void Radar_DetectsTargetsInRangeAndFieldOfView()
    {
        Entity lFighter = SpawnFighter(new GeoPosition(0, 0, 5000), 0);
        Entity lAhead = SpawnAt(lFighter, 0, 100000);
        Entity lOffAxis = SpawnAt(lFighter, 50, 50000);
        SpawnAt(lFighter, 0, 200000);
        SpawnAt(lFighter, 90, 50000);
        SpawnAt(lFighter, 180, 10000);

        mSimulation.Step(sStep);

        SensorDetections lDetections = lFighter.FindEquipment("Radar").GetComponent<SensorDetections>();
        Assert.Equal(new[] { lAhead.Id, lOffAxis.Id }, lDetections.Items.Select(pDetection => pDetection.Target.Id).Order());
        Detection lAheadDetection = lDetections.Items.Single(pDetection => pDetection.Target == lAhead);
        Assert.InRange(lAheadDetection.Range, 99000, 101000);
        Assert.InRange(lAheadDetection.RelativeBearing, -1, 1);
        Assert.InRange(lDetections.Items.Single(pDetection => pDetection.Target == lOffAxis).RelativeBearing, 49, 51);
        Assert.Equal(sStep, lDetections.Time);
    }

    [Fact]
    public void Radar_FieldOfViewFollowsTheCarrierHeading()
    {
        Entity lFighter = SpawnFighter(new GeoPosition(0, 0, 5000), 90);
        Entity lEast = SpawnAt(lFighter, 90, 50000);
        SpawnAt(lFighter, 0, 50000);

        mSimulation.Step(sStep);

        Detection lDetection = Assert.Single(lFighter.FindEquipment("Radar").GetComponent<SensorDetections>().Items);
        Assert.Equal(lEast, lDetection.Target);
        Assert.InRange(lDetection.Bearing, 89, 91);
    }

    [Fact]
    public void Radar_InOffMode_DetectsNothing()
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue> { ["Radar.Mode"] = new EnumValue("Off", 0) };
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter", lOverrides);
        SpawnAt(lFighter, 0, 10000);

        mSimulation.Step(sStep);

        Assert.Empty(lFighter.FindEquipment("Radar").GetComponent<SensorDetections>().Items);
    }

    [Fact]
    public void Radar_DetectsTargetOnceItComesInRange()
    {
        Entity lFighter = SpawnFighter(new GeoPosition(0, 0, 5000), 0);
        Entity lTarget = SpawnAt(lFighter, 0, 152000);
        lTarget.GetComponent<Kinematic>().Orientation = new Orientation(180, 0, 0);
        lFighter.GetComponent<Kinematic>().Speed = 300;
        lTarget.GetComponent<Kinematic>().Speed = 300;

        mSimulation.Step(sStep);
        Assert.Empty(lFighter.FindEquipment("Radar").GetComponent<SensorDetections>().Items);

        mSimulation.Run(TimeSpan.FromSeconds(5), sStep);
        Assert.Equal(lTarget, Assert.Single(lFighter.FindEquipment("Radar").GetComponent<SensorDetections>().Items).Target);
    }

    [Fact]
    public void SensorSystem_IgnoresEquipmentsWithoutModel()
    {
        Entity lFighter = SpawnFighter(default, 0);

        mSimulation.Step(sStep);

        Assert.False(lFighter.FindEquipment("Missile_1").HasComponent<SensorDetections>());
        Assert.False(lFighter.FindEquipment("Kinematic").HasComponent<SensorDetections>());
        Assert.True(lFighter.FindEquipment("Radar").HasComponent<SensorDetections>());
    }

    [Fact]
    public void RegisterModel_AddsASensorType()
    {
        Entity lFighter = SpawnFighter(default, 0);
        Entity lTarget = SpawnAt(lFighter, 180, 1000);
        mSimulation.Sensors.RegisterModel("Weapon", new SeeEverything());

        mSimulation.Step(sStep);

        Assert.Equal(lTarget, Assert.Single(lFighter.FindEquipment("Missile_1").GetComponent<SensorDetections>().Items).Target);
    }

    private Entity SpawnFighter(GeoPosition pPosition, double pHeading)
    {
        Dictionary<string, DataValue> lOverrides = new Dictionary<string, DataValue>
        {
            ["Kinematic.Position"] = pPosition.ToValue(),
            ["Kinematic.Orientation"] = new Orientation(pHeading, 0, 0).ToValue(),
        };
        Entity lFighter = mSimulation.Factory.SpawnEntity("Fighter", lOverrides);

        // Hold still unless a test sets a speed: the kinematic would otherwise fly at its stall speed or more.
        lFighter.AddComponent(new KinematicLimits(null, null, null));
        lFighter.GetComponent<Kinematic>().Speed = 0;
        return lFighter;
    }

    private Entity SpawnAt(Entity pFrom, double pBearing, double pDistance)
    {
        GeoPosition lFrom = pFrom.GetComponent<Kinematic>().Position;
        return SpawnFighter(GeoMath.Offset(lFrom, pBearing, pDistance), 0);
    }

    private sealed class SeeEverything : ISensorModel
    {
        public bool IsActive(EquipmentData pData) => true;

        public bool Detects(EquipmentData pData, in SensorGeometry pGeometry) => true;
    }
}
