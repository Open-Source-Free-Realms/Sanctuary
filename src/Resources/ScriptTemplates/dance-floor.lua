-- @name dance-floor
-- @description Starts a confetti dance party while players are nearby and winds down when they leave
-- @param radius float 12
-- @param linger int 10

local dances = { 3201, 3211, 3212, 3213, 3214, 3501, 3502, 3503, 3504, 3505 }
local cheers = { "Everybody dance!", "Show me your moves!", "Now spin!", "This is my jam!", "Left foot, right foot!" }

local partying = false
local confetti = nil
local notes = nil
local emptyFor = 0
local clock = 0

local function startParty()
    partying = true
    confetti = npc:addEffectTag(5723) -- confetti falling
    notes = npc:addEffectTag(15113) -- rainbow musical notes
    npc:say("Party time!")
end

local function stopParty()
    partying = false

    if confetti then npc:removeEffectTag(confetti) end
    if notes then npc:removeEffectTag(notes) end

    confetti = nil
    notes = nil
    npc:emote(3360) -- bye bye
    npc:say("Party's over... for now.")
end

function onSecond()
    clock = clock + 1

    local dancers = #npc:getNearbyPlayers({{radius}})

    if dancers > 0 then
        emptyFor = 0

        if not partying then
            startParty()
        end
    elseif partying then
        emptyFor = emptyFor + 1

        if emptyFor >= {{linger}} then
            stopParty()
        end
    end

    if partying and clock % 4 == 0 then
        npc:emote(dances[math.random(#dances)])

        if math.random(3) == 1 then
            npc:say(cheers[math.random(#cheers)])
        end
    end
end
