-- @name proximity-greeter
-- @description Says a line when a player walks up (once per approach, with a cooldown)
-- @param text string Welcome, traveller!
-- @param cooldown int 30

local lastSaid = -1000
local clock = 0

function onSecond()
    clock = clock + 1
end

function onProximity(player)
    if clock - lastSaid < {{cooldown}} then
        return
    end

    lastSaid = clock
    npc:face(player)
    npc:say("{{text}}")
end
