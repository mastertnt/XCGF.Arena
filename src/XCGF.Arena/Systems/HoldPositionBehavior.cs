using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Behaviour of the HoldPosition task of the XCGF.Data standard library, with its DurationElapsed trigger.
/// </summary>
/// <remarks>
/// The task sets HoldPoint to the position of the executor when it starts. A kinematic that can stop (no StallSpeed)
/// stops there; one that cannot circles around it at Loiter throttle, or at its stall speed when it has no performance
/// table, by heading back to the hold point. DurationElapsed fires once Duration has elapsed since the start; without
/// Duration the task only ends when cancelled.
/// </remarks>
public sealed class HoldPositionBehavior : ITaskBehavior
{
    /// <summary>
    /// Name of the task this behaviour implements.
    /// </summary>
    public const string TaskName = "HoldPosition";

    /// <summary>
    /// Kind of the custom trigger that ends the hold.
    /// </summary>
    public const string DurationElapsedKind = "DurationElapsed";

    /// <summary>
    /// Throttle setting of a kinematic that circles.
    /// </summary>
    public const string LoiterThrottle = "Loiter";

    /// <summary>
    /// Evaluates the DurationElapsed trigger.
    /// </summary>
    /// <param name="pContext">The task context.</param>
    /// <param name="pTrigger">The trigger; its <c>parameter</c> argument names the duration parameter.</param>
    /// <returns>True once the duration has elapsed since the task started.</returns>
    public static bool IsElapsed(TaskContext pContext, CustomTrigger pTrigger)
    {
        ArgumentNullException.ThrowIfNull(pContext);
        ArgumentNullException.ThrowIfNull(pTrigger);
        string lParameter = pTrigger.Arguments.FirstOrDefault(pArgument => pArgument.Name == "parameter")?.Value ?? "Duration";
        return pContext.Task.Parameters.GetValueOrDefault(lParameter)?.AsNumber() is { } lDuration
            && pContext.Task.StartedAt is { } lStart
            && (pContext.Elapsed - lStart).TotalSeconds >= lDuration - 1e-9;
    }

    /// <inheritdoc/>
    public void OnStart(TaskContext pContext)
    {
        if (Movement.ExecutorOf(pContext) is not { } lExecutor)
        {
            return;
        }

        pContext.Task.Parameters["HoldPoint"] = lExecutor.GetComponent<Kinematic>().Position.ToValue();
        Hold(pContext);
    }

    /// <inheritdoc/>
    public void OnUpdate(TaskContext pContext) => Hold(pContext);

    /// <inheritdoc/>
    public void OnEnd(TaskContext pContext)
    {
        if (Movement.ExecutorOf(pContext) is { } lExecutor)
        {
            Movement.LevelOff(lExecutor);
        }
    }

    private static void Hold(TaskContext pContext)
    {
        if (Movement.ExecutorOf(pContext) is not { } lExecutor || pContext.Task.Parameters.GetValueOrDefault("HoldPoint") is not { } lHoldPoint)
        {
            return;
        }

        lExecutor.TryGetComponent(out KinematicLimits lLimits);
        if (lLimits.StallSpeed is not { } lStallSpeed)
        {
            lExecutor.GetComponent<Kinematic>().Speed = 0;
            Movement.LevelOff(lExecutor);
            return;
        }

        double lSpeed = Movement.ThrottleSpeed(lExecutor, LoiterThrottle) ?? lStallSpeed;
        Movement.SteerTowards(lExecutor, GeoPosition.FromValue(lHoldPoint), lSpeed);
    }
}
