-- @name quest-giver
-- @description Offers a quest on click, or a line once it's done (custom quests are 900000+)
-- @param quest int 900001
-- @param offer string I have a task for you!
-- @param done string Thanks again for your help.

function onInteract(player)
    npc:face(player)

    if player:hasCompletedQuest({{quest}}) then
        npc:say("{{done}}")
        return
    end

    npc:say("{{offer}}")
    player:acceptQuest({{quest}})
end
