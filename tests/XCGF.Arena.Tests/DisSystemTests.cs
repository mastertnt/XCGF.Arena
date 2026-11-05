using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;
using XCGF.Arena.Dis;

namespace XCGF.Arena.Tests;

public class DisSystemTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void ToGeocentric_OnEquatorAndPole_MatchesWgs84()
    {
        GeocentricVector lEquator = DisConversion.ToGeocentric(new GeoPosition(0, 0, 0));
        GeocentricVector lEast = DisConversion.ToGeocentric(new GeoPosition(0, 90, 1000));
        GeocentricVector lPole = DisConversion.ToGeocentric(new GeoPosition(90, 0, 0));

        Assert.Equal(6378137, lEquator.X, 3);
        Assert.Equal(0, lEquator.Y, 3);
        Assert.Equal(6379137, lEast.Y, 3);
        Assert.Equal(6356752.314245, lPole.Z, 3);
    }

    [Fact]
    public void ToDisOrientation_FollowsTheGeocentricAxes()
    {
        // At latitude 0, longitude 0: north is +Z, east is +Y, down is -X.
        DisOrientation lNorth = DisConversion.ToDisOrientation(default, new Orientation(0, 0, 0));
        DisOrientation lEast = DisConversion.ToDisOrientation(default, new Orientation(90, 0, 0));
        DisOrientation lEastRolled = DisConversion.ToDisOrientation(default, new Orientation(90, 0, 30));

        Assert.Equal(-Math.PI / 2, lNorth.Theta, Tolerance);
        Assert.Equal(Math.PI / 2, lEast.Psi, Tolerance);
        Assert.Equal(0, lEast.Theta, Tolerance);
        Assert.Equal(-Math.PI / 2, lEast.Phi, Tolerance);
        Assert.Equal(-Math.PI / 2 + (Math.PI / 6), lEastRolled.Phi, Tolerance);
    }

    [Fact]
    public void ToGeocentricVelocity_PointsAlongHeading()
    {
        GeocentricVector lVelocity = DisConversion.ToGeocentricVelocity(new Kinematic(new GeoPosition(0, 0, 0), new Orientation(90, 0, 0), 250));

        Assert.Equal(0, lVelocity.X, Tolerance);
        Assert.Equal(250, lVelocity.Y, Tolerance);
        Assert.Equal(0, lVelocity.Z, Tolerance);
    }

    [Fact]
    public void DisEntityType_ParsesTheDisTypeOfTheArchetype()
    {
        Assert.Equal(new DisEntityType(1, 2, 71, 1, 0, 0, 0), DisEntityType.Parse("1.2.71.1.0.0.0"));
        Assert.Equal("1.2.71.1.0.0.0", DisEntityType.Parse("1.2.71.1.0.0.0").ToString());
        Assert.Throws<FormatException>(() => DisEntityType.Parse("1.2.71"));
    }

    [Fact]
    public void DisSystem_PublishesEntitiesAtItsInterval()
    {
        ArenaSimulation lArena = new ArenaSimulation(TestLibrary.Example);
        Entity lFighter = lArena.Factory.SpawnEntity("Fighter", new Dictionary<string, DataValue>
        {
            ["Kinematic.Position"] = new GeoPosition(48, 2, 8000).ToValue(),
        });
        lFighter.AddComponent(new Force(ForceSide.Opposing, "RED-LEADER-ONE"));
        Entity lAnonymous = lArena.Factory.SpawnEntity("Fighter");
        List<DisEntityState> lStates = new List<DisEntityState>();
        lArena.Systems.Add(new DisSystem(new DelegatePublisher(lStates.Add), 12, 34, TimeSpan.FromSeconds(5)));

        lArena.Run(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { 1.0, 1, 6, 6 }, lStates.Select(pState => pState.Timestamp.TotalSeconds));
        DisEntityState lState = lStates.First(pState => pState.Id.Entity == lFighter.Id);
        Assert.Equal(new DisEntityId(12, 34, (ushort)lFighter.Id), lState.Id);
        Assert.Equal(2, lState.Force);
        Assert.Equal("RED-LEADER-", lState.Marking);
        Assert.Equal(new DisEntityType(1, 2, 71, 1, 0, 0, 0), lState.Type);
        GeocentricVector lLocation = lState.Location;
        double lRadius = Math.Sqrt((lLocation.X * lLocation.X) + (lLocation.Y * lLocation.Y) + (lLocation.Z * lLocation.Z));
        Assert.InRange(lRadius, 6366000 + 8000 - 1000, 6366000 + 8000 + 1000);
        DisEntityState lOther = lStates.First(pState => pState.Id.Entity == lAnonymous.Id);
        Assert.Equal(0, lOther.Force);
        Assert.Equal("Fighter", lOther.Marking);
    }

    private sealed class DelegatePublisher(Action<DisEntityState> pPublish) : IDisPublisher
    {
        public void Publish(DisEntityState pState) => pPublish(pState);
    }
}
