using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public sealed class RatingDataEntry : ISerializableType
{
    public string CandidateId = string.Empty;
    public string OwnerName = string.Empty;
    public string Name = string.Empty;
    public ulong OwnerGuid;
    public string Snapshot = string.Empty;
    public string Description = string.Empty;
    public string Keywords = string.Empty;
    public float Rating;
    public float Votes;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(CandidateId);
        writer.Write(OwnerName);
        writer.Write(Name);
        writer.Write(OwnerGuid);
        writer.Write(Snapshot);
        writer.Write(Keywords);
        writer.Write(Description);
        writer.Write(Rating);
        writer.Write(Votes);
    }
}

public sealed class CandidateRatingInfo : ISerializableType
{
    public string CandidateId = string.Empty;
    public string OwnerName = string.Empty;
    public string Name = string.Empty;
    public float Rating;
    public int Votes;
    public bool HasRating;
    public bool CanVote;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(CandidateId);
        writer.Write(OwnerName);
        writer.Write(Name);
        writer.Write(Rating);
        writer.Write(Votes);
        writer.Write(HasRating);
        writer.Write(CanVote);
    }
}

public sealed class RatingPacketDataReply : BaseRatingPacket, ISerializablePacket
{
    public const byte SubOpCode = 5;

    public ulong Correlation;
    public string System = "Housing";
    public Dictionary<int, RatingDataEntry> Entries = [];
    public int TotalCount;

    public RatingPacketDataReply() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Correlation);
        writer.Write(System);
        writer.Write(Entries);
        writer.Write(TotalCount);
        return writer.Buffer;
    }
}

public sealed class RatingPacketSearchReply : BaseRatingPacket, ISerializablePacket
{
    public const byte SubOpCode = 13;

    public ulong Correlation;
    public string Query = string.Empty;
    public List<RatingDataEntry> Entries = [];

    public RatingPacketSearchReply() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Correlation);
        writer.Write(Query);
        writer.Write(Entries);
        return writer.Buffer;
    }
}

public sealed class RatingPacketCandidateInfoReply : BaseRatingPacket, ISerializablePacket
{
    public const byte SubOpCode = 17;

    public ulong Correlation;
    public List<CandidateRatingInfo> Candidates = [];

    public RatingPacketCandidateInfoReply() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Correlation);
        writer.Write(Candidates);
        return writer.Buffer;
    }
}

public sealed class RatingPacketVoteReply : BaseRatingPacket, ISerializablePacket
{
    public const byte SubOpCode = 18;

    public RatingPacketVoteReply() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        return writer.Buffer;
    }
}

public sealed class RatingPacketSendFeatured : BaseRatingPacket, ISerializablePacket
{
    public const byte SubOpCode = 22;

    public ulong Correlation;
    public string System = "Housing";
    public RatingDataEntry Entry = new();

    public RatingPacketSendFeatured() : base(SubOpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Correlation);
        writer.Write(System);
        Entry.Serialize(writer);
        return writer.Buffer;
    }
}
