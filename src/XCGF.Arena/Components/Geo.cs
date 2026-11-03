using GeographicLib;
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
/// Geodesic computations on the WGS84 ellipsoid, delegated to GeographicLib.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Computes the horizontal geodesic distance between two positions, ignoring altitude.
    /// </summary>
    /// <param name="pFrom">The first position.</param>
    /// <param name="pTo">The second position.</param>
    /// <returns>The distance, in metres.</returns>
    public static double Distance(GeoPosition pFrom, GeoPosition pTo)
    {
        Geodesic.WGS84.Inverse(pFrom.Latitude, pFrom.Longitude, pTo.Latitude, pTo.Longitude, out double lDistance);
        return lDistance;
    }

    /// <summary>
    /// Computes the initial azimuth of the geodesic from one position to another.
    /// </summary>
    /// <param name="pFrom">The start.</param>
    /// <param name="pTo">The destination.</param>
    /// <returns>The azimuth from true north, clockwise, in [0, 360) degrees.</returns>
    public static double Bearing(GeoPosition pFrom, GeoPosition pTo)
    {
        Geodesic.WGS84.Inverse(pFrom.Latitude, pFrom.Longitude, pTo.Latitude, pTo.Longitude, out _, out double lAzimuth, out _);
        return NormalizeHeading(lAzimuth);
    }

    /// <summary>
    /// Computes the position reached by following a geodesic.
    /// </summary>
    /// <param name="pFrom">The start. Its altitude is kept.</param>
    /// <param name="pHeading">The initial azimuth, in degrees.</param>
    /// <param name="pDistance">The horizontal distance, in metres.</param>
    /// <returns>The position reached.</returns>
    public static GeoPosition Offset(GeoPosition pFrom, double pHeading, double pDistance)
    {
        Geodesic.WGS84.Direct(pFrom.Latitude, pFrom.Longitude, pHeading, pDistance, out double lLatitude, out double lLongitude);
        return new GeoPosition(lLatitude, lLongitude, pFrom.Altitude);
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
}
