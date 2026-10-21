using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryRecipeInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Recipe;

    public int RecipeId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(RecipeId);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out RecipeId))
            return false;
        return true;
    }
}
