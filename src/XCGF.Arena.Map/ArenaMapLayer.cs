using Friflo.Engine.ECS;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using XCGF.Arena.Components;

namespace XCGF.Arena.Map;

/// <summary>
/// Map layer showing the entities of a simulation: a symbol coloured by side, pointing to the heading, and the callsign.
/// </summary>
public sealed class ArenaMapLayer
{
    private const string UserAgent = "XCGF.Arena.Map";
    private const double ZoomMargin = 0.5;

    private readonly MemoryLayer mLayer = new MemoryLayer("Entities") { Style = null };

    /// <summary>
    /// Creates a map with an OpenStreetMap background and this layer on top.
    /// </summary>
    /// <returns>The map.</returns>
    public Mapsui.Map CreateMap()
    {
        Mapsui.Map lMap = new Mapsui.Map();
        lMap.Layers.Add(OpenStreetMap.CreateTileLayer(UserAgent));
        lMap.Layers.Add(mLayer);
        return lMap;
    }

    /// <summary>
    /// Shows the entities of the simulation where they are now.
    /// </summary>
    /// <param name="pSimulation">The simulation.</param>
    public void Update(ArenaSimulation pSimulation)
    {
        ArgumentNullException.ThrowIfNull(pSimulation);
        List<IFeature> lFeatures = new List<IFeature>();
        foreach (Entity lEntity in pSimulation.Store.Query<Kinematic>().Entities)
        {
            Kinematic lKinematic = lEntity.GetComponent<Kinematic>();
            (double lX, double lY) = SphericalMercator.FromLonLat(lKinematic.Position.Longitude, lKinematic.Position.Latitude);
            PointFeature lFeature = new PointFeature(lX, lY);
            ForceSide lSide = lEntity.TryGetComponent(out Force lForce) ? lForce.Side : ForceSide.Other;
            lFeature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Triangle,
                SymbolScale = 0.6,
                SymbolRotation = lKinematic.Orientation.Heading,
                Fill = new Brush(ColorOf(lSide)),
                Outline = new Pen(Color.Black, 1),
            });
            lFeature.Styles.Add(new LabelStyle
            {
                Text = lForce.Callsign ?? $"#{lEntity.Id}",
                Offset = new Offset(0, 22),
                BackColor = new Brush(Color.White),
            });
            lFeatures.Add(lFeature);
        }

        mLayer.Features = lFeatures;
        mLayer.DataHasChanged();
    }

    /// <summary>
    /// Centres the map on the entities shown.
    /// </summary>
    /// <param name="pMap">The map holding this layer.</param>
    public void ZoomToEntities(Mapsui.Map pMap)
    {
        ArgumentNullException.ThrowIfNull(pMap);
        if (mLayer.Extent is MRect lExtent)
        {
            pMap.Navigator.ZoomToBox(lExtent.Grow(Math.Max(lExtent.Width, lExtent.Height) * ZoomMargin));
        }
    }

    private static Color ColorOf(ForceSide pSide)
    {
        return pSide switch
        {
            ForceSide.Friendly => Color.FromArgb(255, 30, 100, 220),
            ForceSide.Opposing => Color.FromArgb(255, 220, 40, 40),
            ForceSide.Neutral => Color.FromArgb(255, 40, 170, 60),
            _ => Color.FromArgb(255, 230, 200, 30),
        };
    }
}
