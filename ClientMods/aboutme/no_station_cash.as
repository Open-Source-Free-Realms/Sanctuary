// ===== Sanctuary no Station Cash =====
// Appended to aboutme's main timeline by build_client_mods.py.
// Station Cash no longer exists, so the profile card leaves out its "I have N Station Cash" line.

var sanctuaryOriginalSetPlayerData = setPlayerData;
setPlayerData = function()
{
   sanctuaryOriginalSetPlayerData.apply(_root,arguments);
   totalStationCash._visible = false;
};
totalStationCash._visible = false;
// ===== End Sanctuary no Station Cash =====
