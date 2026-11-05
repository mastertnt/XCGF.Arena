namespace XCGF.Arena.Dis;

/// <summary>
/// DIS entity identifier.
/// </summary>
/// <param name="Site">Site number.</param>
/// <param name="Application">Application number.</param>
/// <param name="Entity">Entity number, unique for the site and application.</param>
public readonly record struct DisEntityId(ushort Site, ushort Application, ushort Entity);

/// <summary>
/// DIS entity type: Kind.Domain.Country.Category.Subcategory.Specific.Extra.
/// </summary>
/// <param name="Kind">Entity kind, e.g. 1 for platform.</param>
/// <param name="Domain">Domain, e.g. 2 for air.</param>
/// <param name="Country">Country code.</param>
/// <param name="Category">Category.</param>
/// <param name="Subcategory">Subcategory.</param>
/// <param name="Specific">Specific.</param>
/// <param name="Extra">Extra.</param>
public readonly record struct DisEntityType(byte Kind, byte Domain, ushort Country, byte Category, byte Subcategory, byte Specific, byte Extra)
{
    /// <summary>
    /// Parses a dotted DIS type, as written in the disType attribute of an XCGF.Data entity.
    /// </summary>
    /// <param name="pText">The dotted type, e.g. 1.2.71.1.0.0.0.</param>
    /// <returns>The type.</returns>
    public static DisEntityType Parse(string pText)
    {
        ArgumentNullException.ThrowIfNull(pText);
        string[] lParts = pText.Split('.');
        if (lParts.Length != 7)
        {
            throw new FormatException($"'{pText}' is not a DIS entity type of seven dotted numbers.");
        }

        System.Globalization.CultureInfo lCulture = System.Globalization.CultureInfo.InvariantCulture;
        return new DisEntityType(
            byte.Parse(lParts[0], lCulture),
            byte.Parse(lParts[1], lCulture),
            ushort.Parse(lParts[2], lCulture),
            byte.Parse(lParts[3], lCulture),
            byte.Parse(lParts[4], lCulture),
            byte.Parse(lParts[5], lCulture),
            byte.Parse(lParts[6], lCulture));
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}.{Domain}.{Country}.{Category}.{Subcategory}.{Specific}.{Extra}";
}

/// <summary>
/// Cartesian vector in the geocentric (ECEF) frame DIS uses for world coordinates.
/// </summary>
/// <param name="X">X, towards latitude 0 longitude 0.</param>
/// <param name="Y">Y, towards latitude 0 longitude 90 east.</param>
/// <param name="Z">Z, towards the north pole.</param>
public readonly record struct GeocentricVector(double X, double Y, double Z);

/// <summary>
/// Euler angles of an entity relative to the geocentric frame, as DIS expects them.
/// </summary>
/// <param name="Psi">Rotation about Z, in radians.</param>
/// <param name="Theta">Rotation about the rotated Y, in radians.</param>
/// <param name="Phi">Rotation about the rotated X, in radians.</param>
public readonly record struct DisOrientation(double Psi, double Theta, double Phi);

/// <summary>
/// Content of an Entity State PDU, independent of the library that encodes it.
/// </summary>
/// <param name="Id">Entity identifier.</param>
/// <param name="Force">Force identifier: 0 other, 1 friendly, 2 opposing, 3 neutral.</param>
/// <param name="Type">Entity type.</param>
/// <param name="Location">Location of the entity, in geocentric metres.</param>
/// <param name="Velocity">Linear velocity, in geocentric metres per second.</param>
/// <param name="Orientation">Orientation.</param>
/// <param name="Marking">Marking text, at most 11 ASCII characters.</param>
/// <param name="Timestamp">Simulation time of the state.</param>
public sealed record DisEntityState(
    DisEntityId Id,
    byte Force,
    DisEntityType Type,
    GeocentricVector Location,
    GeocentricVector Velocity,
    DisOrientation Orientation,
    string Marking,
    TimeSpan Timestamp);
