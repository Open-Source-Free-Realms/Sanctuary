-- @name pinata
-- @description Players whack it with clicks; after enough hits it bursts and everyone who hit it gets a treat
-- @param hits int 10
-- @param item item 30589
-- @param respawn int 30

local health = {{hits}}
local hitters = {}
local brokenFor = -1

local whacks = { "Thwack!", "Bonk!", "Ow!", "Is that all you've got?", "I can feel the candy shifting!" }

function onSecond()
    if brokenFor < 0 then
        return
    end

    brokenFor = brokenFor + 1

    if brokenFor >= {{respawn}} then
        brokenFor = -1
        health = {{hits}}
        hitters = {}
        npc:playEffect(46) -- teleport flash
        npc:say("A fresh pinata appears! Who wants candy?")
    end
end

function onInteract(player)
    if brokenFor >= 0 then
        player:message("It's already smashed. A new one is coming!")
        return
    end

    hitters[player.guid] = player
    health = health - 1

    if health > 0 then
        npc:emote(3368) -- startled wobble
        npc:playEffect(906) -- pink hit flash
        npc:say(whacks[math.random(#whacks)] .. " (" .. tostring(health) .. " to go)")
        return
    end

    brokenFor = 0
    npc:playEffect(15093) -- gold coins burst
    npc:playEffect(5354) -- fireworks
    npc:say(player.name .. " smashed the pinata! Candy for everyone who helped!")

    for _, hitter in pairs(hitters) do
        if {{item}} > 0 then
            hitter:giveItem({{item}}, 1)
        end

        hitter:message("You helped smash the pinata!")
    end
end
