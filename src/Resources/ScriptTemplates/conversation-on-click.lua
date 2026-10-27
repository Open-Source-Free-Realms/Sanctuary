-- @name conversation-on-click
-- @description Turns to the player and opens this NPC's conversation (author it in the Talk tab)
-- @param node int 0

function onInteract(player)
    npc:face(player)

    if {{node}} > 0 then
        npc:startConversation(player, {{node}})
    else
        npc:startConversation(player)
    end
end
