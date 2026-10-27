-- @name weather-totem
-- @description Each click changes the weather around it: snow, butterflies, fireflies, rainbow, clear
-- @param cooldown int 3

local weathers = {
    { name = "Let it snow!", effect = 4015 },
    { name = "Butterfly season!", effect = 5202 },
    { name = "The fireflies come out to play.", effect = 5233 },
    { name = "A rainbow! Somebody make a wish.", effect = 5522 },
    { name = "Confetti storm!", effect = 5723 },
    { name = "The sky clears.", effect = 0 }
}

local index = #weathers
local tag = nil
local clock = 0
local changedAt = -1000

function onSecond()
    clock = clock + 1
end

function onInteract(player)
    if clock - changedAt < {{cooldown}} then
        player:message("The totem is still humming...")
        return
    end

    changedAt = clock
    index = index % #weathers + 1

    if tag then
        npc:removeEffectTag(tag)
        tag = nil
    end

    local weather = weathers[index]

    if weather.effect > 0 then
        tag = npc:addEffectTag(weather.effect)
    end

    npc:playEffect(40) -- blue power-up burst
    npc:say(player.name .. " shakes the totem. " .. weather.name)
end
