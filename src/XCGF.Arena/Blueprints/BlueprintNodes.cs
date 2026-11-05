using Friflo.Engine.ECS;

namespace XCGF.Arena.Blueprints;

/// <summary>
/// Type of a data pin.
/// </summary>
public enum PinType
{
    /// <summary>A <see cref="double"/>.</summary>
    Number,

    /// <summary>A <see cref="bool"/>.</summary>
    Bool,

    /// <summary>An <see cref="Friflo.Engine.ECS.Entity"/>.</summary>
    Entity,

    /// <summary>A simulation time, as a <see cref="TimeSpan"/>.</summary>
    Time,

    /// <summary>A <see cref="string"/>.</summary>
    Text,
}

/// <summary>
/// Data pin of a node. Exec pins are implicit: an action has one exec input, and as many exec outputs as its
/// <see cref="ExecResult.Next"/> uses.
/// </summary>
/// <param name="Name">Name of the pin.</param>
/// <param name="Type">Type of the values it carries.</param>
public readonly record struct PinDef(string Name, PinType Type);

/// <summary>
/// Kind of node a graph can hold. A node definition is stateless and shared by every graph and fiber using it.
/// </summary>
public interface IBlueprintNode
{
    /// <summary>
    /// Gets the identifier of the node kind, e.g. Flow.Delay.
    /// </summary>
    string TypeId { get; }

    /// <summary>
    /// Gets the data inputs.
    /// </summary>
    PinDef[] Inputs { get; }

    /// <summary>
    /// Gets the data outputs.
    /// </summary>
    PinDef[] Outputs { get; }
}

/// <summary>
/// What a pure node sees: it reads the world but never changes it.
/// </summary>
public interface IReadContext
{
    /// <summary>
    /// Gets the store holding the entities.
    /// </summary>
    EntityStore Store { get; }

    /// <summary>
    /// Gets the entity the blueprint is attached to.
    /// </summary>
    Entity Self { get; }

    /// <summary>
    /// Gets the simulation time.
    /// </summary>
    TimeSpan SimTime { get; }
}

/// <summary>
/// What an action or an event sees: it can change the world.
/// </summary>
public interface IExecContext : IReadContext
{
    /// <summary>
    /// Gets the runtime running the blueprint.
    /// </summary>
    BlueprintRuntime Runtime { get; }

    /// <summary>
    /// Writes a line to the runtime log, prefixed by the entity.
    /// </summary>
    /// <param name="pMessage">The message.</param>
    void Log(string pMessage);
}

/// <summary>
/// Node that computes its outputs from its inputs, on demand, without exec pins and without changing the world.
/// </summary>
public interface IPureNode : IBlueprintNode
{
    /// <summary>
    /// Computes the outputs.
    /// </summary>
    /// <param name="pContext">The context.</param>
    /// <param name="pArgs">Access to the pins.</param>
    void Evaluate(IReadContext pContext, NodeArgs pArgs);
}

/// <summary>
/// What a fiber does after an action.
/// </summary>
public enum ExecKind
{
    /// <summary>Follows an exec output.</summary>
    Next,

    /// <summary>Sleeps until a simulation time, then resumes the same node.</summary>
    Wait,

    /// <summary>Ends the fiber.</summary>
    End,
}

/// <summary>
/// Result of an action.
/// </summary>
public readonly struct ExecResult
{
    private ExecResult(ExecKind pKind, int pExecPin, TimeSpan pWakeTime)
    {
        Kind = pKind;
        ExecPin = pExecPin;
        WakeTime = pWakeTime;
    }

    /// <summary>
    /// Gets what the fiber does.
    /// </summary>
    public ExecKind Kind { get; }

    /// <summary>
    /// Gets the exec output to follow, for <see cref="ExecKind.Next"/>.
    /// </summary>
    public int ExecPin { get; }

    /// <summary>
    /// Gets the simulation time to wake up at, for <see cref="ExecKind.Wait"/>.
    /// </summary>
    public TimeSpan WakeTime { get; }

    /// <summary>
    /// Follows an exec output.
    /// </summary>
    /// <param name="pExecPin">The exec output; 0 for the default one.</param>
    /// <returns>The result.</returns>
    public static ExecResult Next(int pExecPin = 0) => new ExecResult(ExecKind.Next, pExecPin, TimeSpan.Zero);

    /// <summary>
    /// Suspends the fiber until a simulation time; the node is then resumed (<see cref="IActionNode.Resume"/>).
    /// </summary>
    /// <param name="pSimTime">The simulation time.</param>
    /// <returns>The result.</returns>
    public static ExecResult WaitUntil(TimeSpan pSimTime) => new ExecResult(ExecKind.Wait, 0, pSimTime);

    /// <summary>
    /// Ends the fiber.
    /// </summary>
    /// <returns>The result.</returns>
    public static ExecResult End() => new ExecResult(ExecKind.End, 0, TimeSpan.Zero);
}

/// <summary>
/// Node run by a fiber through its exec input. It covers plain actions, latent ones (that wait) and flow control (that
/// choose an exec output).
/// </summary>
public interface IActionNode : IBlueprintNode
{
    /// <summary>
    /// Runs the node.
    /// </summary>
    /// <param name="pContext">The context.</param>
    /// <param name="pArgs">Access to the pins.</param>
    /// <returns>What the fiber does next.</returns>
    ExecResult Execute(IExecContext pContext, NodeArgs pArgs);

    /// <summary>
    /// Runs when a fiber this node suspended wakes up. By default, follows the exec output 0.
    /// </summary>
    /// <param name="pContext">The context.</param>
    /// <param name="pArgs">Access to the pins.</param>
    /// <returns>What the fiber does next.</returns>
    ExecResult Resume(IExecContext pContext, NodeArgs pArgs) => ExecResult.Next();
}

/// <summary>
/// What happens when an event fires while a fiber it started is still running.
/// </summary>
public enum RetriggerPolicy
{
    /// <summary>A new fiber runs alongside.</summary>
    Parallel,

    /// <summary>The firing is ignored.</summary>
    Ignore,

    /// <summary>The running fiber is ended and a new one starts.</summary>
    Restart,

    /// <summary>Only the first firing ever starts a fiber.</summary>
    Once,
}

/// <summary>
/// Identifies an event node of a graph attached to an entity.
/// </summary>
/// <param name="OwnerId">Id of the entity the graph is attached to.</param>
/// <param name="GraphId">Id of the graph in the runtime.</param>
/// <param name="EventNode">Id of the event node in the graph.</param>
public readonly record struct EventKey(int OwnerId, int GraphId, int EventNode);

/// <summary>
/// Entry point of a graph. It subscribes when the graph is attached, and starts a fiber at each firing.
/// </summary>
public interface IEventNode : IBlueprintNode
{
    /// <summary>
    /// Gets what happens on a firing while a fiber of this event is running.
    /// </summary>
    RetriggerPolicy Policy { get; }

    /// <summary>
    /// Subscribes to what fires the event, when the graph is attached to an entity.
    /// </summary>
    /// <param name="pContext">The context.</param>
    /// <param name="pParameters">Access to the inputs of the node.</param>
    /// <param name="pKey">The key to fire with <see cref="BlueprintRuntime.Trigger"/>.</param>
    void Subscribe(IExecContext pContext, NodeArgs pParameters, EventKey pKey);

    /// <summary>
    /// Writes the content of a firing to the outputs of the node.
    /// </summary>
    /// <param name="pPayload">The content, as given to <see cref="BlueprintRuntime.Trigger"/>.</param>
    /// <param name="pArgs">Access to the pins.</param>
    void WritePayload(object?[] pPayload, NodeArgs pArgs);
}
