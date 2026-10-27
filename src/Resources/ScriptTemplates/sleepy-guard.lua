-- @name sleepy-guard
-- @description Dozes on duty, jolts awake when someone sneaks up, and nods off again when it's quiet
-- @param doze int 20

local awake = false
local quietFor = 0
local clock = 0

local snores = { "Zzz...", "*snore*", "Five more minutes...", "Mmm... cupcakes...", "Zzz... I'm awake... zzz" }

local function fallAsleep()
    awake = false
    npc:emote(3334) -- yawn
    npc:say("*yawn* Nothing to see here...")
end

function onSpawn()
    fallAsleep()
end

function onSecond()
    clock = clock + 1

    if awake then
        if #npc:getNearbyPlayers(10) == 0 then
            quietFor = quietFor + 1
        else
            quietFor = 0
        end

        if quietFor >= {{doze}} then
            fallAsleep()
        end
        return
    end

    if clock % 8 == 0 then
        npc:emote(3329) -- sleep standing
        npc:say(snores[math.random(#snores)])
    end
end

function onProximity(player)
    if awake then
        return
    end

    awake = true
    quietFor = 0
    npc:face(player)
    npc:emote(3368) -- startled
    npc:say("Wha-?! Who goes there?! Oh. It's you, " .. player.name .. ".")
end

function onInteract(player)
    npc:face(player)

    if not awake then
        npc:say("*snore* ...the password is... zzz...")
        return
    end

    npc:emote(3344) -- salute
    npc:say("All quiet on my watch, " .. player.name .. "! Totally wasn't sleeping.")
end
