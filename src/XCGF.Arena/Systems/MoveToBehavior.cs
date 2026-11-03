using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Behaviour of the MoveTo task: steers the executor towards Destination at Speed, keeping its altitude, and sets
/// TravelTime when the task ends.
/// </summary>
public sealed class MoveToBehavior : ITaskBehavior
{
    /// <summary>
    /// Name of the task this behaviour implements.
    /// </summary>
    public const string TaskName = "MoveTo";

    /// <inheritdoc/>
    public void OnStart(TaskContext pContext) => Steer(pContext);

    /// <inheritdoc/>
    public void OnUpdate(TaskContext pContext) => Steer(pContext);

    /// <inheritdoc/>
    public void OnEnd(TaskContext pContext)
    {
        ArgumentNullException.ThrowIfNull(pContext);
        TaskInstance lTask = pContext.Task;
        if (lTask.Parameters.ContainsKey("TravelTime") && lTask.StartedAt is { } lStart && lTask.EndedAt is { } lEnd)
        {
            lTask.Parameters["TravelTime"] = new NumberValue((lEnd - lStart).TotalSeconds);
        }
    }

    private static void Steer(TaskContext pContext)
    {
        ArgumentNullException.ThrowIfNull(pContext);
        TaskInstance lTask = pContext.Task;
        Ecs.Entity lExecutor = lTask.Parameters.GetValueOrDefault("Executor") is ReferenceValue lReference ? lReference.Target : pContext.Carrier;
        if (!pContext.World.Has<Kinematic>(lExecutor) || lTask.Parameters.GetValueOrDefault("Destination") is not { } lDestinationValue)
        {
            return;
        }

        ref Kinematic lKinematic = ref pContext.World.Get<Kinematic>(lExecutor);
        GeoPosition lDestination = GeoPosition.FromValue(lDestinationValue);
        lKinematic.Orientation = lKinematic.Orientation with
        {
            Heading = GeoMath.Bearing(lKinematic.Position, lDestination),
            Pitch = 0,
        };
        if (lTask.Parameters.GetValueOrDefault("Speed")?.AsNumber() is { } lSpeed)
        {
            lKinematic.Speed = lSpeed;
        }
    }
}
