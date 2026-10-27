-- @name greeter
-- @description Turns to the player and says a line when clicked
-- @param text string Hello there, adventurer!

function onInteract(player)
    npc:face(player)
    npc:say("{{text}}")
end
