local GreetOnApproach = {}

function GreetOnApproach.create(radius, cooldownSeconds, onGreet)
    local seconds = 0
    local lastGreetedAt = {}
    local nearby = {}

    return function(npc)
        seconds = seconds + 1

        local nowNearby = {}
        for _, playerGuid in ipairs(npc:getNearbyPlayers(radius)) do
            nowNearby[playerGuid] = true

            local greetedAt = lastGreetedAt[playerGuid]
            if not nearby[playerGuid] and (greetedAt == nil or seconds - greetedAt >= cooldownSeconds) then
                onGreet(npc)
                lastGreetedAt[playerGuid] = seconds
            end
        end

        nearby = nowNearby
    end
end

return GreetOnApproach
