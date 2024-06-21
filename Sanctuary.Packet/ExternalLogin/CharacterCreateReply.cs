using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CharacterCreateReply : ISerializablePacket
{
    public const byte OpCode = 6;

    // 1 - Success
    // 2 - FailureUnableToCreateCharacter
    // 3 - FailureUnableToProcessRequest
    // 4 - FailureNameAlreadyTaken
    // 5 - FailureTemporaryNameAlreadyTaken
    // 6 - FailureTooManyCharactersOnAccount
    // 7 - FailureInvalidName
    // X - UnknownError

    public int Result;

    public ulong Guid;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Result);
        writer.Write(Guid);

        return writer.Buffer;
    }
}