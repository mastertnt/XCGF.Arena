using System.Text;
using OpenDisNet;
using OpenDisNet.Enumerations;
using OpenDisNet.Pdus;

namespace XCGF.Arena.Dis;

/// <summary>
/// Turns entity states into DIS 7 (IEEE 1278.1-2012) Entity State PDUs with OpenDisNet.
/// </summary>
public static class EntityStatePduEncoder
{
    private const int MarkingLength = 11;
    private const double SecondsPerHour = 3600;
    private const double TimestampUnitsPerHour = 1u << 31;

    /// <summary>
    /// Builds the Entity State PDU of an entity state.
    /// </summary>
    /// <param name="pState">The state.</param>
    /// <param name="pExerciseId">DIS exercise identifier.</param>
    /// <returns>The PDU. Dead reckoning is DRM(F, P, W): constant velocity, world coordinates.</returns>
    public static EntityStatePdu ToPdu(DisEntityState pState, byte pExerciseId)
    {
        ArgumentNullException.ThrowIfNull(pState);
        EntityStatePdu lPdu = DisPduBuilder.CreateEntityState(
            new EntityId(pState.Id.Site, pState.Id.Application, pState.Id.Entity),
            new EntityType
            {
                EntityKind = (EntityKind)pState.Type.Kind,
                Domain = new Domain { Value = pState.Type.Domain },
                Country = (Country)pState.Type.Country,
                Category = pState.Type.Category,
                SubCategory = pState.Type.Subcategory,
                Specific = pState.Type.Specific,
                Extra = pState.Type.Extra,
            },
            new Vector3Double { X = pState.Location.X, Y = pState.Location.Y, Z = pState.Location.Z },
            (ForceId)pState.Force,
            pExerciseId);
        lPdu.EntityLinearVelocity = new Vector3Float { X = (float)pState.Velocity.X, Y = (float)pState.Velocity.Y, Z = (float)pState.Velocity.Z };
        lPdu.EntityOrientation = new EulerAngles
        {
            Psi = (float)pState.Orientation.Psi,
            Theta = (float)pState.Orientation.Theta,
            Phi = (float)pState.Orientation.Phi,
        };
        lPdu.DeadReckoningParameters = new DeadReckoningParameters
        {
            DeadReckoningAlgorithm = DeadReckoningAlgorithm.DrmFpwConstantVelocityLowAccelerationLinearMotionEntity,
            Parameters = new byte[15],
            EntityLinearAcceleration = new Vector3Float(),
            EntityAngularVelocity = new Vector3Float(),
        };
        lPdu.Marking = new EntityMarking { CharacterSet = EntityMarkingCharacterSet.Ascii, Characters = EncodeMarking(pState.Marking) };
        lPdu.Timestamp = ToRelativeTimestamp(pState.Timestamp);
        return lPdu;
    }

    /// <summary>
    /// Encodes the Entity State PDU of an entity state.
    /// </summary>
    /// <param name="pState">The state.</param>
    /// <param name="pExerciseId">DIS exercise identifier.</param>
    /// <returns>The datagram.</returns>
    public static byte[] Encode(DisEntityState pState, byte pExerciseId) => DisSerializer.Serialize(ToPdu(pState, pExerciseId));

    /// <summary>
    /// Converts a simulation time into a DIS relative timestamp: units of 3600 s / 2^31 past the hour, low bit 0.
    /// </summary>
    /// <param name="pTime">The simulation time.</param>
    /// <returns>The timestamp.</returns>
    public static uint ToRelativeTimestamp(TimeSpan pTime)
    {
        double lSecondsPastHour = pTime.TotalSeconds % SecondsPerHour;
        uint lUnits = (uint)Math.Min(TimestampUnitsPerHour - 1, Math.Floor(lSecondsPastHour / SecondsPerHour * TimestampUnitsPerHour));
        return lUnits << 1;
    }

    private static byte[] EncodeMarking(string pMarking)
    {
        byte[] lCharacters = new byte[MarkingLength];
        byte[] lAscii = Encoding.ASCII.GetBytes(pMarking);
        Array.Copy(lAscii, lCharacters, Math.Min(lAscii.Length, MarkingLength));
        return lCharacters;
    }
}
