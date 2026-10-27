-- @name quiz-master
-- @description Runs a quiz you write in the Talk tab: give right answers the hook "right" and wrong ones "wrong"
-- @param streak int 3
-- @param item item 16370

local streaks = {}

function onInteract(player)
    npc:face(player)
    npc:startConversation(player)
end

function onChoice(player, hook)
    local current = streaks[player.guid] or 0

    if hook == "right" then
        current = current + 1
        npc:emote(3306) -- cheer
        npc:playEffect(15165) -- confetti pop
        npc:say("Correct! That's " .. tostring(current) .. " in a row, " .. player.name .. "!")

        if current >= {{streak}} then
            current = 0
            npc:playEffect(5354)
            npc:say("A perfect streak! Here's your prize!")

            if {{item}} > 0 then
                player:giveItem({{item}}, 1)
            end
        end
    elseif hook == "wrong" then
        current = 0
        npc:emote(3316) -- laugh
        npc:playEffect(15141) -- confused question marks
        npc:say("Ha! Not even close. Streak reset!")
    end

    streaks[player.guid] = current
end
