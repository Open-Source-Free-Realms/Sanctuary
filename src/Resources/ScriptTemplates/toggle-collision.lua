-- @name toggle-collision
-- @description Clicking switches whether players collide with it (e.g. a door or gate)
-- @param open string The way is open.
-- @param closed string The way is blocked.

local solid = true

function onInteract(player)
    solid = not solid
    npc:setCollidable(solid)

    if solid then
        player:message("{{closed}}")
    else
        player:message("{{open}}")
    end
end
