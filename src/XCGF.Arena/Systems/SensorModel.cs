using XCGF.Arena.Components;
using XCGF.Arena.Data;

namespace XCGF.Arena.Systems;

/// <summary>
/// Behaviour of the Sensor definition of the XCGF.Data standard library, shared by every sensor (docs/models/sensor.md).
/// </summary>
/// <remarks>
/// A sensor is active while <c>Operational</c> is true. It detects a target inside its coverage: slant range within
/// [MinRange, MaxRange], target altitude within [MinAltitude, MaxAltitude], inside one of the DetectionArcs (all around
/// when there is none) and in a domain its Capabilities search (AirSearch, SurfaceSearch, LandSearch, PeriscopeSearch;
/// every domain when Capabilities is absent). A missing bound puts no limit. The specialisations add their physics in
/// <see cref="Senses"/>.
/// </remarks>
public class SensorModel : ISensorModel
{
    /// <summary>
    /// Equipment type of the root sensor definition.
    /// </summary>
    public const string SensorType = "Sensor";

    /// <summary>
    /// Name of the HLAboolean enumerator for true.
    /// </summary>
    public const string TrueName = "HLAtrue";

    /// <inheritdoc/>
    public virtual bool IsActive(EquipmentData pData)
    {
        return IsTrue(pData.Initialization.Get("Operational"), true);
    }

    /// <inheritdoc/>
    public bool Detects(EquipmentData pData, in SensorGeometry pGeometry)
    {
        return InCoverage(pData, pGeometry) && Senses(pData, pGeometry);
    }

    /// <summary>
    /// Tells whether a target lies in the geometric coverage of the sensor.
    /// </summary>
    /// <param name="pData">Data of the sensor equipment.</param>
    /// <param name="pGeometry">Geometry of the target as seen from the carrier.</param>
    /// <returns>True when the target is in range, altitude, arcs and searched domains.</returns>
    public static bool InCoverage(EquipmentData pData, in SensorGeometry pGeometry)
    {
        ParameterValues lTechnological = pData.Technological;
        if (pGeometry.Range < (lTechnological.GetNumber("MinRange") ?? double.NegativeInfinity)
            || pGeometry.Range > (lTechnological.GetNumber("MaxRange") ?? double.PositiveInfinity))
        {
            return false;
        }

        double lAltitude = pGeometry.TargetKinematic.Position.Altitude;
        if (lAltitude < (lTechnological.GetNumber("MinAltitude") ?? double.NegativeInfinity)
            || lAltitude > (lTechnological.GetNumber("MaxAltitude") ?? double.PositiveInfinity))
        {
            return false;
        }

        if (lTechnological.Get("DetectionArcs") is ArrayValue { Items.Count: > 0 } lArcs && !InArcs(lArcs, pGeometry))
        {
            return false;
        }

        return SearchesDomain(lTechnological.Get("Capabilities"), pGeometry);
    }

    /// <summary>
    /// Tells whether a target lies inside one of a list of SensorArc sectors, relative to the carrier heading and to
    /// its horizontal plane.
    /// </summary>
    /// <param name="pArcs">The SensorArcs value.</param>
    /// <param name="pGeometry">Geometry of the target as seen from the carrier.</param>
    /// <returns>True when one of the arcs contains the target.</returns>
    /// <remarks>
    /// An arc runs clockwise from AzimuthStart to AzimuthEnd, so 300 to 60 is the 120 degree front sector. A missing
    /// elevation bound puts no limit.
    /// </remarks>
    public static bool InArcs(ArrayValue pArcs, in SensorGeometry pGeometry)
    {
        double lAzimuth = GeoMath.NormalizeHeading(pGeometry.RelativeBearing);
        foreach (DataValue lItem in pArcs.Items)
        {
            if (lItem is not RecordValue lArc)
            {
                continue;
            }

            double lStart = GeoMath.NormalizeHeading(FieldOf(lArc, "AzimuthStart") ?? 0);
            double lEnd = GeoMath.NormalizeHeading(FieldOf(lArc, "AzimuthEnd") ?? 360);
            bool lInAzimuth = lStart <= lEnd ? lAzimuth >= lStart && lAzimuth <= lEnd : lAzimuth >= lStart || lAzimuth <= lEnd;
            if (lInAzimuth
                && pGeometry.Elevation >= (FieldOf(lArc, "ElevationMin") ?? -90)
                && pGeometry.Elevation <= (FieldOf(lArc, "ElevationMax") ?? 90))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads an HLAboolean value.
    /// </summary>
    /// <param name="pValue">The value, or null when unset.</param>
    /// <param name="pDefault">Result for an unset value.</param>
    /// <returns>True for HLAtrue.</returns>
    public static bool IsTrue(DataValue? pValue, bool pDefault)
    {
        return pValue switch
        {
            null => pDefault,
            EnumValue lEnum => lEnum.Name == TrueName,
            _ => pValue.AsNumber() is { } lNumber && lNumber != 0,
        };
    }

    /// <summary>
    /// Reads the names of the enumerators of an array of enumerated values, e.g. Capabilities or FrequencyBands.
    /// </summary>
    /// <param name="pValue">The array value, or null when unset.</param>
    /// <returns>The names; empty when unset.</returns>
    public static IReadOnlySet<string> NamesOf(DataValue? pValue)
    {
        HashSet<string> lNames = new HashSet<string>(StringComparer.Ordinal);
        if (pValue is ArrayValue lArray)
        {
            foreach (DataValue lItem in lArray.Items)
            {
                if (lItem is EnumValue lEnum)
                {
                    lNames.Add(lEnum.Name);
                }
            }
        }

        return lNames;
    }

    /// <summary>
    /// Physics of the specialisation, applied to the targets inside the coverage. The root sensor detects them all.
    /// </summary>
    /// <param name="pData">Data of the sensor equipment.</param>
    /// <param name="pGeometry">Geometry of the target as seen from the carrier.</param>
    /// <returns>True when the target is detected.</returns>
    protected virtual bool Senses(EquipmentData pData, in SensorGeometry pGeometry) => true;

    private static double? FieldOf(RecordValue pRecord, string pName)
    {
        return pRecord.Fields.TryGetValue(pName, out DataValue? lValue) ? lValue.AsNumber() : null;
    }

    private static bool SearchesDomain(DataValue? pCapabilities, in SensorGeometry pGeometry)
    {
        if (pCapabilities is null || !pGeometry.Target.TryGetComponent(out EntityIdentity lIdentity) || lIdentity.Environment is not { } lEnvironment)
        {
            return true;
        }

        string? lSearch = lEnvironment switch
        {
            EntityEnvironment.Air or EntityEnvironment.Space => "AirSearch",
            EntityEnvironment.Surface => "SurfaceSearch",
            EntityEnvironment.Land => "LandSearch",
            EntityEnvironment.Subsurface => "PeriscopeSearch",
            _ => null,
        };
        return lSearch is null || NamesOf(pCapabilities).Contains(lSearch);
    }
}
