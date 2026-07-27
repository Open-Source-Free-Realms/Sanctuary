---@priority 1

local counter = 1

function onSecond(npc)
    counter = counter - 1
    if counter <= 0 then
        npc:sayLocalized(8130) -- "Welcome to Free Realms!"
        counter = math.random(10, 20)
    end
end