// ===== Sanctuary coin store only =====
// Appended to marketplace's main timeline by build_client_mods.py.
// Station Cash and membership no longer exist, so the Marketplace window only shows its coin store
// page. Every other page (portal, Station Cash store, membership, claim, add funds) goes to the
// coin store instead.

// MarketplaceEnum.COIN_STORE in the client's marketplace.lua.
var COIN_STORE_MODE = "7";

var sanctuaryOriginalSetMainMode = setMainMode;
var sanctuaryRedirecting = false;

setMainMode = function(s)
{
   if(s == "coinstore")
   {
      sanctuaryRedirecting = false;
      sanctuaryOriginalSetMainMode("coinstore");
      return;
   }
   // Ask Lua for the coin store, so it also sends the coin store's categories and items.
   if(!sanctuaryRedirecting)
   {
      sanctuaryRedirecting = true;
      fscommand("FR_event","setMode(" + COIN_STORE_MODE + ")");
      return;
   }
   // Lua didn't switch pages; show the coin store page anyway.
   sanctuaryRedirecting = false;
   sanctuaryOriginalSetMainMode("coinstore");
};

// Buttons inside the window (add funds, become a member, claim, view all) ask Lua for a page here.
base.FSCommands.fsSetMode = function(mode)
{
   fscommand("FR_event","setMode(" + COIN_STORE_MODE + ")");
};

// Lua's Station Cash and membership purchase flows.
setModeAddSC = function()
{
   setMainMode("coinstore");
};
setModeMembershipSC = function()
{
   setMainMode("coinstore");
};
settoMembershipPreview_lua = function(sku)
{
   setMainMode("coinstore");
};

// The Station Cash wallet sits on top of every page.
_root.wallet._visible = false;

// The coin store header has its own Station Cash wallet (top left). Chatty pops up there instead.
var sanctuaryOriginalGetWalletInfo = WalletInfo.prototype._handleGetWalletInfo;
WalletInfo.prototype._handleGetWalletInfo = function()
{
   sanctuaryOriginalGetWalletInfo.apply(this,arguments);
   this._visible = false;
};
var sanctuaryOriginalSetWalletInfo = WalletInfo.prototype._handleSetWalletInfo;
WalletInfo.prototype._handleSetWalletInfo = function()
{
   sanctuaryOriginalSetWalletInfo.apply(this,arguments);
   this._visible = false;
};
_root.mc_coinstore.mcWalletInfo._visible = false;

// The Chatty toy's 128px icon (Resources/Images/Images.txt; ii:// takes an image id, not an image set).
var CHATTY_IMAGE_ID = 46502;
var CHATTY_IMAGE_SIZE = 128;
var CHATTY_HEIGHT = 110;

// Loads the Chatty icon into a new clip of parent, sized height and anchored at its bottom centre.
// The image loader fits an image to its holder's original size, so the holder starts as an invisible
// square of the icon's size, inside a frame that does the scaling (the loader resets the holder itself).
function sanctuaryMakeChatty(parent, name, height)
{
   var chatty = parent.createEmptyMovieClip(name,parent.getNextHighestDepth());
   var frame = chatty.createEmptyMovieClip("frame",1);
   frame._xscale = frame._yscale = height / CHATTY_IMAGE_SIZE * 100;
   frame._x = - height / 2;
   frame._y = - height;
   var holder = frame.createEmptyMovieClip("holder",1);
   holder.beginFill(0,0);
   holder.moveTo(0,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,0);
   holder.lineTo(CHATTY_IMAGE_SIZE,CHATTY_IMAGE_SIZE);
   holder.lineTo(0,CHATTY_IMAGE_SIZE);
   holder.endFill();
   _root.frSetIconByIndex(holder,CHATTY_IMAGE_ID);
   return chatty;
}

function sanctuaryPopChatty()
{
   var store = _root.mc_coinstore;
   var chatty = store.sanctuaryChatty;
   if(chatty == undefined)
   {
      // Stands on the header's bottom edge where the wallet was, so he pokes up out of the window.
      var spot = store.mcWalletInfo.getBounds(store);
      chatty = sanctuaryMakeChatty(store,"sanctuaryChatty",CHATTY_HEIGHT);
      chatty._x = (spot.xMin + spot.xMax) / 2;
      chatty._y = spot.yMax + 12;
      chatty.onRollOver = function()
      {
         gs.TweenLite.to(this,1,{_rotation:-8,ease:mx.transitions.easing.Elastic.easeOut});
      };
      chatty.onRollOut = function()
      {
         gs.TweenLite.to(this,1,{_rotation:0,ease:mx.transitions.easing.Elastic.easeOut});
      };
   }
   chatty._xscale = chatty._yscale = 10;
   chatty._rotation = 0;
   gs.TweenLite.to(chatty,0.9,{_xscale:100,_yscale:100,ease:mx.transitions.easing.Elastic.easeOut});
}

// The client's item sorts (Resources/ItemSortTypes.txt) in display order, with their English names
// (the combo box shows its text as is, so locale ids don't resolve). The coin store's Lua sends the
// Marketplace's sort options, which are empty without the Station Cash store.
var SANCTUARY_SORT_OPTIONS = [
   {Id:1, Name:"Default"},
   {Id:2, Name:"Alphabetical A - Z"},
   {Id:4, Name:"Alphabetical Z - A"},
   {Id:3, Name:"Price Low to High"},
   {Id:5, Name:"Price High to Low"}
];

// The coin store's Sort By menu read the Station Cash store's sort options ("undefined"); it reads its
// own, falling back to the client's item sorts.
coinstore.CoinStoreManager.prototype.onSetSortOptions = function()
{
   var model = base.CoinStoreModel.getInstance();
   var options = model.dsSortOptions.data;
   if(options == undefined || options.length == 0 || options[0].Name == undefined)
   {
      options = SANCTUARY_SORT_OPTIONS;
   }
   var sortId;
   if(model.dsSortState.data.length > 0)
   {
      sortId = model.dsSortState.data[0].SortId;
   }
   // The combo box's own setup (label field, change listener) runs on a load callback that the coin
   // store's copy can miss; without it every option reads "undefined".
   var combo = this.mcSort.cbSorts;
   combo.labelField = "Name";
   if(!this.mcSort.sanctuaryListening)
   {
      this.mcSort.sanctuaryListening = true;
      var listener = new Object();
      listener.onValueChange = function(data)
      {
         base.FSCommands.fsSetSortOptionForCoinStore(data.Id);
      };
      combo.addListener(listener);
   }
   this.mcSort.setData(options,sortId);
};

var sanctuaryOriginalCoinStoreEnter = mainstates.StateCoinStore.prototype.enterState;
mainstates.StateCoinStore.prototype.enterState = function()
{
   sanctuaryOriginalCoinStoreEnter.apply(this,arguments);
   var store = this.manager.mc_coinstore;
   store.mcWalletInfo._visible = false;
   // (Not getSortStateForCoinStore: the client's Lua has no CoinStoreHandler:SetSortState.)
   store.onSetSortOptions();
   sanctuaryPopChatty();
};

// Everyone pays what used to be the member price, so the side panel shows that one price: no regular
// price, member savings or members-only badge (the panel's own non-member layout, used for China).
function sanctuaryShowOnePrice(panel)
{
   var widget = panel.mcActionWidget;
   widget.notMemberPrice._visible = false;
   widget.savings._visible = false;
   widget.MOText._visible = false;
   widget.memberPrice._alpha = 100;
   _root.frSetTextPreserveWidthV3(widget.memberPrice.label,"S##marketplace_label_price2",true);
   panel.mcMemberGraphic._visible = false;
   panel.btnMembersOnly._visible = false;
   panel.UpgradeCantBuy._visible = false;
   widget._visible = true;
}

var sanctuaryOriginalSetMembershipState = coinstore.CoinStoreSidePanel.prototype._setMembershipState;
coinstore.CoinStoreSidePanel.prototype._setMembershipState = function()
{
   sanctuaryOriginalSetMembershipState.apply(this,arguments);
   sanctuaryShowOnePrice(this);
};

var sanctuaryOriginalCoinStoreSetMode = coinstore.CoinStoreSidePanel.prototype._handleCoinStoreSetMode;
coinstore.CoinStoreSidePanel.prototype._handleCoinStoreSetMode = function(s)
{
   sanctuaryOriginalCoinStoreSetMode.apply(this,arguments);
   if(s == base.CoinStoreModel.MODE_BUY)
   {
      sanctuaryShowOnePrice(this);
   }
};
// ===== End Sanctuary coin store only =====
