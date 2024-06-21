using Sanctuary.Core.IO;

namespace Sanctuary.Game.Entities;

public interface IEntitySession
{
    void Send(ISerializablePacket packet);
    void SendToVisible(ISerializablePacket packet, bool sendToSelf = false);

    void SendTunneled(ISerializablePacket packet);
    void SendTunneledToVisible(ISerializablePacket packet, bool sendToSelf = false);
}