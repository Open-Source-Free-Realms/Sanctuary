-- @name fortune-teller
-- @description Ponders, then reads the clicker a random fortune with a burst of stars
-- @param cooldown int 5

local fortunes = {
    "A great treasure awaits you... probably under a rock.",
    "You will soon meet a tall stranger. Or a short one. Strangers vary.",
    "Beware of chickens bearing gifts.",
    "Your next jump will be your best jump.",
    "The stars say: buy more cupcakes.",
    "Someone nearby is thinking about lunch. It is me.",
    "Fortune favours the bold, and also the well snacked.",
    "You will find what you seek where you left it.",
    "A pixie owes you three coins. Collect with caution.",
    "Today is a good day to dance badly in public."
}

local lastRead = {}
local clock = 0

function onSecond()
    clock = clock + 1
end

function onInteract(player)
    npc:face(player)

    if lastRead[player.guid] and clock - lastRead[player.guid] < {{cooldown}} then
        npc:say("Patience, " .. player.name .. ". The spirits are catching their breath.")
        return
    end

    lastRead[player.guid] = clock
    npc:emote(3320) -- ponder
    npc:playEffect(4010) -- gold stars bursting from the head
    npc:say(player.name .. ", " .. fortunes[math.random(#fortunes)])
end
