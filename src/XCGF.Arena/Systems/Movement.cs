using Friflo.Engine.ECS;
using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Steering shared by the movement tasks of the XCGF.Data standard library (docs/tasks/kinematics.md).
/// </summary>
public static class Movement
{
    /// <summary>
    /// Time constant of the altitude capture, in seconds: the executor aims at closing the altitude gap in this time,
    /// within its ClimbRate.
    /// </summary>
    public const double AltitudeTimeConstant = 10;

    /// <summary>
    /// Finds the executor of a movement task: its Executor parameter, or else the carrier of the task.
    /// </summary>
    /// <param name="pContext">The task context.</param>
    /// <returns>The executor; null when it has no <see cref="Kinematic"/>.</returns>
    public static Entity? ExecutorOf(TaskContext pContext)
    {
        ArgumentNullException.ThrowIfNull(pContext);
        Entity lExecutor = pContext.Task.Parameters.GetValueOrDefault("Executor") is ReferenceValue lReference ? lReference.Target : pContext.Carrier;
        return lExecutor.IsNull || !lExecutor.HasComponent<Kinematic>() ? null : lExecutor;
    }

    /// <summary>
    /// Reads a GeoPosition value; a record without Altitude keeps the given altitude.
    /// </summary>
    /// <param name="pValue">The value.</param>
    /// <param name="pAltitude">Altitude used when the value has none.</param>
    /// <returns>The position.</returns>
    public static GeoPosition PositionOf(DataValue pValue, double pAltitude)
    {
        GeoPosition lPosition = GeoPosition.FromValue(pValue);
        return pValue is RecordValue lRecord && !lRecord.Fields.ContainsKey("Altitude") ? lPosition with { Altitude = pAltitude } : lPosition;
    }

    /// <summary>
    /// Speed of a throttle setting at the current altitude of the executor, read from the PerformanceTable of its
    /// kinematic: the row of that throttle whose altitude band contains the altitude, or else the nearest band.
    /// </summary>
    /// <param name="pExecutor">The executor.</param>
    /// <param name="pThrottle">Name of the throttle setting, e.g. Cruise.</param>
    /// <returns>The speed; null when the kinematic has no matching row.</returns>
    public static double? ThrottleSpeed(Entity pExecutor, string pThrottle)
    {
        if (pExecutor.FindEquipment("Kinematic") is not { IsNull: false } lKinematic
            || !lKinematic.TryGetComponent(out EquipmentData lData)
            || lData.Technological.Get("PerformanceTable") is not ArrayValue lTable)
        {
            return null;
        }

        double lAltitude = pExecutor.GetComponent<Kinematic>().Position.Altitude;
        double? lBest = null;
        double lBestGap = double.PositiveInfinity;
        foreach (RecordValue lRow in lTable.Items.OfType<RecordValue>())
        {
            if (lRow.Fields.GetValueOrDefault("Throttle") is not EnumValue { } lThrottle || lThrottle.Name != pThrottle)
            {
                continue;
            }

            double lMin = lRow.GetNumber("MinAltitude");
            double lMax = lRow.Fields.ContainsKey("MaxAltitude") ? lRow.GetNumber("MaxAltitude") : double.PositiveInfinity;
            double lGap = lAltitude < lMin ? lMin - lAltitude : lAltitude > lMax ? lAltitude - lMax : 0;
            if (lGap < lBestGap)
            {
                lBestGap = lGap;
                lBest = lRow.GetNumber("Speed");
            }
        }

        return lBest;
    }

    /// <summary>
    /// Speed asked by a task: its Speed parameter when set, or else the speed of its Throttle parameter.
    /// </summary>
    /// <param name="pExecutor">The executor.</param>
    /// <param name="pTask">The task.</param>
    /// <param name="pLegSpeed">Speed of the current leg, which wins over Throttle but not over Speed.</param>
    /// <returns>The speed; null to keep the current one.</returns>
    public static double? RequestedSpeed(Entity pExecutor, TaskInstance pTask, double? pLegSpeed = null)
    {
        ArgumentNullException.ThrowIfNull(pTask);
        if (pTask.Parameters.GetValueOrDefault("Speed")?.AsNumber() is { } lSpeed)
        {
            return lSpeed;
        }

        if (pLegSpeed is > 0)
        {
            return pLegSpeed;
        }

        return pTask.Parameters.GetValueOrDefault("Throttle") is EnumValue lThrottle ? ThrottleSpeed(pExecutor, lThrottle.Name) : null;
    }

    /// <summary>
    /// Heads the executor towards a point and climbs or descends towards its altitude, within the ClimbRate of the
    /// kinematic.
    /// </summary>
    /// <param name="pExecutor">The executor.</param>
    /// <param name="pTarget">The point.</param>
    /// <param name="pSpeed">Speed to hold; null keeps the current one.</param>
    public static void SteerTowards(Entity pExecutor, GeoPosition pTarget, double? pSpeed)
    {
        ref Kinematic lKinematic = ref pExecutor.GetComponent<Kinematic>();
        if (pSpeed is { } lSpeed)
        {
            lKinematic.Speed = lSpeed;
        }

        double lPitch = 0;
        if (lKinematic.Speed > 0)
        {
            double lClimbRate = ClimbRateOf(pExecutor) ?? double.PositiveInfinity;
            double lVertical = Math.Clamp((pTarget.Altitude - lKinematic.Position.Altitude) / AltitudeTimeConstant, -lClimbRate, lClimbRate);
            lPitch = Math.Asin(Math.Clamp(lVertical / lKinematic.Speed, -1, 1)) * 180 / Math.PI;
        }

        lKinematic.Orientation = lKinematic.Orientation with
        {
            Heading = GeoMath.Bearing(lKinematic.Position, pTarget),
            Pitch = lPitch,
        };
    }

    /// <summary>
    /// Levels the executor off.
    /// </summary>
    /// <param name="pExecutor">The executor.</param>
    public static void LevelOff(Entity pExecutor)
    {
        ref Kinematic lKinematic = ref pExecutor.GetComponent<Kinematic>();
        lKinematic.Orientation = lKinematic.Orientation with { Pitch = 0 };
    }

    private static double? ClimbRateOf(Entity pExecutor)
    {
        return pExecutor.FindEquipment("Kinematic") is { IsNull: false } lKinematic && lKinematic.TryGetComponent(out EquipmentData lData)
            ? lData.Technological.GetNumber("ClimbRate")
            : null;
    }
}
