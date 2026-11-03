using XCGF.Arena.Data;

namespace XCGF.Arena.Components;

/// <summary>
/// WGS84 geodetic position.
/// </summary>
/// <param name="Latitude">Latitude, in degrees.</param>
/// <param name="Longitude">Longitude, in degrees.</param>
/// <param name="Altitude">Altitude above mean sea level, in metres.</param>
public readonly record struct GeoPosition(double Latitude, double Longitude, double Altitude)
{
    /// <summary>
    /// Reads a position from a GeoPosition or GeoPoint2D record. A missing altitude reads as 0.
    /// </summary>
    /// <param name="pValue">The record.</param>
    /// <returns>The position.</returns>
    public static GeoPosition FromValue(DataValue pValue)
    {
        RecordValue lRecord = pValue as RecordValue
            ?? throw new XcgfDataException($"A geographic position must be a record, not {pValue}.");
        return new GeoPosition(lRecord.GetNumber("Latitude"), lRecord.GetNumber("Longitude"), lRecord.GetNumber("Altitude"));
    }

    /// <summary>
    /// Writes the position as a GeoPosition record.
    /// </summary>
    /// <returns>The record.</returns>
    public RecordValue ToValue()
    {
        return RecordValue.Of(("Latitude", new NumberValue(Latitude)), ("Longitude", new NumberValue(Longitude)), ("Altitude", new NumberValue(Altitude)));
    }
}

/// <summary>
/// Attitude of an entity.
/// </summary>
/// <param name="Heading">Heading from true north, clockwise, in degrees.</param>
/// <param name="Pitch">Pitch, positive nose up, in degrees.</param>
/// <param name="Roll">Roll, positive right wing down, in degrees.</param>
public readonly record struct Orientation(double Heading, double Pitch, double Roll)
{
    /// <summary>
    /// Reads an orientation from an Orientation record.
    /// </summary>
    /// <param name="pValue">The record.</param>
    /// <returns>The orientation.</returns>
    public static Orientation FromValue(DataValue pValue)
    {
        RecordValue lRecord = pValue as RecordValue
            ?? throw new XcgfDataException($"An orientation must be a record, not {pValue}.");
        return new Orientation(lRecord.GetNumber("Heading"), lRecord.GetNumber("Pitch"), lRecord.GetNumber("Roll"));
    }

    /// <summary>
    /// Writes the orientation as an Orientation record.
    /// </summary>
    /// <returns>The record.</returns>
    public RecordValue ToValue()
    {
        return RecordValue.Of(("Heading", new NumberValue(Heading)), ("Pitch", new NumberValue(Pitch)), ("Roll", new NumberValue(Roll)));
    }
}

/// <summary>
/// Spherical earth computations, accurate enough for the arena.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Mean earth radius, in metres.
    /// </summary>
    public const double EarthRadius = 6371000;

    /// <summary>
    /// Computes the horizontal great-circle distance between two positions, ignoring altitude.
    /// </summary>
    /// <param name="pFrom">The first position.</param>
    /// <param name="pTo">The second position.</param>
    /// <returns>The distance, in metres.</returns>
    public static double Distance(GeoPosition pFrom, GeoPosition pTo)
    {
        double lLat1 = ToRadians(pFrom.Latitude);
        double lLat2 = ToRadians(pTo.Latitude);
        double lDeltaLat = lLat2 - lLat1;
        double lDeltaLon = ToRadians(pTo.Longitude - pFrom.Longitude);
        double lA = Math.Pow(Math.Sin(lDeltaLat / 2), 2) + (Math.Cos(lLat1) * Math.Cos(lLat2) * Math.Pow(Math.Sin(lDeltaLon / 2), 2));
        return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(lA)));
    }

    /// <summary>
    /// Computes the initial bearing to fly from one position to another.
    /// </summary>
    /// <param name="pFrom">The start.</param>
    /// <param name="pTo">The destination.</param>
    /// <returns>The bearing from true north, clockwise, in [0, 360) degrees.</returns>
    public static double Bearing(GeoPosition pFrom, GeoPosition pTo)
    {
        double lLat1 = ToRadians(pFrom.Latitude);
        double lLat2 = ToRadians(pTo.Latitude);
        double lDeltaLon = ToRadians(pTo.Longitude - pFrom.Longitude);
        double lY = Math.Sin(lDeltaLon) * Math.Cos(lLat2);
        double lX = (Math.Cos(lLat1) * Math.Sin(lLat2)) - (Math.Sin(lLat1) * Math.Cos(lLat2) * Math.Cos(lDeltaLon));
        return NormalizeHeading(ToDegrees(Math.Atan2(lY, lX)));
    }

    /// <summary>
    /// Computes the position reached by flying a distance along a great circle.
    /// </summary>
    /// <param name="pFrom">The start. Its altitude is kept.</param>
    /// <param name="pHeading">The initial heading, in degrees.</param>
    /// <param name="pDistance">The horizontal distance, in metres.</param>
    /// <returns>The position reached.</returns>
    public static GeoPosition Offset(GeoPosition pFrom, double pHeading, double pDistance)
    {
        double lAngular = pDistance / EarthRadius;
        double lHeading = ToRadians(pHeading);
        double lLat1 = ToRadians(pFrom.Latitude);
        double lLon1 = ToRadians(pFrom.Longitude);
        double lLat2 = Math.Asin((Math.Sin(lLat1) * Math.Cos(lAngular)) + (Math.Cos(lLat1) * Math.Sin(lAngular) * Math.Cos(lHeading)));
        double lLon2 = lLon1 + Math.Atan2(
            Math.Sin(lHeading) * Math.Sin(lAngular) * Math.Cos(lLat1),
            Math.Cos(lAngular) - (Math.Sin(lLat1) * Math.Sin(lLat2)));
        double lLongitude = ToDegrees(lLon2);
        lLongitude = ((lLongitude + 540) % 360) - 180;
        return new GeoPosition(ToDegrees(lLat2), lLongitude, pFrom.Altitude);
    }

    /// <summary>
    /// Brings a heading into [0, 360).
    /// </summary>
    /// <param name="pHeading">The heading, in degrees.</param>
    /// <returns>The normalized heading.</returns>
    public static double NormalizeHeading(double pHeading)
    {
        double lHeading = pHeading % 360;
        return lHeading < 0 ? lHeading + 360 : lHeading;
    }

    private static double ToRadians(double pDegrees) => pDegrees * Math.PI / 180;

    private static double ToDegrees(double pRadians) => pRadians * 180 / Math.PI;
}
