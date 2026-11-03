using System.Globalization;
using Friflo.Engine.ECS;
using XCGF.Arena.Components;

namespace XCGF.Arena.Scenarios;

/// <summary>
/// Writes the notable changes of a simulation as text lines: task transitions and new or lost radar contacts.
/// </summary>
public sealed class ArenaJournal
{
    private readonly TextWriter mOutput;
    private readonly Dictionary<TaskInstance, TaskState> mTaskStates = new Dictionary<TaskInstance, TaskState>();
    private readonly Dictionary<Entity, HashSet<Entity>> mContacts = new Dictionary<Entity, HashSet<Entity>>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ArenaJournal"/> class.
    /// </summary>
    /// <param name="pOutput">The writer that receives the lines.</param>
    public ArenaJournal(TextWriter pOutput)
    {
        ArgumentNullException.ThrowIfNull(pOutput);
        mOutput = pOutput;
    }

    /// <summary>
    /// Writes what changed since the previous call.
    /// </summary>
    /// <param name="pArena">The simulation.</param>
    public void Record(ArenaSimulation pArena)
    {
        ArgumentNullException.ThrowIfNull(pArena);
        foreach (Entity lCarrier in pArena.Store.Query<AssignedTasks>().Entities.ToArray())
        {
            foreach (TaskInstance lTask in lCarrier.GetComponent<AssignedTasks>().Items)
            {
                if (mTaskStates.TryGetValue(lTask, out TaskState lPrevious) && lPrevious == lTask.State)
                {
                    continue;
                }

                mTaskStates[lTask] = lTask.State;
                string lEvent = lTask.State switch
                {
                    TaskState.Pending => "assigned",
                    TaskState.Running => "started",
                    _ => $"ended ({lTask.Outcome})",
                };
                Write(pArena, $"{NameOf(lCarrier)} {lTask.Definition.Name} {lEvent}");
            }
        }

        foreach (Entity lSensor in pArena.Store.Query<SensorDetections>().Entities.ToArray())
        {
            HashSet<Entity> lNow = lSensor.GetComponent<SensorDetections>().Items.Select(pDetection => pDetection.Target).ToHashSet();
            HashSet<Entity> lBefore = mContacts.GetValueOrDefault(lSensor) ?? new HashSet<Entity>();
            string lSensorName = $"{NameOf(lSensor.Parent)}.{lSensor.GetComponent<Equipment>().Name}";
            foreach (Entity lTarget in lNow.Except(lBefore))
            {
                Write(pArena, $"{lSensorName} detects {NameOf(lTarget)}");
            }

            foreach (Entity lTarget in lBefore.Except(lNow))
            {
                Write(pArena, $"{lSensorName} loses {NameOf(lTarget)}");
            }

            mContacts[lSensor] = lNow;
        }
    }

    private static string NameOf(Entity pEntity)
    {
        if (pEntity.TryGetComponent(out Force lForce) && lForce.Callsign is not null)
        {
            return lForce.Callsign;
        }

        if (pEntity.TryGetComponent(out EntityIdentity lIdentity))
        {
            return $"{lIdentity.Archetype}#{pEntity.Id}";
        }

        return pEntity.TryGetComponent(out Unit lUnit) ? $"{lUnit.Archetype}#{pEntity.Id}" : $"#{pEntity.Id}";
    }

    private void Write(ArenaSimulation pArena, string pText)
    {
        mOutput.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[{pArena.Elapsed:hh\\:mm\\:ss}] {pText}"));
    }
}
