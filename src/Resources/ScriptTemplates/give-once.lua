-- @name give-once
-- @description Gives each player an item the first time they click (remembered until restart)
-- @param item item 0
-- @param quantity int 1
-- @param thanks string Here, take this!
-- @param already string I already gave you something.

local given = {}

function onInteract(player)
    npc:face(player)

    if given[player.guid] then
        player:message("{{already}}")
        return
    end

    given[player.guid] = true
    player:giveItem({{item}}, {{quantity}})
    npc:say("{{thanks}}")
end
