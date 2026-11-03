using XCGF.Arena.Data;

namespace XCGF.Arena.Tests;

/// <summary>
/// Access to the example library of XCGF.Data.
/// </summary>
internal static class TestLibrary
{
    private static readonly Lazy<ArchetypeLibrary> sExample = new Lazy<ArchetypeLibrary>(() => XcgfDataLoader.Load(PathOf("library.xml")));

    public static ArchetypeLibrary Example => sExample.Value;

    public static string PathOf(string pFile) => Path.Combine(AppContext.BaseDirectory, "XCGF.Data", "examples", "library", pFile);

    public static ArchetypeLibrary FromXml(string pXml)
    {
        ArchetypeLibrary lLibrary = new ArchetypeLibrary();
        XcgfDataLoader.LoadInto(lLibrary, System.Xml.Linq.XDocument.Parse(pXml));
        return lLibrary;
    }

    public static DataValue Point(double pLatitude, double pLongitude) =>
        RecordValue.Of(("Latitude", new NumberValue(pLatitude)), ("Longitude", new NumberValue(pLongitude)));
}
