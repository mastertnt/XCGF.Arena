using System.Net;
using System.Net.Sockets;
using OpenDisNet;
using OpenDisNet.Enumerations;
using OpenDisNet.Pdus;
using XCGF.Arena.Dis;

namespace XCGF.Arena.Tests;

public class EntityStatePduEncoderTests
{
    private static readonly DisEntityState sState = new DisEntityState(
        new DisEntityId(12, 34, 56),
        2,
        new DisEntityType(1, 2, 71, 1, 0, 0, 0),
        new GeocentricVector(4276603.5, 149342.3, 4724496.1),
        new GeocentricVector(10, -250, 5),
        new DisOrientation(1.5, -0.25, 0.5),
        "RED1",
        TimeSpan.FromSeconds(1800));

    [Fact]
    public void Encode_ProducesAnEntityStatePduOpenDisNetReadsBack()
    {
        byte[] lDatagram = EntityStatePduEncoder.Encode(sState, 7);

        Assert.True(DisSerializer.TryDeserialize(lDatagram, out EntityStatePdu? lPdu, out DisParseError lError), lError.Message);
        Assert.Equal(7, lPdu!.ExerciseId);
        Assert.Equal((12, 34, 56), (lPdu.EntityId.SiteId, lPdu.EntityId.ApplicationId, lPdu.EntityId.EntityNumber));
        Assert.Equal(ForceId.Opposing, lPdu.ForceId);
        Assert.Equal((EntityKind)1, lPdu.EntityType.EntityKind);
        Assert.Equal(2, lPdu.EntityType.Domain.Value);
        Assert.Equal((Country)71, lPdu.EntityType.Country);
        Assert.Equal(1, lPdu.EntityType.Category);
        Assert.Equal((4276603.5, 149342.3, 4724496.1), (lPdu.EntityLocation.X, lPdu.EntityLocation.Y, lPdu.EntityLocation.Z));
        Assert.Equal((10f, -250f, 5f), (lPdu.EntityLinearVelocity.X, lPdu.EntityLinearVelocity.Y, lPdu.EntityLinearVelocity.Z));
        Assert.Equal((1.5f, -0.25f, 0.5f), (lPdu.EntityOrientation.Psi, lPdu.EntityOrientation.Theta, lPdu.EntityOrientation.Phi));
        Assert.Equal(DeadReckoningAlgorithm.DrmFpwConstantVelocityLowAccelerationLinearMotionEntity, lPdu.DeadReckoningParameters.DeadReckoningAlgorithm);
        Assert.Equal("RED1", System.Text.Encoding.ASCII.GetString(lPdu.Marking.Characters).TrimEnd('\0'));
        Assert.Equal(1u << 31, lPdu.Timestamp);
        Assert.Equal(144, lDatagram.Length);
    }

    [Fact]
    public void ToRelativeTimestamp_CountsFromTheHourWithLowBitCleared()
    {
        Assert.Equal(0u, EntityStatePduEncoder.ToRelativeTimestamp(TimeSpan.Zero));
        Assert.Equal(0u, EntityStatePduEncoder.ToRelativeTimestamp(TimeSpan.FromHours(1)));
        Assert.Equal(1u << 31, EntityStatePduEncoder.ToRelativeTimestamp(TimeSpan.FromMinutes(90)));
        Assert.Equal(0u, EntityStatePduEncoder.ToRelativeTimestamp(TimeSpan.FromSeconds(1234.5)) & 1u);
    }

    [Fact]
    public async Task UdpDisPublisher_SendsOneDatagramPerState()
    {
        using UdpClient lReceiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        IPEndPoint lEndPoint = (IPEndPoint)lReceiver.Client.LocalEndPoint!;
        using UdpDisPublisher lPublisher = new UdpDisPublisher(lEndPoint, 3);

        lPublisher.Publish(sState);
        UdpReceiveResult lResult = await lReceiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, lPublisher.SentCount);
        Assert.True(DisSerializer.TryDeserialize(lResult.Buffer, out EntityStatePdu? lPdu, out _));
        Assert.Equal(3, lPdu!.ExerciseId);
        Assert.Equal(56, lPdu.EntityId.EntityNumber);
    }
}
