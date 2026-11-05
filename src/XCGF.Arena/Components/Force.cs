using Friflo.Engine.ECS;

namespace XCGF.Arena.Components;

/// <summary>
/// Side an entity fights for. Set by the scenario, since archetypes do not carry it.
/// </summary>
public enum ForceSide
{
    /// <summary>Side not set.</summary>
    Other = 0,

    /// <summary>Friendly force.</summary>
    Friendly = 1,

    /// <summary>Opposing force.</summary>
    Opposing = 2,

    /// <summary>Neutral force.</summary>
    Neutral = 3,
}

/// <summary>
/// Side and callsign of an entity in a scenario.
/// </summary>
/// <param name="Side">The side.</param>
/// <param name="Callsign">Name of the entity in the scenario, used as DIS marking.</param>
public record struct Force(ForceSide Side, string Callsign) : IComponent;
