using XCGF.Arena.Components;
using XCGF.Arena.Ecs;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class KinematicSystemTests
{
    private readonly World mWorld = new World();
    private readonly Simulation mSimulation;

    public KinematicSystemTests()
    {
        mSimulation = new Simulation(mWorld).AddSystem(new KinematicSystem());
    }

    [Fact]
    public void Update_MovesAlongHeadingAtSpeed()
    {
        Entity lEntity = Spawn(new Orientation(90, 0, 0), 250, new KinematicLimits(null, null, null));

        mSimulation.Run(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        Kinematic lKinematic = mWorld.Get<Kinematic>(lEntity);
        Assert.Equal(2500, GeoMath.Distance(new GeoPosition(0, 0, 1000), lKinematic.Position), 3);
        Assert.Equal(0, lKinematic.Position.Latitude, 6);
        Assert.True(lKinematic.Position.Longitude > 0);
        Assert.Equal(1000, lKinematic.Position.Altitude);
    }

    [Fact]
    public void Update_ClampsSpeedToLimits()
    {
        KinematicLimits lLimits = new KinematicLimits(60, 600, null);
        Entity lTooSlow = Spawn(default, 10, lLimits);
        Entity lTooFast = Spawn(default, 900, lLimits);

        mSimulation.Step(TimeSpan.FromSeconds(1));

        Assert.Equal(60, mWorld.Get<Kinematic>(lTooSlow).Speed);
        Assert.Equal(600, mWorld.Get<Kinematic>(lTooFast).Speed);
    }

    [Fact]
    public void Update_ClimbsWithPitchAndStopsAtCeiling()
    {
        Entity lEntity = Spawn(new Orientation(0, 30, 0), 200, new KinematicLimits(null, null, 1500));

        mSimulation.Step(TimeSpan.FromSeconds(2));
        Assert.Equal(1200, mWorld.Get<Kinematic>(lEntity).Position.Altitude, 6);

        mSimulation.Run(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        Assert.Equal(1500, mWorld.Get<Kinematic>(lEntity).Position.Altitude);
    }

    [Fact]
    public void GeoMath_OffsetThenBearingAndDistance_AreConsistent()
    {
        GeoPosition lStart = new GeoPosition(48.7, 2.1, 0);

        GeoPosition lEnd = GeoMath.Offset(lStart, 45, 10000);

        Assert.Equal(10000, GeoMath.Distance(lStart, lEnd), 3);
        Assert.Equal(45, GeoMath.Bearing(lStart, lEnd), 1);
        Assert.Equal(270, GeoMath.NormalizeHeading(-90));
    }

    private Entity Spawn(Orientation pOrientation, double pSpeed, KinematicLimits pLimits)
    {
        Entity lEntity = mWorld.CreateEntity();
        mWorld.Set(lEntity, new Kinematic(new GeoPosition(0, 0, 1000), pOrientation, pSpeed));
        mWorld.Set(lEntity, pLimits);
        return lEntity;
    }
}
