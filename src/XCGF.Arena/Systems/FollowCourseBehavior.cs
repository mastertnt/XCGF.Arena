using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Behaviour of the FollowCourse task of the XCGF.Data standard library, with its CourseCompleted trigger.
/// </summary>
/// <remarks>
/// The executor flies towards Waypoints[CurrentWaypoint], at the speed of the task (Speed is not a parameter of
/// FollowCourse), else of the waypoint, else of Throttle. Once within the trigger tolerance (500 m by default) it heads
/// for the next waypoint; after the last one it restarts from the first when Loop is true, or else the course is
/// completed and the task ends.
/// </remarks>
public sealed class FollowCourseBehavior : ITaskBehavior
{
    /// <summary>
    /// Name of the task this behaviour implements.
    /// </summary>
    public const string TaskName = "FollowCourse";

    /// <summary>
    /// Kind of the custom trigger that ends the course.
    /// </summary>
    public const string CourseCompletedKind = "CourseCompleted";

    /// <summary>
    /// Distance at which a waypoint is reached when the trigger gives no tolerance, in metres.
    /// </summary>
    public const double DefaultTolerance = 500;

    /// <summary>
    /// Evaluates the CourseCompleted trigger.
    /// </summary>
    /// <param name="pContext">The task context.</param>
    /// <param name="pTrigger">The trigger.</param>
    /// <returns>True once the last waypoint is reached on a course that does not loop.</returns>
    public static bool IsCompleted(TaskContext pContext, CustomTrigger pTrigger)
    {
        ArgumentNullException.ThrowIfNull(pContext);
        ArgumentNullException.ThrowIfNull(pTrigger);
        return Advance(pContext, ToleranceOf(pTrigger));
    }

    /// <inheritdoc/>
    public void OnStart(TaskContext pContext) => Steer(pContext);

    /// <inheritdoc/>
    public void OnUpdate(TaskContext pContext) => Steer(pContext);

    /// <inheritdoc/>
    public void OnEnd(TaskContext pContext)
    {
        if (Movement.ExecutorOf(pContext) is { } lExecutor)
        {
            Movement.LevelOff(lExecutor);
        }
    }

    private static double ToleranceOf(CustomTrigger pTrigger)
    {
        return pTrigger.Arguments.FirstOrDefault(pArgument => pArgument.Name == "tolerance")?.Value is { } lText
            && double.TryParse(lText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lTolerance)
            ? lTolerance
            : DefaultTolerance;
    }

    // Moves CurrentWaypoint past the waypoints reached; true when the course is over.
    private static bool Advance(TaskContext pContext, double pTolerance)
    {
        TaskInstance lTask = pContext.Task;
        if (Movement.ExecutorOf(pContext) is not { } lExecutor || lTask.Parameters.GetValueOrDefault("Waypoints") is not ArrayValue { Items.Count: > 0 } lWaypoints)
        {
            return false;
        }

        int lCurrent = (int)(lTask.Parameters.GetValueOrDefault("CurrentWaypoint")?.AsNumber() ?? 0);
        GeoPosition lPosition = lExecutor.GetComponent<Kinematic>().Position;
        if (lCurrent >= lWaypoints.Items.Count || GeoMath.Distance(lPosition, TargetOf(lWaypoints.Items[lCurrent], lPosition)) > pTolerance)
        {
            return lCurrent >= lWaypoints.Items.Count;
        }

        lCurrent++;
        if (lCurrent >= lWaypoints.Items.Count)
        {
            if (!SensorModel.IsTrue(lTask.Parameters.GetValueOrDefault("Loop"), false))
            {
                return true;
            }

            lCurrent = 0;
        }

        lTask.Parameters["CurrentWaypoint"] = new IntegerValue(lCurrent);
        return false;
    }

    private static GeoPosition TargetOf(DataValue pWaypoint, GeoPosition pCurrent)
    {
        return pWaypoint is RecordValue lRecord && lRecord.Fields.TryGetValue("Position", out DataValue? lPosition)
            ? Movement.PositionOf(lPosition, pCurrent.Altitude)
            : pCurrent;
    }

    private static void Steer(TaskContext pContext)
    {
        TaskInstance lTask = pContext.Task;
        if (Movement.ExecutorOf(pContext) is not { } lExecutor || lTask.Parameters.GetValueOrDefault("Waypoints") is not ArrayValue { Items.Count: > 0 } lWaypoints)
        {
            return;
        }

        int lCurrent = Math.Clamp((int)(lTask.Parameters.GetValueOrDefault("CurrentWaypoint")?.AsNumber() ?? 0), 0, lWaypoints.Items.Count - 1);
        DataValue lWaypoint = lWaypoints.Items[lCurrent];
        double? lLegSpeed = lWaypoint is RecordValue lRecord && lRecord.Fields.TryGetValue("Speed", out DataValue? lSpeed) ? lSpeed.AsNumber() : null;
        GeoPosition lTarget = TargetOf(lWaypoint, lExecutor.GetComponent<Kinematic>().Position);
        Movement.SteerTowards(lExecutor, lTarget, Movement.RequestedSpeed(lExecutor, lTask, lLegSpeed));
    }
}
