using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class NpcInteractPromptTests
{
    private static Npc CreateNpc() => new(null!) { Name = "Test NPC" };

    [TestMethod]
    public void NothingToDo_NoPrompt()
    {
        var npc = CreateNpc();

        Assert.IsFalse(npc.ShowsInteractPrompt);
    }

    [TestMethod]
    public void NamedWithInteraction_Prompt()
    {
        var npc = CreateNpc();
        npc.Interactions.Add(OpenAtlasInteraction.Data);

        Assert.IsTrue(npc.ShowsInteractPrompt);
    }

    [TestMethod]
    public void HiddenNameplate_NoPrompt()
    {
        var npc = CreateNpc();
        npc.HideNamePlate = true;
        npc.InteractAction = _ => { };

        Assert.IsFalse(npc.ShowsInteractPrompt);
    }

    [TestMethod]
    public void HiddenNameplateWithOwnCursor_Prompt()
    {
        // e.g. the house exit door.
        var npc = CreateNpc();
        npc.HideNamePlate = true;
        npc.CursorId = 17;
        npc.Interactions.Add(LeaveHouseInteraction.Data);

        Assert.IsTrue(npc.ShowsInteractPrompt);
    }

    [TestMethod]
    public void NotInteractable_NoPrompt()
    {
        var npc = CreateNpc();
        npc.IsInteractable = false;
        npc.InteractAction = _ => { };

        Assert.IsFalse(npc.ShowsInteractPrompt);
    }
}
