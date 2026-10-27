-- @name heckler
-- @description Greets each player who walks by with a random compliment or a (gentle) roast
-- @param cooldown int 60

local compliments = {
    { "%s! Looking sharp today!", 3302 },
    { "Is that %s? I love what you've done with your hat.", 3306 },
    { "Make way, make way! %s is here!", 3344 },
    { "%s, you walk like a hero. A slightly lost hero.", 3331 }
}

local roasts = {
    { "Oh look, it's %s. Did you get dressed in the dark?", 3316 },
    { "%s, I've seen faster snails. Friendlier ones too.", 3330 },
    { "Hey %s! Your shoelace is untied. Made you look!", 3319 },
    { "%s again? Don't you have a quest to do?", 3326 }
}

local lastSeen = {}
local clock = 0

function onSecond()
    clock = clock + 1
end

function onProximity(player)
    if lastSeen[player.guid] and clock - lastSeen[player.guid] < {{cooldown}} then
        return
    end

    lastSeen[player.guid] = clock

    local pool = math.random(2) == 1 and compliments or roasts
    local line = pool[math.random(#pool)]

    npc:face(player)
    npc:emote(line[2])
    npc:say(string.format(line[1], player.name))
end

function onInteract(player)
    npc:face(player)
    npc:emote(3348) -- wink
    npc:say("I'm only teasing, " .. player.name .. ". Mostly.")
end
