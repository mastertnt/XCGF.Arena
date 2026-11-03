using XCGF.Arena.Ecs;

namespace XCGF.Arena.Tests;

public class WorldTests
{
    private record struct Position(double X);

    private sealed record Name(string Value);

    [Fact]
    public void CreateEntity_ReturnsDistinctLivingEntities()
    {
        World lWorld = new World();

        Entity lFirst = lWorld.CreateEntity();
        Entity lSecond = lWorld.CreateEntity();

        Assert.NotEqual(lFirst, lSecond);
        Assert.True(lWorld.IsAlive(lFirst));
        Assert.True(lWorld.IsAlive(lSecond));
        Assert.Equal(2, lWorld.EntityCount);
        Assert.False(lWorld.IsAlive(Entity.Null));
    }

    [Fact]
    public void DestroyEntity_RecyclesIdWithNewGeneration()
    {
        World lWorld = new World();
        Entity lOld = lWorld.CreateEntity();
        lWorld.Set(lOld, new Position(1));

        Assert.True(lWorld.DestroyEntity(lOld));
        Entity lNew = lWorld.CreateEntity();

        Assert.Equal(lOld.Id, lNew.Id);
        Assert.NotEqual(lOld.Generation, lNew.Generation);
        Assert.False(lWorld.IsAlive(lOld));
        Assert.False(lWorld.Has<Position>(lNew));
        Assert.False(lWorld.DestroyEntity(lOld));
        Assert.Throws<InvalidOperationException>(() => lWorld.Set(lOld, new Position(2)));
    }

    [Fact]
    public void Get_ReturnsReferenceThatModifiesComponentInPlace()
    {
        World lWorld = new World();
        Entity lEntity = lWorld.CreateEntity();
        lWorld.Set(lEntity, new Position(1));

        lWorld.Get<Position>(lEntity).X = 5;

        Assert.Equal(5, lWorld.Get<Position>(lEntity).X);
    }

    [Fact]
    public void Set_ReplacesExistingComponent()
    {
        World lWorld = new World();
        Entity lEntity = lWorld.CreateEntity();

        lWorld.Set(lEntity, new Name("a"));
        lWorld.Set(lEntity, new Name("b"));

        Assert.Equal("b", lWorld.Get<Name>(lEntity).Value);
        Assert.Single(lWorld.Query<Name>());
    }

    [Fact]
    public void Remove_KeepsOtherEntitiesComponents()
    {
        World lWorld = new World();
        Entity[] lEntities = Enumerable.Range(0, 5).Select(pIndex =>
        {
            Entity lEntity = lWorld.CreateEntity();
            lWorld.Set(lEntity, new Position(pIndex));
            return lEntity;
        }).ToArray();

        Assert.True(lWorld.Remove<Position>(lEntities[1]));
        Assert.False(lWorld.Remove<Position>(lEntities[1]));

        Assert.False(lWorld.Has<Position>(lEntities[1]));
        Assert.Equal(4, lWorld.Query<Position>().Count);
        foreach (int lIndex in new[] { 0, 2, 3, 4 })
        {
            Assert.Equal(lIndex, lWorld.Get<Position>(lEntities[lIndex]).X);
        }
    }

    [Fact]
    public void Query_ReturnsEntitiesWithAllComponents()
    {
        World lWorld = new World();
        Entity lBoth = lWorld.CreateEntity();
        lWorld.Set(lBoth, new Position(1));
        lWorld.Set(lBoth, new Name("both"));
        Entity lPositionOnly = lWorld.CreateEntity();
        lWorld.Set(lPositionOnly, new Position(2));
        Entity lNameOnly = lWorld.CreateEntity();
        lWorld.Set(lNameOnly, new Name("name"));

        Assert.Equal(new[] { lBoth }, lWorld.Query<Position, Name>());
        Assert.Equal(new[] { lBoth }, lWorld.Query<Name, Position>());
        Assert.Equal(2, lWorld.Query<Position>().Count);
        Assert.Empty(lWorld.Query<Position, Name, int>());
    }

    [Fact]
    public void Query_IsASnapshotThatToleratesChanges()
    {
        World lWorld = new World();
        for (int lIndex = 0; lIndex < 3; lIndex++)
        {
            lWorld.Set(lWorld.CreateEntity(), new Position(lIndex));
        }

        foreach (Entity lEntity in lWorld.Query<Position>())
        {
            lWorld.DestroyEntity(lEntity);
            lWorld.Set(lWorld.CreateEntity(), new Name("new"));
        }

        Assert.Empty(lWorld.Query<Position>());
        Assert.Equal(3, lWorld.Query<Name>().Count);
    }

    [Fact]
    public void Get_WithoutComponent_Throws()
    {
        World lWorld = new World();
        Entity lEntity = lWorld.CreateEntity();

        Assert.Throws<KeyNotFoundException>(() => lWorld.Get<Position>(lEntity));
        Assert.False(lWorld.TryGet(lEntity, out Position _));
    }

    [Fact]
    public void Simulation_RunsSystemsInOrderAndAdvancesTime()
    {
        World lWorld = new World();
        List<string> lCalls = new List<string>();
        Simulation lSimulation = new Simulation(lWorld)
            .AddSystem(new RecordingSystem("a", lCalls))
            .AddSystem(new RecordingSystem("b", lCalls));

        lSimulation.Run(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(2.5), lSimulation.Elapsed);
        Assert.Equal(new[] { "a@1", "b@1", "a@2", "b@2", "a@2.5", "b@2.5" }, lCalls);
        Assert.Throws<ArgumentOutOfRangeException>(() => lSimulation.Step(TimeSpan.Zero));
    }

    private sealed class RecordingSystem(string pName, List<string> pCalls) : ISystem
    {
        public void Update(World pWorld, SimulationTime pTime) =>
            pCalls.Add($"{pName}@{pTime.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }
}
