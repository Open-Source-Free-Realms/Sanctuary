// ===== Sanctuary furniture tray =====
// Appended to housingEditPanel's main timeline by build_client_mods.py.
//
// The decorate-mode furniture tray: shows SANCTUARY_TRAY_ITEMS items per page instead of 6, and a search
// that filters the current category by name. install_client_mods.py widens the tray's window to match;
// the movie draws 1:1 from the left so the extra width is room, not scaling.
//
// The stock item pages sit under a fixed-size mask shape and the dark panel behind them is a shape too,
// and shapes can't be resized from script. So the pages are rebuilt from the barItem symbol above that
// mask (with a script mask of the new width), and a wider panel is drawn over the old one.
// This window never gets the keyboard (its Lua has no text entry), so search is typed with letter buttons.

Stage.scaleMode = "noScale";
Stage.align = "TL";

var SANCTUARY_TRAY_ITEMS = 10;
// Stock layout: 6 slots 74 apart, centred on items_mc; pages at y 19, items at y 32; mask x -220..220, y 0..100.
var SANCTUARY_TRAY_STEP = 74;
var SANCTUARY_TRAY_EXTRA = (SANCTUARY_TRAY_ITEMS - 6) * SANCTUARY_TRAY_STEP;

// Every item Lua sent for the current category, and the search text.
var sanctuaryTrayItems = new Array();
var sanctuaryTraySearch = "";

function sanctuaryTrayRect(mc, x, y, w, h, r)
{
   mc.moveTo(x + r, y);
   mc.lineTo(x + w - r, y);
   mc.curveTo(x + w, y, x + w, y + r);
   mc.lineTo(x + w, y + h - r);
   mc.curveTo(x + w, y + h, x + w - r, y + h);
   mc.lineTo(x + r, y + h);
   mc.curveTo(x, y + h, x, y + h - r);
   mc.lineTo(x, y + r);
   mc.curveTo(x, y, x + r, y);
}

// The instance at a timeline depth (unnamed, or sharing its name with another).
function sanctuaryAtDepth(parent, depth)
{
   // By depth first: two stock instances share the name itemcountBG, so only one is reachable by name.
   var direct = parent.getInstanceAtDepth(depth - 16384);
   if(direct != undefined)
   {
      return direct;
   }
   var k;
   for(k in parent)
   {
      if(typeof(parent[k]) == "movieclip" && parent[k]._parent == parent && parent[k].getDepth() == depth - 16384)
      {
         return parent[k];
      }
   }
   return undefined;
}

function sanctuaryTrayWiden()
{
   var half = SANCTUARY_TRAY_EXTRA / 2;
   var i;

   // Dark panel behind the items (stock shape at x 165..635, y 94..188), drawn wider over it.
   var panel = _root.createEmptyMovieClip("sanctuaryPanel_mc", 5 - 16384);
   panel.lineStyle(2, 0x3E78B8, 100);
   panel.beginFill(0x0E3366, 92);
   sanctuaryTrayRect(panel, 165, 94, 470 + SANCTUARY_TRAY_EXTRA, 94, 12);
   panel.endFill();

   items_mc._x += half;
   noItems_mc._x += half;
   bg_mc._width = bg_mc._width + SANCTUARY_TRAY_EXTRA;

   // Item pages above the stock mask, under a script mask of the new width.
   var pages = items_mc.createEmptyMovieClip("sanctuaryPages_mc", 300);
   var mask = items_mc.createEmptyMovieClip("sanctuaryMask_mc", 301);
   mask.beginFill(0x000000, 100);
   sanctuaryTrayRect(mask, -220 - half, 0, 440 + SANCTUARY_TRAY_EXTRA, 100, 0);
   mask.endFill();
   pages.setMask(mask);

   var p;
   for(p = 0; p < 2; p++)
   {
      var page = pages.createEmptyMovieClip("page" + p + "_mc", p + 1);
      page._y = 19;
      for(i = 0; i < SANCTUARY_TRAY_ITEMS; i++)
      {
         page.attachMovie("barItem", "item" + i, i + 1, { _x: -181 - half + i * SANCTUARY_TRAY_STEP, _y: 32 });
      }
   }
   items_mc.m_fiveItems1_mc._visible = false;
   items_mc.m_fiveItems2_mc._visible = false;
   items_mc.m_fiveItems1_mc = pages.page0_mc;
   items_mc.m_fiveItems2_mc = pages.page1_mc;
   pages.page1_mc._visible = false;

   items_mc.m_left_mc._x -= half;
   items_mc.m_right_mc._x += half;

   // Right-hand panel: its background (the second itemcountBG, depth 6), options, lock, grid and the
   // decoration at depth 92.
   var right = [sanctuaryAtDepth(_root, 6), sanctuaryAtDepth(_root, 92), optionsButton_mc, lockRadio_mc, gridRadio_mc];
   for(i = 0; i < right.length; i++)
   {
      right[i]._x += SANCTUARY_TRAY_EXTRA;
   }

   items_mc.setItemsInGroup(SANCTUARY_TRAY_ITEMS);
}

// Search: a query box (click to clear), letter buttons and backspace.
function sanctuaryTraySearchBar()
{
   // Above the right-hand panel: the category buttons take the middle of the top band.
   var bar = _root.createEmptyMovieClip("sanctuarySearch_mc", _root.getNextHighestDepth());
   bar._x = 800 + SANCTUARY_TRAY_EXTRA - 246;
   bar._y = 2;

   var format = new TextFormat();
   format.font = "$MainFont";
   format.size = 11;
   format.color = 0xFFFFFF;
   format.bold = true;

   bar.lineStyle(2, 0xF2C94C, 100);
   bar.beginFill(0x0B1E3A, 90);
   sanctuaryTrayRect(bar, 0, 0, 236, 18, 6);
   bar.endFill();
   bar.createTextField("query_txt", 1, 6, 0, 224, 18);
   bar.query_txt.embedFonts = true;
   bar.query_txt.selectable = false;
   bar.query_txt.setNewTextFormat(format);

   var keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".split("");
   keys.push("<");
   var size = 16;
   var i;
   for(i = 0; i < keys.length; i++)
   {
      var key = bar.createEmptyMovieClip("key" + i + "_mc", 10 + i);
      key._x = (i % 14) * (size + 1);
      key._y = 21 + Math.floor(i / 14) * (size + 2);
      key.lineStyle(1, 0xF2C94C, 100);
      key.beginFill(keys[i] == "<" ? 0xC0392B : 0x2F6FBF, 100);
      sanctuaryTrayRect(key, 0, 0, size, size, 5);
      key.endFill();
      key.createTextField("t", 1, 0, 0, size, 16);
      key.t.embedFonts = true;
      key.t.selectable = false;
      var keyFormat = new TextFormat();
      keyFormat.font = "$MainFont";
      keyFormat.size = 10;
      keyFormat.color = 0xFFFFFF;
      keyFormat.bold = true;
      keyFormat.align = "center";
      key.t.setNewTextFormat(keyFormat);
      key.t.text = keys[i];
      key.letter = keys[i];
      key.onRelease = function()
      {
         if(this.letter == "<")
         {
            sanctuaryTraySearch = sanctuaryTraySearch.substr(0, Math.max(0, sanctuaryTraySearch.length - 1));
         }
         else
         {
            sanctuaryTraySearch += this.letter.toLowerCase();
         }
         fscommand("FR_event", "PlaySound(DockSelect)");
         sanctuaryTrayApply();
      };
   }
   // The query box is its own clip: a click handler on the bar would swallow the keys' clicks.
   // Clicking it arms typing (if the client hands this window the keyboard) and clears the search.
   var box = bar.createEmptyMovieClip("box_mc", 2);
   box.beginFill(0x000000, 0);
   sanctuaryTrayRect(box, 0, 0, 236, 18, 0);
   box.endFill();
   box.onRelease = function()
   {
      sanctuaryTrayTyping = true;
      sanctuaryTraySearch = "";
      fscommand("FR_event", "PlaySound(DockSelect)");
      sanctuaryTrayApply();
   };
   sanctuaryTrayShowQuery();
}

// Typing into the search after clicking it, when the keyboard reaches this window.
var sanctuaryTrayTyping = false;
var sanctuaryTrayKeys = new Object();
sanctuaryTrayKeys.onKeyDown = function()
{
   if(!sanctuaryTrayTyping)
   {
      return;
   }
   var code = Key.getCode();
   if(code == Key.ENTER || code == Key.ESCAPE)
   {
      sanctuaryTrayTyping = false;
      sanctuaryTrayShowQuery();
      return;
   }
   if(code == Key.BACKSPACE)
   {
      sanctuaryTraySearch = sanctuaryTraySearch.substr(0, Math.max(0, sanctuaryTraySearch.length - 1));
   }
   else
   {
      var ascii = Key.getAscii();
      if(ascii < 32 || ascii > 126)
      {
         return;
      }
      sanctuaryTraySearch += String.fromCharCode(ascii).toLowerCase();
   }
   sanctuaryTrayApply();
};
Key.addListener(sanctuaryTrayKeys);

function sanctuaryTrayShowQuery()
{
   var t = _root.sanctuarySearch_mc.query_txt;
   if(sanctuaryTraySearch == "")
   {
      t.text = sanctuaryTrayTyping ? "Type to search..." : "Search: click here or the letters";
   }
   else
   {
      t.text = sanctuaryTraySearch.toUpperCase() + (sanctuaryTrayTyping ? "_" : "") + "   (click to clear)";
   }
   t.textColor = sanctuaryTraySearch == "" ? 0xBFD7F5 : 0xFFFFFF;
}

function sanctuaryTrayMatches(item)
{
   return sanctuaryTraySearch == "" || String(item.Name).toLowerCase().indexOf(sanctuaryTraySearch) != -1;
}

// Shows the items that match the search, from the first page.
function sanctuaryTrayApply()
{
   sanctuaryTrayShowQuery();
   items_mc.resetItems();
   var shown = 0;
   var i;
   for(i = 0; i < sanctuaryTrayItems.length; i++)
   {
      if(sanctuaryTrayMatches(sanctuaryTrayItems[i]))
      {
         items_mc.addItem(sanctuaryTrayItems[i]);
         shown++;
      }
   }
   noItems_mc._visible = shown == 0;
   items_mc.slotClicked();
}

// Lua fills the tray with ResetItems, AddItem per item and DisplayItems; keep the full list for searching.
AddItem = function(guid, iconId, name, desc, quantity, memberOnly, tint)
{
   var item = new Array();
   item.Guid = guid;
   item.Icon = iconId;
   item.Name = name;
   item.Desc = desc;
   item.quantity = quantity;
   item.MemberOnly = memberOnly;
   item.tintValue = tint;
   sanctuaryTrayItems.push(item);
   if(sanctuaryTrayMatches(item))
   {
      noItems_mc._visible = false;
      items_mc.addItem(item);
   }
};

var sanctuaryOriginalResetItems = ResetItems;
ResetItems = function()
{
   sanctuaryTrayItems = new Array();
   sanctuaryOriginalResetItems.apply(_root, arguments);
};

// The category buttons are attached on the first category; keep them centred over the wider tray.
var sanctuaryOriginalAddCategoryItem = AddCategoryItem;
AddCategoryItem = function(catId, iconId, name)
{
   var first = catButtons == undefined;
   sanctuaryOriginalAddCategoryItem.apply(_root, arguments);
   if(first && catButtons != undefined)
   {
      catButtons._x += SANCTUARY_TRAY_EXTRA / 2;
   }
};

sanctuaryTrayWiden();
sanctuaryTraySearchBar();
// ===== End Sanctuary furniture tray =====
