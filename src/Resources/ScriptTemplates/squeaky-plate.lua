-- @name squeaky-plate
-- @description A prop that reacts when players stand on it; every Nth step sets off fireworks
-- @param radius float 2
-- @param height float 3
-- @param every int 10

local standing = {}
local steps = 0

local reactions = { "Squeak!", "Oof!", "Boing!", "Hey, that tickles!", "*creak*" }

function onSecond()
    local now = {}

    for _, guid in ipairs(npc:getPlayersOnTop({{radius}}, -0.5, {{height}})) do
        now[guid] = true

        if not standing[guid] then
            steps = steps + 1

            if steps % {{every}} == 0 then
                npc:playEffect(5354)
                local position = npc.position
                npc.zone:broadcastNear("Step number " .. tostring(steps) .. "! The plate erupts in fireworks!", position.x, position.z, 40)
            else
                npc:playEffect(15118) -- small level-up sparkle
                npc:say(reactions[math.random(#reactions)])
            end
        end
    end

    standing = now
end
