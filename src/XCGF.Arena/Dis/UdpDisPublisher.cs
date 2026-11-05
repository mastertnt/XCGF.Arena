using System.Net;
using System.Net.Sockets;

namespace XCGF.Arena.Dis;

/// <summary>
/// Sends entity states as DIS 7 Entity State PDUs in UDP datagrams.
/// </summary>
public sealed class UdpDisPublisher : IDisPublisher, IDisposable
{
    /// <summary>
    /// Usual DIS port.
    /// </summary>
    public const int DefaultPort = 3000;

    private readonly UdpClient mClient;
    private readonly IPEndPoint mDestination;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpDisPublisher"/> class.
    /// </summary>
    /// <param name="pDestination">Address and port to send to; a broadcast address enables broadcast.</param>
    /// <param name="pExerciseId">DIS exercise identifier.</param>
    public UdpDisPublisher(IPEndPoint pDestination, byte pExerciseId = 1)
    {
        ArgumentNullException.ThrowIfNull(pDestination);
        mDestination = pDestination;
        ExerciseId = pExerciseId;
        mClient = new UdpClient(pDestination.AddressFamily) { EnableBroadcast = pDestination.Address.Equals(IPAddress.Broadcast) };
    }

    /// <summary>
    /// Gets the DIS exercise identifier.
    /// </summary>
    public byte ExerciseId { get; }

    /// <summary>
    /// Gets the number of PDUs sent.
    /// </summary>
    public long SentCount { get; private set; }

    /// <inheritdoc/>
    public void Publish(DisEntityState pState)
    {
        byte[] lDatagram = EntityStatePduEncoder.Encode(pState, ExerciseId);
        mClient.Send(lDatagram, lDatagram.Length, mDestination);
        SentCount++;
    }

    /// <inheritdoc/>
    public void Dispose() => mClient.Dispose();
}
