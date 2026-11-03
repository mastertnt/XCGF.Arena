using GeographicLib;
using XCGF.Arena.Components;

namespace XCGF.Arena.Dis;

/// <summary>
/// Converts the geodetic kinematic state of the arena into the geocentric frame of DIS.
/// </summary>
public static class DisConversion
{
    private const double DegreesToRadians = Math.PI / 180;

    /// <summary>
    /// Converts a WGS84 geodetic position into geocentric coordinates.
    /// </summary>
    /// <remarks>The altitude is taken as a height above the ellipsoid; the geoid separation is ignored.</remarks>
    /// <param name="pPosition">The position.</param>
    /// <returns>The geocentric coordinates, in metres.</returns>
    public static GeocentricVector ToGeocentric(GeoPosition pPosition)
    {
        (double lX, double lY, double lZ) = Geocentric.WGS84.Forward(pPosition.Latitude, pPosition.Longitude, pPosition.Altitude, Span<double>.Empty);
        return new GeocentricVector(lX, lY, lZ);
    }

    /// <summary>
    /// Converts a local attitude (heading, pitch, roll from north-east-down) into DIS Euler angles.
    /// </summary>
    /// <param name="pPosition">Position of the entity, which sets the local frame.</param>
    /// <param name="pOrientation">The local attitude.</param>
    /// <returns>The DIS orientation.</returns>
    public static DisOrientation ToDisOrientation(GeoPosition pPosition, Orientation pOrientation)
    {
        (GeocentricVector lNorth, GeocentricVector lEast, GeocentricVector lDown) = LocalFrame(pPosition);
        double lHeading = pOrientation.Heading * DegreesToRadians;
        double lPitch = pOrientation.Pitch * DegreesToRadians;
        double lRoll = pOrientation.Roll * DegreesToRadians;
        double lCosH = Math.Cos(lHeading), lSinH = Math.Sin(lHeading);
        double lCosP = Math.Cos(lPitch), lSinP = Math.Sin(lPitch);
        double lCosR = Math.Cos(lRoll), lSinR = Math.Sin(lRoll);

        // Body axes expressed in north-east-down, then in the geocentric frame.
        GeocentricVector lForward = Combine(lNorth, lEast, lDown, lCosP * lCosH, lCosP * lSinH, -lSinP);
        GeocentricVector lRight = Combine(
            lNorth, lEast, lDown,
            (lCosH * lSinP * lSinR) - (lSinH * lCosR),
            (lSinH * lSinP * lSinR) + (lCosH * lCosR),
            lCosP * lSinR);
        GeocentricVector lBelow = Combine(
            lNorth, lEast, lDown,
            (lCosH * lSinP * lCosR) + (lSinH * lSinR),
            (lSinH * lSinP * lCosR) - (lCosH * lSinR),
            lCosP * lCosR);

        double lPsi = Math.Atan2(lForward.Y, lForward.X);
        double lTheta = Math.Asin(Math.Clamp(-lForward.Z, -1, 1));
        double lPhi = Math.Atan2(lRight.Z, lBelow.Z);
        return new DisOrientation(lPsi, lTheta, lPhi);
    }

    /// <summary>
    /// Converts a speed along a heading and a pitch into a geocentric velocity.
    /// </summary>
    /// <param name="pKinematic">The kinematic state.</param>
    /// <returns>The velocity, in metres per second.</returns>
    public static GeocentricVector ToGeocentricVelocity(Kinematic pKinematic)
    {
        (GeocentricVector lNorth, GeocentricVector lEast, GeocentricVector lDown) = LocalFrame(pKinematic.Position);
        double lHeading = pKinematic.Orientation.Heading * DegreesToRadians;
        double lPitch = pKinematic.Orientation.Pitch * DegreesToRadians;
        double lSpeed = pKinematic.Speed;
        return Combine(
            lNorth, lEast, lDown,
            lSpeed * Math.Cos(lPitch) * Math.Cos(lHeading),
            lSpeed * Math.Cos(lPitch) * Math.Sin(lHeading),
            -lSpeed * Math.Sin(lPitch));
    }

    private static (GeocentricVector North, GeocentricVector East, GeocentricVector Down) LocalFrame(GeoPosition pPosition)
    {
        double lLatitude = pPosition.Latitude * DegreesToRadians;
        double lLongitude = pPosition.Longitude * DegreesToRadians;
        double lSinLat = Math.Sin(lLatitude), lCosLat = Math.Cos(lLatitude);
        double lSinLon = Math.Sin(lLongitude), lCosLon = Math.Cos(lLongitude);
        return (
            new GeocentricVector(-lSinLat * lCosLon, -lSinLat * lSinLon, lCosLat),
            new GeocentricVector(-lSinLon, lCosLon, 0),
            new GeocentricVector(-lCosLat * lCosLon, -lCosLat * lSinLon, -lSinLat));
    }

    private static GeocentricVector Combine(GeocentricVector pNorth, GeocentricVector pEast, GeocentricVector pDown, double pN, double pE, double pD)
    {
        return new GeocentricVector(
            (pN * pNorth.X) + (pE * pEast.X) + (pD * pDown.X),
            (pN * pNorth.Y) + (pE * pEast.Y) + (pD * pDown.Y),
            (pN * pNorth.Z) + (pE * pEast.Z) + (pD * pDown.Z));
    }
}
