// ===== Sanctuary coin store tabs =====
// Appended to navigation's main timeline by build_client_mods.py.
// The Marketplace window's tab bar comes from Lua (marketplace.lua): Marketplace, Coin Shop,
// Membership and Claim. Station Cash and membership no longer exist, so only Coin Shop is added.
// The tabs are told apart by their icons, since this bar is shared with other windows.

// Marketplace (Station Cash store), Membership and Claim tab icons.
var sanctuaryRemovedTabIcons = [798, 703, 28546];

var sanctuaryOriginalAddButton = AddButton;

AddButton = function(parent, iconID, name, id)
{
   var i = 0;
   while(i < sanctuaryRemovedTabIcons.length)
   {
      if(Number(iconID) == sanctuaryRemovedTabIcons[i])
      {
         return undefined;
      }
      i = i + 1;
   }
   sanctuaryOriginalAddButton(parent,iconID,name,id);
};
// ===== End Sanctuary coin store tabs =====
