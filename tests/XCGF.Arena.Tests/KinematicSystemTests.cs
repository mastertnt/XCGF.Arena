using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using XCGF.Arena.Components;
using XCGF.Arena.Systems;

namespace XCGF.Arena.Tests;

public class KinematicSystemTests
{
    private readonly EntityStore mStore = new EntityStore();
    private readonly SystemRoot mSystems;
    private float mElapsed;

    public KinematicSystemTests()
    {
        mSystems = new SystemRoot(mStore) { new KinematicSystem() };
    }

    [Fact]
    public void Update_MovesAlongHeadingAtSpeed()
    {
        Entity lEntity = Spawn(new Orientation(90, 0, 0), 250, new KinematicLimits(null, null, null));

        Run(10);

        Kinematic lKinematic = lEntity.GetComponent<Kinematic>();
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

        Step(1);

        Assert.Equal(60, lTooSlow.GetComponent<Kinematic>().Speed);
        Assert.Equal(600, lTooFast.GetComponent<Kinematic>().Speed);
    }

    [Fact]
    public void Update_ClimbsWithPitchAndStopsAtCeiling()
    {
        Entity lEntity = Spawn(new Orientation(0, 30, 0), 200, new KinematicLimits(null, null, 1500));

        Step(2);
        Assert.Equal(1200, lEntity.GetComponent<Kinematic>().Position.Altitude, 6);

        Run(10);
        Assert.Equal(1500, lEntity.GetComponent<Kinematic>().Position.Altitude);
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
        Entity lEntity = mStore.CreateEntity();
        lEntity.AddComponent(new Kinematic(new GeoPosition(0, 0, 1000), pOrientation, pSpeed));
        lEntity.AddComponent(pLimits);
        return lEntity;
    }

    private void Step(float pSeconds)
    {
        mElapsed += pSeconds;
        mSystems.Update(new UpdateTick(pSeconds, mElapsed));
    }

    private void Run(int pSteps)
    {
        for (int lIndex = 0; lIndex < pSteps; lIndex++)
        {
            Step(1);
        }
    }
}
