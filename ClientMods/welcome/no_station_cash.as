// ===== Sanctuary welcome screen =====
// Appended to welcome's main timeline by build_client_mods.py.
// Station Cash and membership no longer exist:
//  - the header's wallet shows coins (its SC icon is swapped for the coin in build_client_mods.py),
//  - the Popular Items panel (Station Cash store picks) and the Monthly Member Bonus ad give way to
//    Chatty, who pops up there and opens the coin shop,
//  - the Member / Upgrade badge is gone.

function sanctuaryHideStationCash()
{
   browser.wallet_mc.sc_amount_txt._visible = false;
   browser.wallet_mc.icon_sc_mc._visible = false;
   browser.sc_mc._visible = false;
}

// The header's amount next to the (now coin) icon shows the player's coins.
setStationCashAmount = function(n)
{
   sanctuaryHideStationCash();
};

var sanctuaryOriginalSetCoinAmount = setCoinAmount;
setCoinAmount = function(n)
{
   sanctuaryOriginalSetCoinAmount(n);
   _root.frSetTextPreserveWidthV3(browser.sc_amount_txt,String(n),true);
   sanctuaryHideStationCash();
};

var sanctuaryOriginalSetToCoinMode = WalletComponent.prototype.setToCoinMode;
WalletComponent.prototype.setToCoinMode = function()
{
   sanctuaryOriginalSetToCoinMode.apply(this,arguments);
   this.icon_sc_mc._visible = false;
   this.sc_amount_txt._visible = false;
};

// No membership: no Member / Upgrade badge.
setIsMember = function(is)
{
   member_mc._visible = false;
};
getIsMember = function()
{
   return true;
};
member_mc._visible = false;
// The badge's orange plate is an unnamed clip at timeline depth 115 (ActionScript sees timeline depths
// offset by -16384).
_root.getInstanceAtDepth(115 - 16384)._visible = false;

// No Station Cash store picks or member bonus; Chatty takes their place in the right column.
browser.newItems_mc._visible = false;
browser.monthlyMemberPack._visible = false;
AddNewItem = function()
{
};

// The Chatty toy's 128px icon (Resources/Images/Images.txt; ii:// takes an image id, not an image set).
var CHATTY_IMAGE_ID = 46502;
var CHATTY_IMAGE_SIZE = 128;
var CHATTY_HEIGHT = 260;

function sanctuaryPopChatty()
{
   // Bottom centre of the space Popular Items and the member ad used (x 628-1090, y 114-430).
   var chatty = browser.createEmptyMovieClip("sanctuaryChatty",browser.getNextHighestDepth());
   chatty._x = 860;
   chatty._y = 425;
   // The image loader fits an image to its holder's original size, so the holder starts as an invisible
   // square of the icon's size, inside a frame that does the scaling (the loader resets the holder itself).
   var frame = chatty.createEmptyMovieClip("frame",1);
   frame._xscale = frame._yscale = CHATTY_HEIGHT / CHATTY_IMAGE_SIZE * 100;
   frame._x = - CHATTY_HEIGHT / 2;
   frame._y = - CHATTY_HEIGHT;
   var holder = frame.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   holder.moveTo(0,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,CHATTY_IMAGE_SIZE);
   holder.lineTo(0,CHATTY_IMAGE_SIZE);
   holder.endFill();
   _root.frSetIconByIndex(holder,CHATTY_IMAGE_ID);
   chatty.onRollOver = function()
   {
      fscommand("FR_event","PlaySound(DockOver)");
      gs.TweenLite.to(this,1,{_rotation:-6,_xscale:108,_yscale:108,ease:mx.transitions.easing.Elastic.easeOut});
      ToolTipEnum.ShowToolTip(ToolTipEnum.TOOLTIP_LABEL,"Visit the Coin Shop!",this);
   };
   chatty.onRollOut = chatty.onReleaseOutside = function()
   {
      gs.TweenLite.to(this,1,{_rotation:0,_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
      ToolTipEnum.HideToolTip(this);
   };
   chatty.onRelease = function()
   {
      fscommand("FR_event","PlaySound(DockSelect)");
      ToolTipEnum.HideToolTip(this);
      // The Marketplace window opens straight on the coin shop (ClientMods/marketplace).
      fscommand("FR_event","gotoMarket()");
   };
   chatty._xscale = chatty._yscale = 10;
   gs.TweenLite.to(chatty,1.1,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
}

sanctuaryHideStationCash();
sanctuaryPopChatty();
// ===== End Sanctuary welcome screen =====
