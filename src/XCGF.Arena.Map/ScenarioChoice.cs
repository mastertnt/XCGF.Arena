using XCGF.Arena.Scenarios;

namespace XCGF.Arena.Map;

/// <summary>
/// Scenario offered by the map host.
/// </summary>
/// <param name="Key">Key of the scenario on the command line (--scenario).</param>
/// <param name="Label">Label shown in the scenario list.</param>
/// <param name="Create">Creates a fresh instance of the scenario.</param>
public sealed record ScenarioChoice(string Key, string Label, Func<IScenario> Create)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
}
