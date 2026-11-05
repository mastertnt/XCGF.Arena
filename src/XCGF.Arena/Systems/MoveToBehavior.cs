using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Behaviour of the MoveTo task of the XCGF.Data standard library: steers the executor towards Destination, climbing
/// or descending to its altitude, at Speed or else at the speed of Throttle, and sets TravelTime when the task ends.
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

        if (Movement.ExecutorOf(pContext) is { } lExecutor)
        {
            Movement.LevelOff(lExecutor);
        }
    }

    private static void Steer(TaskContext pContext)
    {
        if (Movement.ExecutorOf(pContext) is not { } lExecutor || pContext.Task.Parameters.GetValueOrDefault("Destination") is not { } lDestination)
        {
            return;
        }

        GeoPosition lTarget = Movement.PositionOf(lDestination, lExecutor.GetComponent<Kinematic>().Position.Altitude);
        Movement.SteerTowards(lExecutor, lTarget, Movement.RequestedSpeed(lExecutor, pContext.Task));
    }
}
