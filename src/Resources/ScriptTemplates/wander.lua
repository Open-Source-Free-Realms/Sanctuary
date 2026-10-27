-- @name wander
-- @description Walks to random spots around where the script started
-- @param radius float 10
-- @param interval int 6

local home = nil
local clock = 0

function onSpawn()
    home = npc.position
end

function onSecond()
    clock = clock + 1

    if home == nil or clock % {{interval}} ~= 0 then
        return
    end

    local angle = math.random() * math.pi * 2
    local distance = math.random() * {{radius}}

    npc:moveTo(home.x + math.cos(angle) * distance, home.y, home.z + math.sin(angle) * distance)
end
