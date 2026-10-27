// ===== Sanctuary GM hub button =====
// Appended to gamedock's main timeline by build_client_mods.py.
// Adds a purple tree "Referee Tools" icon (gmTreeIcon, a recolored copy of the hub's treeIcon)
// right of the Marketplace icon. It sends "!gm"; for referees the server answers by toggling the
// GM panel in the chat window (ClientMods/chatWindowCtrl). Everyone else gets "no permission".

Object.registerClass("gmTreeIcon", GameDockIcon);

function gmCreateHubButton()
{
   var b = _root.attachMovie("gmTreeIcon", "gmHubButton", 9000);
   b.setTooltipStr("Referee Tools");

   // Line it up with the Marketplace icon, using on-stage bounds so it works with any registration point.
   // (No rescale: GameDockIcon's hover tween resets _xscale/_yscale.)
   var shop = shopButton.getBounds(_root);
   var own = b.getBounds(_root);
   b._x += shop.xMax + 8 - own.xMin;
   b._y += (shop.yMin + shop.yMax) / 2 - (own.yMin + own.yMax) / 2;

   b.onPress = function()
   {
      this.onRollOut();
      fscommand("FR_event", "PlaySound(DockSelect)");
      fscommand("FR_event", "sendChatMessage(  \"!gm\")");
   };
   b.onRelease = b.onReleaseOutside = function()
   {
   };
}

// Gold tree "Redeem Code" icon for every player, right of the Referee Tools icon. "!redeem" with no code
// opens the redeem box in the chat window.
Object.registerClass("gmRedeemIcon", GameDockIcon);

function gmCreateRedeemButton()
{
   var b = _root.attachMovie("gmRedeemIcon", "gmRedeemButton", 9001);
   b.setTooltipStr("Redeem Code");

   var hub = _root.gmHubButton.getBounds(_root);
   var own = b.getBounds(_root);
   b._x += hub.xMax + 8 - own.xMin;
   b._y += (hub.yMin + hub.yMax) / 2 - (own.yMin + own.yMax) / 2;

   b.onPress = function()
   {
      this.onRollOut();
      fscommand("FR_event", "PlaySound(DockSelect)");
      fscommand("FR_event", "sendChatMessage(  \"!redeem\")");
   };
   b.onRelease = b.onReleaseOutside = function()
   {
   };
}

// Lua shows and hides the hub icons through showDockIcons_lua; keep ours in step.
var gmOriginalShowDockIcons = showDockIcons_lua;
showDockIcons_lua = function(n)
{
   gmOriginalShowDockIcons.apply(_root, arguments);
   _root.gmHubButton._visible = n == 1;
   _root.gmRedeemButton._visible = n == 1;
};

gmCreateHubButton();
gmCreateRedeemButton();
// ===== End Sanctuary GM hub button =====
