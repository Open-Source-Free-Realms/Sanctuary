-- @name firework-launcher
-- @description Click to light the fuse: counts down, then fireworks for everyone nearby (good on a prop)
-- @param countdown int 5
-- @param cooldown int 30

local fuse = 0
local readyAt = 0
local clock = 0
local lighter = nil

function onSecond()
    clock = clock + 1

    if fuse <= 0 then
        return
    end

    fuse = fuse - 1

    if fuse > 0 then
        npc:say(tostring(fuse) .. "...")
        return
    end

    npc:playEffect(5349) -- large multicoloured fireworks
    npc:playEffect(5354)

    local position = npc.position
    npc.zone:broadcastNear(lighter .. " set off the fireworks! Ooooh!", position.x, position.z, 60)
    readyAt = clock + {{cooldown}}
end

function onInteract(player)
    if fuse > 0 then
        player:message("The fuse is already lit. Stand back!")
        return
    end

    if clock < readyAt then
        player:message("Still reloading: " .. tostring(readyAt - clock) .. " seconds.")
        return
    end

    lighter = player.name
    fuse = {{countdown}}
    npc:playEffect(17) -- puff of smoke
    npc:say(player.name .. " lit the fuse! " .. tostring(fuse) .. "...")
end
