using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XCGF.Arena.Map.Blueprints;

/// <summary>
/// Brush of a pin or a link, from its name: "exec" or the name of a <see cref="XCGF.Arena.Blueprints.PinType"/>.
/// </summary>
public sealed class PinBrushes : IValueConverter
{
    /// <summary>
    /// The instance used by the styles.
    /// </summary>
    public static readonly PinBrushes Instance = new PinBrushes();

    private static readonly Dictionary<string, IBrush> sByName = new Dictionary<string, IBrush>
    {
        ["exec"] = new SolidColorBrush(Colors.White),
        ["Number"] = new SolidColorBrush(Color.Parse("#9BCB3C")),
        ["Bool"] = new SolidColorBrush(Color.Parse("#D0473B")),
        ["Entity"] = new SolidColorBrush(Color.Parse("#3BA3D9")),
        ["Time"] = new SolidColorBrush(Color.Parse("#E0A030")),
        ["Text"] = new SolidColorBrush(Color.Parse("#E26EB4")),
        ["Position"] = new SolidColorBrush(Color.Parse("#F2D14E")),
        ["Course"] = new SolidColorBrush(Color.Parse("#9B7BE0")),
    };

    private static readonly IBrush sDefault = new SolidColorBrush(Color.Parse("#A8AAB2"));

    /// <inheritdoc/>
    public object Convert(object? pValue, Type pTargetType, object? pParameter, CultureInfo pCulture)
        => pValue is string lName && sByName.TryGetValue(lName, out IBrush? lBrush) ? lBrush : sDefault;

    /// <inheritdoc/>
    public object ConvertBack(object? pValue, Type pTargetType, object? pParameter, CultureInfo pCulture)
        => throw new NotSupportedException();
}
