using System.Diagnostics;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.UdpLibrary.Tests;

[TestClass]
public class BasicTest
{
    [TestMethod]
    public void ClientServerCommunication()
    {
        var protocolName = "Test";

        var serverParams = new UdpParams(ManagerRole.ExternalServer)
        {
            ProtocolName = protocolName,
            BindIpAddress = "127.0.0.1",
            Port = 12345
        };

        serverParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
        serverParams.UserSuppliedEncryptExpansionBytes = 1;

        var clientParams = new UdpParams(ManagerRole.ExternalClient)
        {
            ProtocolName = protocolName
        };

        clientParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
        clientParams.UserSuppliedEncryptExpansionBytes = 1;

        var serviceCollection = new ServiceCollection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var serverManager = new TestManager(true, serverParams, serviceProvider);
        var clientManager = new TestManager(false, clientParams, serviceProvider);

        var serverThread = new Thread(() => ServerLoop(serverManager));
        var clientThread = new Thread(() => ClientLoop(clientManager));

        serverThread.Start();
        clientThread.Start();

        while (serverThread.IsAlive || clientThread.IsAlive)
        {
        }
    }

    private void ServerLoop(TestManager manager)
    {
        while (true)
            manager.GiveTime();
    }

    private void ClientLoop(TestManager manager)
    {
        var connection = manager.EstablishConnection("127.0.0.1", 12345);

        if (connection is null)
            return;

        while (connection.Status == Status.Negotiating)
            manager.GiveTime();

        var count = 0ul;

        while (connection.Status != Status.Disconnected)
        {
            manager.GiveTime();

            connection.GetStats(out var stats);

            Debug.WriteLine("{0}\t{1}  AVE={2} HIGH={3} LOW={4} MSTR={5},{6} CRC={7} ORD={8}  {9}<<{10}  {11}>>{12}",
                count++,
                connection,
                stats.AveragePingTime,
                stats.HighPingTime,
                stats.LowPingTime,
                stats.MasterPingTime,
                stats.MasterPingAge,
                stats.CrcRejectedPackets,
                stats.OrderRejectedPackets,
                stats.SyncOurReceived,
                stats.SyncTheirSent,
                stats.SyncOurSent,
                stats.SyncTheirReceived);
        }
    }
}