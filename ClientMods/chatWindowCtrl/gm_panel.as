// ===== Sanctuary GM panel =====
// Appended to chatWindowCtrl's main timeline by build_client_mods.py.
//
// Opened by the "Referee Tools" button on the main hub (ClientMods/gamedock), which sends "!gm".
// The server answers with hidden chat lines "[[SANCTUARY_GM]]|kind|field|..." (GmChatCommand)
// that are swallowed here and rendered. Every button sends a "!" command through chat.

var gmToken = "[[SANCTUARY_GM]]";

// install_client_mods.py enlarges the chat window to about the size of the screen (the client clips
// each movie to its window, and its Lua bottom-aligns the chat window). Drawing 1:1 anchored
// bottom-left keeps chat where it always was, and the rest of the window is room for the panel.
Stage.scaleMode = "noScale";
Stage.align = "BL";

// Height of the chat movie's own stage; with bottom-left alignment the visible area spans
// y = GM_CHAT_STAGE_H - Stage.height (top) .. GM_CHAT_STAGE_H (bottom).
var GM_CHAT_STAGE_H = 345;
var GM_MIN_SCALE = 0.6;

function gmTop()
{
   return Math.min(0, GM_CHAT_STAGE_H - Stage.height);
}

function gmBottom()
{
   return Math.max(GM_CHAT_STAGE_H, Stage.height + gmTop());
}

function gmRight()
{
   return Stage.width;
}

// Base layout; the panel scales from here (resize grip, maximize). Tabs run down a sidebar on the left
// and every tab draws into the content area beside it.
var GM_W = 780;
var GM_H = 500;
var GM_SIDEBAR_W = 112;
var GM_CONTENT_X = GM_SIDEBAR_W + 24;
var GM_CONTENT_Y = 52;
var GM_CONTENT_W = GM_W - GM_CONTENT_X - 20;
var GM_CONTENT_H = GM_H - GM_CONTENT_Y - 40;
var GM_COLUMNS = 3;
var GM_CELL_W = 204;
var GM_CELL_H = 52;

// Hub palette.
var GM_GOLD = 0xF2C94C;
var GM_GOLD_DARK = 0x9C6B12;
var GM_BLUE_TOP = 0x4C8FD6;
var GM_BLUE_BOTTOM = 0x1B4A8C;
var GM_BLUE_INNER = 0x10325F;
var GM_BLUE_CELL = 0x1E5799;
var GM_BLUE_ACTIVE = 0x2F6FBF;
var GM_TEXT = 0xFFFFFF;
var GM_TEXT_DIM = 0xBFD7F5;
var GM_TEXT_GOLD = 0xFFE68A;

var gmTab = "items";
var gmMaximized = false;
var gmScale = 1;
var gmResults = [];
var gmList = { kind: "", search: "", page: 1, pages: 1, total: 0 };
var gmTabs = [
   ["items", "Items"],
   ["npcs", "NPCs"],
   ["players", "Players"],
   ["houses", "Houses"],
   ["world", "World"],
   ["edit", "Edit"],
   ["script", "Script"],
   ["conv", "Dialogue"],
   ["quests", "Quests"]
];
// Entity selected for editing (god mode click), as sent by the server: ["npc"|"prop"|"none", fields...].
var gmEdit = ["none"];
// Emotes for the editor: [[animationId, label], ...]; the server may replace it with an "anims" line.
var gmAnims = [];

// --- drawing -----------------------------------------------------------------

function gmFormat(size, color, bold, align)
{
   var f = new TextFormat();
   f.font = "$MainFont";
   f.size = size;
   f.color = color;
   f.bold = bold == true;
   f.align = align == undefined ? "left" : align;
   return f;
}

function gmText(mc, name, txt, x, y, w, h, size, color, bold, align)
{
   mc.createTextField(name, mc.getNextHighestDepth(), x, y, w, h);
   var t = mc[name];
   t.embedFonts = true;
   t.selectable = false;
   t.wordWrap = true;
   t.multiline = true;
   t.setNewTextFormat(gmFormat(size, color, bold, align));
   t.text = txt;
   return t;
}

function gmRoundRect(mc, x, y, w, h, r)
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

// Gold-rimmed blue panel like the hub windows.
function gmPanelShape(mc, w, h, r, top, bottom, rim, rimWidth)
{
   mc.clear();
   mc.lineStyle(rimWidth, rim, 100);
   var matrix = { matrixType: "box", x: 0, y: 0, w: w, h: h, r: Math.PI / 2 };
   mc.beginGradientFill("linear", [top, bottom], [100, 100], [0, 255], matrix);
   gmRoundRect(mc, 0, 0, w, h, r);
   mc.endFill();
}

function gmButton(parent, name, label, x, y, w, h, onClick, color)
{
   var b = parent.createEmptyMovieClip(name, parent.getNextHighestDepth());
   b._x = x;
   b._y = y;
   b.w = w;
   b.h = h;
   b.base = color == undefined ? GM_BLUE_TOP : color;
   b.bg = b.createEmptyMovieClip("bg_mc", 1);
   gmPanelShape(b.bg, w, h, Math.min(8, h / 2), b.base, GM_BLUE_BOTTOM, GM_GOLD, 2);
   var size = h >= 22 ? 12 : 10;
   // Single line, centered both ways.
   var label_txt = gmText(b, "label_txt", label, 2, Math.round((h - size) / 2) - 3, w - 4, size + 6, size, GM_TEXT, true, "center");
   label_txt.wordWrap = false;
   label_txt.multiline = false;
   b.onRelease = onClick;
   b.onRollOver = function()
   {
      gmPanelShape(this.bg, this.w, this.h, Math.min(8, this.h / 2), 0x7DB4EE, this.base, 0xFFF3B0, 2);
      fscommand("FR_event", "PlaySound(DockOver)");
   };
   b.onRollOut = b.onReleaseOutside = function()
   {
      gmPanelShape(this.bg, this.w, this.h, Math.min(8, this.h / 2), this.base, GM_BLUE_BOTTOM, GM_GOLD, 2);
   };
   return b;
}

function gmUnescape(s)
{
   s = frHelperClass.stringReplace(String(s), "&lt;", "<");
   s = frHelperClass.stringReplace(s, "&gt;", ">");
   return frHelperClass.stringReplace(s, "&amp;", "&");
}

// Item and house icons are image sets; slot 6 is the icon (as the hub's item slots use).
function gmIcon(parent, name, imageSetId, x, y, size)
{
   var frame = parent.createEmptyMovieClip(name, parent.getNextHighestDepth());
   frame._x = x;
   frame._y = y;
   frame.lineStyle(1, GM_GOLD_DARK, 100);
   frame.beginFill(GM_BLUE_INNER, 100);
   gmRoundRect(frame, 0, 0, size, size, 6);
   frame.endFill();
   var holder = frame.createEmptyMovieClip("holder", 1);
   holder._x = 2;
   holder._y = 2;
   // The client's loader (AutoMovieClipEntry) sizes the loaded image to the holder's original
   // bounds, so an empty holder would shrink every icon to nothing. Give it the icon's size.
   holder.beginFill(0x000000, 0);
   gmRoundRect(holder, 0, 0, size - 4, size - 4, 0);
   holder.endFill();
   if(Number(imageSetId) > 0)
   {
      _root.frSetIconByImageSetSlotNoTint(holder, imageSetId, 6);
   }
   return frame;
}

// --- commands ------------------------------------------------------------------

function gmSend(cmd)
{
   if(!_root.frRunningInGame())
   {
      return;
   }
   fscommand("FR_event", "PlaySound(DockSelect)");
   fscommand("FR_event", "sendChatMessage(  \"" + frHelperClass.stringReplace(cmd, "\"", "\"\"") + "\")");
}

// Puts a command in the chat box so the referee can type the rest and press Enter.
function gmPrompt(prefix)
{
   fscommand("FR_event", "PlaySound(DockSelect)");
   onSendEntryBoxOpen();
   entryBox_mc.setText(prefix);
}

// --- panel -----------------------------------------------------------------------

function gmBuild()
{
   var root_mc = _root.createEmptyMovieClip("gm_mc", 16000);
   root_mc._x = 8;
   root_mc._y = GM_CHAT_STAGE_H - GM_H - 8;
   root_mc._visible = false;

   var frame = root_mc.createEmptyMovieClip("frame_mc", 1);
   gmPanelShape(frame, GM_W, GM_H, 14, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 4);
   frame.onPress = function()
   {
      // Keep the panel inside the window while dragging; anything outside it is clipped.
      var p = _root.gm_mc;
      var w = GM_W * p._xscale / 100;
      var h = GM_H * p._yscale / 100;
      p.startDrag(false, 0, gmTop(), Math.max(0, gmRight() - w), Math.max(gmTop(), gmBottom() - h));
   };
   frame.onRelease = frame.onReleaseOutside = function()
   {
      _root.gm_mc.stopDrag();
   };

   // Gold title plaque.
   var plaque = root_mc.createEmptyMovieClip("plaque_mc", 2);
   plaque._x = GM_W / 2 - 100;
   plaque._y = -12;
   gmPanelShape(plaque, 200, 28, 13, 0xFFE68A, GM_GOLD_DARK, 0xFFF6CF, 2);
   gmText(plaque, "title_txt", "Referee Tools", 0, 4, 200, 22, 15, 0x3A2400, true, "center");

   gmButton(root_mc, "max_mc", "[ ]", GM_W - 62, 8, 26, 22, function()
   {
      gmMaximized = !gmMaximized;
      gmFit();
   });
   gmButton(root_mc, "close_mc", "X", GM_W - 32, 8, 24, 22, function()
   {
      _root.gm_mc._visible = false;
   }, 0xC0392B);

   var i;
   for(i = 0; i < gmTabs.length; i++)
   {
      var tab = gmButton(root_mc, "tab" + i + "_mc", gmTabs[i][1], 12, GM_CONTENT_Y - 8 + i * 38, GM_SIDEBAR_W - 4, 32, function()
      {
         gmShowTab(this.tabId);
      });
      tab.tabId = gmTabs[i][0];
   }

   var inner = root_mc.createEmptyMovieClip("inner_mc", 50);
   inner._x = GM_CONTENT_X - 10;
   inner._y = GM_CONTENT_Y - 10;
   inner.lineStyle(2, GM_GOLD_DARK, 100);
   inner.beginFill(GM_BLUE_INNER, 92);
   gmRoundRect(inner, 0, 0, GM_CONTENT_W + 20, GM_CONTENT_H + 16, 10);
   inner.endFill();

   gmText(root_mc, "status_txt", "", GM_CONTENT_X, GM_H - 28, GM_CONTENT_W - 20, 18, 11, GM_TEXT_DIM, false);

   root_mc.createEmptyMovieClip("content_mc", 60);
   gmAddResizeGrip(root_mc);
   gmClearContent();

   var stageListener = new Object();
   stageListener.onResize = function()
   {
      gmFit();
   };
   Stage.addListener(stageListener);
}

// Keeps the whole panel on screen at any window size; maximized fills the space available.
// Largest scale for the panel. The enlarged chat window can run a little past the screen edge,
// so keep a margin and stay at 90% of the window.
var GM_MAX_FILL = 0.9;

function gmMaxScale()
{
   return GM_MAX_FILL * Math.min((gmRight() - 40) / GM_W, (gmBottom() - gmTop() - 80) / GM_H);
}

// Keeps the panel on screen at its current size; maximized fills the visible area.
function gmFit()
{
   var root_mc = _root.gm_mc;
   var scale = gmMaximized ? gmMaxScale() : Math.max(GM_MIN_SCALE, Math.min(gmScale, gmMaxScale()));
   if(scale <= 0)
   {
      return;
   }
   root_mc._xscale = root_mc._yscale = scale * 100;
   var w = GM_W * scale;
   var h = GM_H * scale;
   if(gmMaximized)
   {
      root_mc._x = (gmRight() - w) / 2;
      root_mc._y = gmTop() + (gmBottom() - gmTop() - h) / 2;
      return;
   }
   root_mc._x = Math.max(0, Math.min(root_mc._x, gmRight() - w));
   root_mc._y = Math.max(gmTop() + 14, Math.min(root_mc._y, gmBottom() - h));
}

// Bottom-right grip: drag to scale the whole panel (every tab keeps its layout).
function gmAddResizeGrip(root_mc)
{
   var grip = root_mc.createEmptyMovieClip("grip_mc", 70);
   grip._x = GM_W - 22;
   grip._y = GM_H - 22;
   grip.beginFill(0x000000, 0);
   gmRoundRect(grip, 0, 0, 20, 20, 0);
   grip.endFill();
   grip.lineStyle(2, GM_GOLD, 100);
   var i;
   for(i = 0; i < 3; i++)
   {
      grip.moveTo(18, 6 + i * 5);
      grip.lineTo(6 + i * 5, 18);
   }
   grip.onPress = function()
   {
      gmMaximized = false;
      this.onMouseMove = function()
      {
         var p = _root.gm_mc;
         var scale = Math.max((_root._xmouse - p._x) / GM_W, (_root._ymouse - p._y) / GM_H);
         gmScale = Math.max(GM_MIN_SCALE, Math.min(scale, gmMaxScale()));
         p._xscale = p._yscale = gmScale * 100;
         updateAfterEvent();
      };
   };
   grip.onRelease = grip.onReleaseOutside = function()
   {
      delete this.onMouseMove;
      gmFit();
   };
}

function gmStatus(s)
{
   _root.gm_mc.status_txt.text = s;
}

function gmClearContent()
{
   var root_mc = _root.gm_mc;
   root_mc.content_mc.removeMovieClip();
   var content = root_mc.createEmptyMovieClip("content_mc", 60);
   content._x = GM_CONTENT_X;
   content._y = GM_CONTENT_Y;
   return content;
}

function gmShowTab(tabId)
{
   gmTab = tabId;
   var i;
   for(i = 0; i < gmTabs.length; i++)
   {
      var tab = _root.gm_mc["tab" + i + "_mc"];
      tab.base = gmTabs[i][0] == tabId ? GM_BLUE_ACTIVE : GM_BLUE_TOP;
      tab.onRollOut();
   }
   var content = gmClearContent();
   if(tabId == "world")
   {
      gmRenderWorld(content);
      return;
   }
   if(tabId == "edit")
   {
      gmRenderEdit(content);
      return;
   }
   if(tabId == "script")
   {
      gmRenderScript(content);
      return;
   }
   if(tabId == "conv")
   {
      gmRenderConv(content);
      return;
   }
   if(tabId == "quests")
   {
      gmRenderQuests(content);
      return;
   }
   gmToolbar(content, tabId);
   if(gmList.kind == tabId)
   {
      gmRenderGrid(content);
   }
   else
   {
      gmSend("!gm " + tabId + " 1");
   }
}

// Search box, show-all and paging bar on every lookup tab.
function gmToolbar(content, kind)
{
   gmSearchBox(content, kind, 0, 0, 176, 24);
   var search = gmButton(content, "search_mc", "Search", 180, 0, 66, 24, function()
   {
      gmSearch(this.kind);
   });
   search.kind = kind;
   var all = gmButton(content, "all_mc", "Show all", 250, 0, 74, 24, function()
   {
      _root.gm_mc.content_mc.search_txt.text = "";
      gmSend("!gm " + this.kind + " 1");
   });
   all.kind = kind;
   gmButton(content, "prev_mc", "<", 330, 0, 28, 24, function()
   {
      gmPage(kind, -1);
   });
   gmText(content, "page_txt", "", 360, 4, 112, 20, 11, GM_TEXT, true, "center");
   gmButton(content, "next_mc", ">", 474, 0, 28, 24, function()
   {
      gmPage(kind, 1);
   });
   if(kind == "houses")
   {
      gmButtonRow(content, 244, [
         ["My houses", "!house list"],
         ["Enter", "!house enter"],
         ["Leave", "!house leave"],
         ["Furniture", "!furniture"],
         ["Anywhere", "!house unbound"]
      ]);
   }
}

// Single-line text input. Typing needs the chat window to own the keyboard, which Lua grants
// through showTextEntry (it focuses the chat entry box, so focus is taken back right after).
// Enter calls onSubmit(text).
function gmInput(parent, name, x, y, w, h, value, hint, onSubmit)
{
   var box = parent.createEmptyMovieClip(name + "_box", parent.getNextHighestDepth());
   box._x = x;
   box._y = y;
   box.lineStyle(2, GM_GOLD, 100);
   box.beginFill(0x0B1E3A, 100);
   gmRoundRect(box, 0, 0, w, h, 8);
   box.endFill();

   parent.createTextField(name, parent.getNextHighestDepth(), x + 6, y + 3, w - 12, h - 4);
   var t = parent[name];
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.wordWrap = false;
   t.multiline = false;
   t.maxChars = 64;
   t.setNewTextFormat(gmFormat(12, GM_TEXT, false));
   t.text = value == undefined ? "" : String(value);
   t.onSubmit = onSubmit;

   parent.createTextField(name + "_hint", parent.getNextHighestDepth(), x + 6, y + 4, w - 12, h - 4);
   var hintText = parent[name + "_hint"];
   hintText.embedFonts = true;
   hintText.selectable = false;
   hintText.setNewTextFormat(gmFormat(11, GM_TEXT_DIM, false));
   hintText.text = hint == undefined ? "" : hint;
   hintText._visible = t.text == "";
   t.hint = hintText;

   t.onChanged = function()
   {
      this.hint._visible = this.text == "";
   };
   t.onSetFocus = function()
   {
      this.hint._visible = false;
   };
   t.onKillFocus = function()
   {
      this.hint._visible = this.text == "";
   };

   box.field = t;
   box.onPress = function()
   {
      gmFocusInput(this.field);
   };
   return t;
}

function gmFocusInput(t)
{
   fscommand("FR_event", "showTextEntry()");
   setTimeout(function()
   {
      Selection.setFocus(t);
      Selection.setSelection(t.text.length, t.text.length);
   }, 50);
}

// Field text made safe for a chat command and the server's '|'-separated replies.
function gmFieldText(t)
{
   return t == undefined ? "" : frHelperClass.stringReplace(String(t.text), "|", " ");
}

// Runs the focused input's onSubmit on Enter instead of sending chat.
var gmKeyListener = new Object();
gmKeyListener.onKeyDown = function()
{
   if(!_root.gm_mc._visible || Key.getCode() != Key.ENTER)
   {
      return;
   }
   var t = eval(Selection.getFocus());
   if(t.onSubmit != undefined)
   {
      t.onSubmit(gmFieldText(t));
      fscommand("FR_event", "killFocus()");
   }
};
Key.addListener(gmKeyListener);

// Search box for the current tab's lookup.
function gmSearchBox(content, kind, x, y, w, h)
{
   var t = gmInput(content, "search_txt", x, y, w, h, gmList.kind == kind ? gmList.search : "", "Search by name or id...", function(text)
   {
      gmSend("!gm " + this.kind + " 1 " + text);
   });
   t.kind = kind;
}

function gmSearch(kind)
{
   gmSend("!gm " + kind + " 1 " + gmFieldText(_root.gm_mc.content_mc.search_txt));
   fscommand("FR_event", "killFocus()");
}

function gmButtonRow(content, y, buttons)
{
   var width = Math.floor((GM_COLUMNS * (GM_CELL_W + 2)) / buttons.length) - 4;
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var b = gmButton(content, "row" + y + "_" + i + "_mc", buttons[i][0], i * (width + 4), y, width, 22, function()
      {
         gmSend(this.cmd);
      });
      b.cmd = buttons[i][1];
   }
}

function gmPage(kind, delta)
{
   if(gmList.kind != kind)
   {
      return;
   }
   var page = gmList.page + delta;
   if(page < 1 || page > gmList.pages)
   {
      return;
   }
   gmSend("!gm " + kind + " " + page + " " + gmList.search);
}

function gmRenderGrid(content)
{
   var kind = gmList.kind;
   content.page_txt.text = "Page " + gmList.page + " / " + gmList.pages;
   gmStatus(gmList.total + (gmList.search == "" ? " total" : " matching \"" + gmList.search + "\""));
   var i;
   for(i = 0; i < gmResults.length; i++)
   {
      var r = gmResults[i];
      var cell = content.createEmptyMovieClip("cell" + i + "_mc", 100 + i);
      cell._x = (i % GM_COLUMNS) * (GM_CELL_W + 2);
      cell._y = 30 + Math.floor(i / GM_COLUMNS) * (GM_CELL_H + 3);
      gmPanelShape(cell, GM_CELL_W, GM_CELL_H, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      if(kind == "items")
      {
         gmItemCell(cell, r);
      }
      else if(kind == "npcs")
      {
         gmNpcCell(cell, r);
      }
      else if(kind == "houses")
      {
         gmHouseCell(cell, r);
      }
      else if(kind == "players")
      {
         gmPlayerCell(cell, r);
      }
   }
   gmStatus(gmList.total + " result(s)");
}

function gmSmall(cell, name, label, x, w, cmd, prompt)
{
   var b = gmButton(cell, name, label, x, GM_CELL_H - 20, w, 17, function()
   {
      if(this.prompt)
      {
         gmPrompt(this.cmd);
      }
      else
      {
         gmSend(this.cmd);
      }
   });
   b.cmd = cmd;
   b.prompt = prompt == true;
   return b;
}

// Name + id line next to an optional icon.
function gmCellText(cell, title, subtitle, left)
{
   var name_txt = gmText(cell, "name_txt", title, left, 1, GM_CELL_W - left - 4, 16, 10, GM_TEXT, true);
   name_txt.wordWrap = false;
   gmText(cell, "id_txt", subtitle, left, 14, GM_CELL_W - left - 4, 14, 9, GM_TEXT_DIM, false).wordWrap = false;
}

// item: [id, imageSetId, tint, type, name]
function gmItemCell(cell, r)
{
   gmIcon(cell, "icon_mc", r[1], 3, 3, 44);
   gmCellText(cell, r[4], "#" + r[0], 50);
   gmSmall(cell, "give1_mc", "+1", 50, 30, "!give " + r[0] + " 1");
   gmSmall(cell, "give999_mc", "+999", 83, 38, "!give " + r[0] + " 999");
   gmSmall(cell, "place_mc", "Place", 124, 38, "!place mode " + r[0]);
}

// npc: [id, modelId, name]
function gmNpcCell(cell, r)
{
   gmCellText(cell, r[2], "#" + r[0] + "   model " + r[1], 6);
   gmSmall(cell, "spawn_mc", "Spawn here", 6, 84, "!npc spawn " + r[0]);
   gmSmall(cell, "despawn_mc", "Despawn", 94, 66, "!npc despawn");
}

// house: [id, imageSetId, name]
function gmHouseCell(cell, r)
{
   gmIcon(cell, "icon_mc", r[1], 3, 3, 44);
   gmCellText(cell, r[2], "#" + r[0], 50);
   gmSmall(cell, "grant_mc", "Grant to me", 50, 112, "!house grant " + r[0]);
}

// player: [guid, name, zoneName]
function gmPlayerCell(cell, r)
{
   gmCellText(cell, r[1], r[2], 6);
   gmSmall(cell, "goto_mc", "Go to", 6, 48, "!gm goto " + r[1]);
   gmSmall(cell, "summon_mc", "Summon", 57, 56, "!gm summon " + r[1]);
   gmSmall(cell, "mute_mc", "Mute", 116, 44, "!mod mute " + r[1] + " ", true);
}

function gmRenderWorld(content)
{
   gmText(content, "props_txt", "World props", 0, 0, 300, 18, 12, GM_TEXT_GOLD, true);
   gmButtonRow(content, 20, [
      ["Nearby props", "!place list"],
      ["Remove prop", "!place remove"],
      ["Stop placing", "!place off"],
      ["Despawn NPC", "!npc despawn"]
   ]);
   gmText(content, "move_txt", "Movement", 0, 54, 300, 18, 12, GM_TEXT_GOLD, true);
   gmButtonRow(content, 74, [
      ["God mode on", "!godmode on"],
      ["God mode off", "!godmode off"],
      ["Where am I", "!gm pos"],
      ["Zones", "!zone ls"],
      ["Help", "!help"]
   ]);
   gmText(content, "admin_txt", "Prompts (type the rest in chat)", 0, 104, 400, 18, 12, GM_TEXT_GOLD, true);
   var prompts = [
      ["Teleport x y z", "!teleport "],
      ["Mount model", "!mount "],
      ["Promote", "!admin promote "],
      ["Demote", "!admin demote "],
      ["Ban", "!mod ban "],
      ["Unban", "!mod unban "],
      ["Mute", "!mod mute "],
      ["Unmute", "!mod unmute "],
      ["Find quest", "!quest find "],
      ["Start quest", "!quest start "],
      ["Skip goal", "!quest skip "],
      ["Finish quest", "!quest finish "],
      ["Reset quest", "!quest reset "],
      ["Find pet", "!pet find "],
      ["Give pet", "!pet give "],
      ["Grant collect.", "!collection grant "],
      ["Reset collect.", "!collection reset "],
      ["Go to area", "!area goto "]
   ];
   var i;
   for(i = 0; i < prompts.length; i++)
   {
      var b = gmButton(content, "prompt" + i + "_mc", prompts[i][0], (i % 6) * 106, 124 + Math.floor(i / 6) * 28, 100, 24, function()
      {
         gmPrompt(this.prefix);
      });
      b.prefix = prompts[i][1];
   }
   gmText(content, "game_txt", "Game systems", 0, 214, 300, 18, 12, GM_TEXT_GOLD, true);
   gmButtonRow(content, 234, [
      ["My quests", "!quest list"],
      ["My pets", "!pet list"],
      ["Collections", "!collection status"],
      ["Area here", "!area here"],
      ["Zone areas", "!area list"]
   ]);
   gmStatus("World tools");
}

// --- editor --------------------------------------------------------------------

// Label, input and "Set" button that sends "<command> <text>" for the selected entity.
function gmEditField(content, name, label, x, y, labelW, inputW, value, command)
{
   gmText(content, name + "_label", label, x, y + 4, labelW, 18, 11, GM_TEXT_DIM, true);
   var t = gmInput(content, name, x + labelW, y, inputW, 22, value, "", function(text)
   {
      gmSend(this.command + " " + text);
   });
   t.command = command;
   var setButton = gmButton(content, name + "_set", "Set", x + labelW + inputW + 3, y, 36, 22, function()
   {
      gmSend(this.field.command + " " + gmFieldText(this.field));
   });
   setButton.field = t;
   return t;
}

// Row of option buttons, the current one highlighted.
function gmEditChoices(content, name, label, x, y, labelW, buttonW, choices, current)
{
   gmText(content, name + "_label", label, x, y + 4, labelW, 18, 11, GM_TEXT_DIM, true);
   var i;
   for(i = 0; i < choices.length; i++)
   {
      var active = String(choices[i][2]) == String(current);
      var b = gmButton(content, name + i + "_mc", choices[i][0], x + labelW + i * (buttonW + 4), y, buttonW, 22, function()
      {
         gmSend(this.cmd);
      }, active ? GM_BLUE_ACTIVE : undefined);
      b.cmd = choices[i][1];
   }
}

function gmEditButtons(content, y, buttons)
{
   var i;
   for(i = 0; i < buttons.length; i++)
   {
      var b = gmButton(content, "editbtn" + y + "_" + i + "_mc", buttons[i][0], i * 84, y, 80, 22, function()
      {
         gmSend(this.cmd);
      }, buttons[i][2]);
      b.cmd = buttons[i][1];
   }
}

function gmRenderEdit(content)
{
   var kind = gmEdit[0];
   if((kind == "npc" || kind == "prop") && gmFields != undefined && gmFields.guid == gmEdit[1])
   {
      gmRenderFields(content);
   }
   else if(kind == "npc")
   {
      gmRenderEditNpc(content, gmEdit);
   }
   else if(kind == "prop")
   {
      gmRenderEditProp(content, gmEdit);
   }
   else
   {
      gmText(content, "none_txt", "Nothing selected. Turn on god mode, then click an NPC or world prop to edit it here.", 0, 0, 500, 40, 12, GM_TEXT_DIM, false);
      gmButtonRow(content, 48, [
         ["God mode on", "!godmode on"],
         ["God mode off", "!godmode off"]
      ]);
      // House furniture: rotates the piece last placed or moved (in edit mode).
      gmText(content, "rotate_txt", "Rotate my last placed / moved furniture", 0, 84, 500, 18, 12, GM_TEXT_GOLD, true);
      gmButtonRow(content, 104, [
         ["Yaw -45", "!house rotate yaw -45"],
         ["Yaw +45", "!house rotate yaw 45"],
         ["Pitch -45", "!house rotate pitch -45"],
         ["Pitch +45", "!house rotate pitch 45"],
         ["Roll -45", "!house rotate roll -45"],
         ["Roll +45", "!house rotate roll 45"]
      ]);
      gmButtonRow(content, 130, [
         ["Yaw -15", "!house rotate yaw -15"],
         ["Yaw +15", "!house rotate yaw 15"],
         ["Pitch -15", "!house rotate pitch -15"],
         ["Pitch +15", "!house rotate pitch 15"],
         ["Roll -15", "!house rotate roll -15"],
         ["Roll +15", "!house rotate roll 15"]
      ]);
      gmStatus("Editor");
   }
}

// edit|npc|guid|name|nameId|model|scale|animation|disposition|nameplate|behavior|speed|event|x|y|z
function gmRenderEditNpc(content, e)
{
   var title = (e[2] == "" ? "Unnamed NPC" : e[2]) + (Number(e[3]) > 0 ? "  (name #" + e[3] + ")" : "");
   gmText(content, "title_txt", title, 0, 0, 330, 20, 13, GM_TEXT_GOLD, true);
   gmText(content, "where_txt", "at " + e[12] + ", " + e[13] + ", " + e[14], 330, 2, 174, 18, 11, GM_TEXT_DIM, false, "right");

   gmEditField(content, "name_txt", "Name", 0, 22, 44, 166, e[2], "!npc name");
   gmButton(content, "clearname_mc", "Clear", 252, 22, 44, 22, function()
   {
      gmSend("!npc name clear");
   });
   gmEditField(content, "event_txt", "Event", 304, 22, 44, 116, e[11], "!npc event");

   gmEditField(content, "model_txt", "Model", 0, 50, 44, 70, e[4], "!npc model");
   gmEditField(content, "scale_txt", "Scale", 160, 50, 40, 50, e[5], "!npc scale");
   gmEditField(content, "speed_txt", "Speed", 296, 50, 44, 50, e[10], "!npc speed");

   gmEditChoices(content, "disp", "Attitude", 0, 78, 52, 60, [
      ["Hostile", "!npc disposition hostile", 0],
      ["Neutral", "!npc disposition neutral", 1],
      ["Ally", "!npc disposition ally", 2]
   ], e[7]);
   gmEditChoices(content, "behave", "Behavior", 254, 78, 56, 60, [
      ["Stay", "!npc behavior stay", "Stay"],
      ["Follow", "!npc behavior follow", "Follow"],
      ["Wander", "!npc behavior wander", "Wander"]
   ], e[9]);

   gmEditChoices(content, "plate", "Plate", 0, 106, 36, 40, [
      ["On", "!npc nameplate on", 1],
      ["Off", "!npc nameplate off", 0]
   ], e[8]);

   gmEditButtons(content, 134, [
      ["Move here", "!npc move"],
      ["Face me", "!npc face"],
      ["Go home", "!npc home"],
      ["Use", "!npc use"],
      ["Despawn", "!npc despawn", 0xC0392B]
   ]);

   var anim = gmInput(content, "anim_txt", 162, 106, 44, 22, Number(e[6]) > 0 ? e[6] : "", "id", function(text)
   {
      gmSend("!npc anim " + text);
   });
   gmText(content, "anim_label", "Anim", 128, 110, 34, 18, 11, GM_TEXT_DIM, true);
   var playButton = gmButton(content, "play_mc", "Play", 209, 106, 38, 22, function()
   {
      gmSend("!npc anim " + gmFieldText(this.field));
   });
   playButton.field = anim;
   var loopButton = gmButton(content, "loop_mc", "Loop", 250, 106, 38, 22, function()
   {
      gmSend("!npc anim " + gmFieldText(this.field) + " loop");
   });
   loopButton.field = anim;
   gmEditField(content, "say_txt", "Say", 296, 106, 28, 140, "", "!npc say");

   // Emotes from the server's "anims" list (4 rows of 6). Looping ones set the NPC's saved idle.
   var i;
   for(i = 0; i < gmAnims.length && i < 24; i++)
   {
      var b = gmButton(content, "emote" + i + "_mc", gmAnims[i][1], (i % 6) * 84, 164 + Math.floor(i / 6) * 21, 80, 19, function()
      {
         gmSend("!npc anim " + this.animId + (this.loop ? " loop" : ""));
      }, gmAnims[i][2] ? 0x2E7D5B : undefined);
      b.animId = gmAnims[i][0];
      b.loop = gmAnims[i][2];
   }
   gmStatus("Editing NPC " + e[1]);
}

// edit|prop|guid|fixtureId|itemId|itemName|scale|tint|x|y|z
function gmRenderEditProp(content, e)
{
   gmText(content, "title_txt", e[4] + "  (item #" + e[3] + ", prop #" + e[2] + ")", 0, 0, 504, 20, 13, GM_TEXT_GOLD, true);
   gmText(content, "where_txt", "at " + e[7] + ", " + e[8] + ", " + e[9], 0, 22, 504, 18, 11, GM_TEXT_DIM, false);

   gmEditField(content, "scale_txt", "Scale", 0, 54, 44, 60, e[5], "!prop scale");
   gmEditField(content, "tint_txt", "Tint", 160, 54, 36, 60, e[6], "!prop tint");

   gmEditButtons(content, 84, [
      ["Move", "!prop move"],
      ["Info", "!prop info"],
      ["Remove", "!prop remove", 0xC0392B]
   ]);
   gmStatus("Editing prop " + e[1]);
}

// --- editor form ---------------------------------------------------------------------
// The server describes every editable field of the selected NPC or prop ("fields" lines, see
// RefereeFieldsHelper); this draws them as grouped cards in two columns, scrollable, with actions,
// emotes and the overhead icon picker below.

// { kind, guid, title, subtitle, list: [{ group, label, type, value, command, options }] }
var gmFields = undefined;
var gmFieldsLoading = undefined;
var gmFieldsScroll = 0;
// Overhead icon catalog: [{ type, icon, state, count, names }], filled by "!npc icon list".
var gmIcons = [];
var gmIconsLoading = [];
var gmIconPicker = false;
var gmIconPage = 0;
var gmIconReplace = false;

var GM_FIELD_ROW = 28;
var GM_FIELD_COL_W = (GM_CONTENT_W - 12) / 2;
var GM_FIELD_LABEL_W = 108;
var GM_ICON_COLUMNS = 3;
var GM_ICON_ROWS = 6;

function gmHandleFields(parts)
{
   if(parts[2] == "begin")
   {
      gmFieldsLoading = { kind: parts[3], guid: parts[4], title: parts[5], subtitle: parts[6], list: [] };
      return;
   }
   if(parts[2] == "f" && gmFieldsLoading != undefined)
   {
      gmFieldsLoading.list.push({ group: parts[3], label: parts[4], type: parts[5], value: parts[6], command: parts[7], options: parts[8] });
      return;
   }
   if(parts[2] == "end" && gmFieldsLoading != undefined)
   {
      if(gmFields == undefined || gmFields.guid != gmFieldsLoading.guid)
      {
         gmFieldsScroll = 0;
         gmIconPicker = false;
      }
      gmFields = gmFieldsLoading;
      gmFieldsLoading = undefined;
      if(_root.gm_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
   }
}

function gmHandleIcons(parts)
{
   if(parts[2] == "begin")
   {
      gmIconsLoading = [];
      return;
   }
   if(parts[2] == "add")
   {
      var i;
      for(i = 3; i < parts.length; i++)
      {
         // type:icon:state:count:example names
         var e = parts[i].split(":");
         gmIconsLoading.push({ type: e[0], icon: e[1], state: e[2], count: e[3], names: e.slice(4).join(":") });
      }
      return;
   }
   if(parts[2] == "end")
   {
      gmIcons = gmIconsLoading;
      if(_root.gm_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
   }
}

// Runs a field's command with the typed text; empty text sends the field's "cleared" value, if any.
function gmFieldSend(f, text)
{
   if(text == "")
   {
      if(f.options == undefined || f.options == "")
      {
         return;
      }
      text = f.options;
   }
   gmSend(frHelperClass.stringReplace(f.command, "{v}", text));
   fscommand("FR_event", "killFocus()");
}

function gmFieldRow(card, f, x, y, w)
{
   gmText(card, "l" + y + "_txt", f.label, x, y + 4, GM_FIELD_LABEL_W, 18, 11, GM_TEXT_DIM, true).wordWrap = false;
   var cx = x + GM_FIELD_LABEL_W;
   var cw = w - GM_FIELD_LABEL_W;
   if(f.type == "text" || f.type == "num")
   {
      var inputW = cw - 46;
      var t = gmInput(card, "v" + y + "_txt", cx, y, inputW, 22, f.value, f.options != "" && f.options != undefined ? "empty = " + f.options : "", function(text)
      {
         gmFieldSend(this.field, text);
      });
      t.field = f;
      var setButton = gmButton(card, "s" + y + "_mc", "Set", cx + inputW + 4, y, 40, 22, function()
      {
         gmFieldSend(this.input.field, gmFieldText(this.input));
      });
      setButton.input = t;
      return;
   }
   if(f.type == "toggle")
   {
      var choices = [["On", "on", "1"], ["Off", "off", "0"]];
      var i;
      for(i = 0; i < choices.length; i++)
      {
         var b = gmButton(card, "t" + y + "_" + i + "_mc", choices[i][0], cx + i * 58, y, 54, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         }, String(f.value) == choices[i][2] ? GM_BLUE_ACTIVE : undefined);
         b.field = f;
         b.arg = choices[i][1];
      }
      return;
   }
   if(f.type == "choice")
   {
      // Label=current value=argument;...
      var options = String(f.options).split(";");
      var bw = Math.min(70, Math.floor((cw - 4 * (options.length - 1)) / options.length));
      var j;
      for(j = 0; j < options.length; j++)
      {
         var o = options[j].split("=");
         var c = gmButton(card, "c" + y + "_" + j + "_mc", o[0], cx + j * (bw + 4), y, bw, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         }, String(f.value) == o[1] ? GM_BLUE_ACTIVE : undefined);
         c.field = f;
         c.arg = o[2];
      }
      return;
   }
   if(f.type == "steps")
   {
      // One button per option, each sending the command with that option.
      var steps = String(f.options).split(";");
      var sw = Math.min(56, Math.floor((cw - 4 * (steps.length - 1)) / steps.length));
      var k;
      for(k = 0; k < steps.length; k++)
      {
         var stepLabel = steps[k] == "reset" ? "Level" : (Number(steps[k]) > 0 ? "+" + steps[k] : steps[k]);
         var st = gmButton(card, "st" + y + "_" + k + "_mc", stepLabel, cx + k * (sw + 4), y, steps[k] == "reset" ? 80 : sw, 22, function()
         {
            gmSend(frHelperClass.stringReplace(this.field.command, "{v}", this.arg));
         });
         st.field = f;
         st.arg = steps[k];
      }
      return;
   }
   if(f.type == "icons")
   {
      var current = f.value == "" ? "none (definition default)" : frHelperClass.stringReplace(String(f.value), ";", ",  ");
      gmText(card, "i" + y + "_txt", current, cx, y + 4, cw, 18, 11, GM_TEXT, false).wordWrap = false;
      gmButton(card, "ipick" + y + "_mc", "Pick icon...", cx, y + GM_FIELD_ROW, 96, 22, function()
      {
         gmIconPicker = true;
         if(gmIcons.length == 0)
         {
            gmSend("!npc icon list");
         }
         gmShowTab("edit");
      }, 0x2E7D5B);
      gmButton(card, "iclear" + y + "_mc", "Clear icons", cx + 100, y + GM_FIELD_ROW, 90, 22, function()
      {
         gmSend("!npc icon clear");
      }, 0xC0392B);
      return;
   }
   gmText(card, "v" + y + "_txt", String(f.value), cx, y + 4, cw, 18, 11, GM_TEXT, false);
}

function gmFieldRows(f)
{
   return f.type == "icons" ? 2 : 1;
}

function gmRenderFields(content)
{
   if(gmIconPicker)
   {
      gmRenderIconPicker(content);
      return;
   }
   gmText(content, "title_txt", (gmFields.kind == "prop" ? "Prop: " : "NPC: ") + gmFields.title, 0, 0, GM_CONTENT_W - 120, 20, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmText(content, "sub_txt", gmFields.subtitle, 0, 20, GM_CONTENT_W - 120, 18, 11, GM_TEXT_DIM, false).wordWrap = false;

   var top = 44;
   var height = GM_CONTENT_H - top;
   var form = content.createEmptyMovieClip("form_mc", content.getNextHighestDepth());
   form._y = top;
   var mask = content.createEmptyMovieClip("mask_mc", content.getNextHighestDepth());
   mask.beginFill(0x000000, 100);
   gmRoundRect(mask, 0, top, GM_CONTENT_W, height, 0);
   mask.endFill();
   form.setMask(mask);

   // Group the fields, keeping their order.
   var groups = [];
   var byName = {};
   var i;
   for(i = 0; i < gmFields.list.length; i++)
   {
      var f = gmFields.list[i];
      if(byName[f.group] == undefined)
      {
         byName[f.group] = { name: f.group, fields: [] };
         groups.push(byName[f.group]);
      }
      byName[f.group].fields.push(f);
   }

   // Field cards fill the shorter of two columns; actions and emotes run full width below.
   var colY = [0, 0];
   var actions = [];
   var g;
   for(g = 0; g < groups.length; g++)
   {
      var group = groups[g];
      if(group.name == "Actions")
      {
         actions = group.fields;
         continue;
      }
      var rows = 0;
      for(i = 0; i < group.fields.length; i++)
      {
         rows += gmFieldRows(group.fields[i]);
      }
      var col = colY[0] <= colY[1] ? 0 : 1;
      var card = form.createEmptyMovieClip("card" + g + "_mc", form.getNextHighestDepth());
      card._x = col * (GM_FIELD_COL_W + 12);
      card._y = colY[col];
      var cardH = 30 + rows * GM_FIELD_ROW;
      gmPanelShape(card, GM_FIELD_COL_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      gmText(card, "h_txt", group.name, 10, 5, GM_FIELD_COL_W - 20, 18, 12, GM_TEXT_GOLD, true);
      var y = 28;
      for(i = 0; i < group.fields.length; i++)
      {
         gmFieldRow(card, group.fields[i], 10, y, GM_FIELD_COL_W - 20);
         y += gmFieldRows(group.fields[i]) * GM_FIELD_ROW;
      }
      colY[col] += cardH + 10;
   }

   var bottom = Math.max(colY[0], colY[1]);
   bottom = gmRenderFieldActions(form, actions, bottom);
   bottom = gmRenderFieldEmotes(form, bottom);

   // Scrolling: arrows, and the mouse wheel over the panel.
   var maxScroll = Math.max(0, bottom - height);
   gmFieldsScroll = Math.max(0, Math.min(gmFieldsScroll, maxScroll));
   form._y = top - gmFieldsScroll;
   if(maxScroll > 0)
   {
      gmButton(content, "up_mc", "Up", GM_CONTENT_W - 110, 6, 52, 24, function()
      {
         gmScrollFields(-140);
      });
      gmButton(content, "down_mc", "Down", GM_CONTENT_W - 54, 6, 52, 24, function()
      {
         gmScrollFields(140);
      });
   }
   content.maxScroll = maxScroll;
   gmStatus("Editing " + gmFields.kind + " " + gmFields.guid + (maxScroll > 0 ? "   (scroll for more)" : ""));
}

function gmScrollFields(delta)
{
   var content = _root.gm_mc.content_mc;
   if(content.form_mc == undefined)
   {
      return;
   }
   gmFieldsScroll = Math.max(0, Math.min(gmFieldsScroll + delta, content.maxScroll));
   content.form_mc._y = 44 - gmFieldsScroll;
}

var gmWheelListener = new Object();
gmWheelListener.onMouseWheel = function(delta)
{
   if(_root.gm_mc._visible && gmTab == "edit" && _root.gm_mc.hitTest(_root._xmouse, _root._ymouse, false))
   {
      gmScrollFields(-delta * 30);
   }
};
Mouse.addListener(gmWheelListener);

function gmRenderFieldActions(form, actions, y)
{
   if(actions.length == 0)
   {
      return y;
   }
   var card = form.createEmptyMovieClip("actions_mc", form.getNextHighestDepth());
   card._y = y;
   var buttons = [];
   var texts = [];
   var i;
   for(i = 0; i < actions.length; i++)
   {
      if(actions[i].type == "action")
      {
         buttons.push(actions[i]);
      }
      else
      {
         texts.push(actions[i]);
      }
   }
   var cardH = 64 + texts.length * GM_FIELD_ROW;
   gmPanelShape(card, GM_CONTENT_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
   gmText(card, "h_txt", "Actions", 10, 5, 200, 18, 12, GM_TEXT_GOLD, true);
   var bw = 100;
   for(i = 0; i < buttons.length; i++)
   {
      var danger = buttons[i].label == "Despawn" || buttons[i].label == "Remove";
      var b = gmButton(card, "a" + i + "_mc", buttons[i].label, 10 + i * (bw + 6), 28, bw, 24, function()
      {
         gmSend(this.command);
      }, danger ? 0xC0392B : undefined);
      b.command = buttons[i].command;
   }
   for(i = 0; i < texts.length; i++)
   {
      gmFieldRow(card, texts[i], 10, 60 + i * GM_FIELD_ROW, GM_CONTENT_W - 20);
   }
   return y + cardH + 10;
}

// Emote buttons from the server's "anims" list; looping ones (green) become the saved idle.
function gmRenderFieldEmotes(form, y)
{
   if(gmAnims.length == 0)
   {
      return y;
   }
   var perRow = 6;
   var rows = Math.ceil(gmAnims.length / perRow);
   var card = form.createEmptyMovieClip("emotes_mc", form.getNextHighestDepth());
   card._y = y;
   var cardH = 32 + rows * 26;
   gmPanelShape(card, GM_CONTENT_W, cardH, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
   gmText(card, "h_txt", "Emotes (green loop as the idle)", 10, 5, 300, 18, 12, GM_TEXT_GOLD, true);
   var bw = Math.floor((GM_CONTENT_W - 20 - (perRow - 1) * 6) / perRow);
   var i;
   for(i = 0; i < gmAnims.length; i++)
   {
      var b = gmButton(card, "e" + i + "_mc", gmAnims[i][1], 10 + (i % perRow) * (bw + 6), 28 + Math.floor(i / perRow) * 26, bw, 22, function()
      {
         gmSend("!npc anim " + this.animId + (this.loop ? " loop" : ""));
      }, gmAnims[i][2] ? 0x2E7D5B : undefined);
      b.animId = gmAnims[i][0];
      b.loop = gmAnims[i][2];
   }
   return y + cardH + 10;
}

// Overhead icons the game's NPCs use, labelled with NPCs that show them; or type one in.
function gmRenderIconPicker(content)
{
   gmText(content, "title_txt", "Overhead icon for " + gmFields.title, 0, 0, 360, 20, 14, GM_TEXT_GOLD, true).wordWrap = false;
   gmButton(content, "back_mc", "Back", GM_CONTENT_W - 60, 0, 60, 24, function()
   {
      gmIconPicker = false;
      gmShowTab("edit");
   });
   gmButton(content, "mode_mc", gmIconReplace ? "Mode: replace" : "Mode: add", GM_CONTENT_W - 180, 0, 116, 24, function()
   {
      gmIconReplace = !gmIconReplace;
      gmShowTab("edit");
   });

   // Manual entry.
   gmText(content, "manual_txt", "Type / icon / state", 0, 32, 130, 18, 11, GM_TEXT_DIM, true);
   gmInput(content, "itype_txt", 130, 28, 50, 22, "", "type");
   gmInput(content, "iicon_txt", 184, 28, 60, 22, "", "icon");
   gmInput(content, "istate_txt", 248, 28, 50, 22, "", "state");
   gmButton(content, "iadd_mc", "Apply", 302, 28, 60, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var state = gmFieldText(c.istate_txt);
      gmIconApply(gmFieldText(c.itype_txt), gmFieldText(c.iicon_txt), state == "" ? "0" : state);
   }, 0x2E7D5B);

   if(gmIcons.length == 0)
   {
      gmText(content, "loading_txt", "Loading the icon list...", 0, 64, 400, 18, 12, GM_TEXT_DIM, false);
      return;
   }
   var perPage = GM_ICON_COLUMNS * GM_ICON_ROWS;
   var pages = Math.ceil(gmIcons.length / perPage);
   gmIconPage = Math.max(0, Math.min(gmIconPage, pages - 1));
   gmButton(content, "iprev_mc", "<", 380, 28, 28, 22, function()
   {
      gmIconPage--;
      gmShowTab("edit");
   });
   gmText(content, "ipage_txt", "Page " + (gmIconPage + 1) + " / " + pages, 410, 31, 96, 18, 11, GM_TEXT, true, "center");
   gmButton(content, "inext_mc", ">", 508, 28, 28, 22, function()
   {
      gmIconPage++;
      gmShowTab("edit");
   });

   var cellW = Math.floor((GM_CONTENT_W - (GM_ICON_COLUMNS - 1) * 6) / GM_ICON_COLUMNS);
   var i;
   for(i = 0; i < perPage && gmIconPage * perPage + i < gmIcons.length; i++)
   {
      var e = gmIcons[gmIconPage * perPage + i];
      var cell = content.createEmptyMovieClip("icon" + i + "_mc", content.getNextHighestDepth());
      cell._x = (i % GM_ICON_COLUMNS) * (cellW + 6);
      cell._y = 60 + Math.floor(i / GM_ICON_COLUMNS) * 56;
      gmPanelShape(cell, cellW, 52, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      gmText(cell, "a_txt", "Type " + e.type + "  icon " + e.icon + "  state " + e.state + "   (" + e.count + ")", 6, 2, cellW - 12, 16, 11, GM_TEXT, true).wordWrap = false;
      gmText(cell, "b_txt", e.names == "" ? "(unnamed NPCs)" : e.names, 6, 18, cellW - 12, 32, 10, GM_TEXT_DIM, false);
      cell.entry = e;
      cell.w = cellW;
      cell.onRelease = function()
      {
         gmIconApply(this.entry.type, this.entry.icon, this.entry.state);
      };
      cell.onRollOver = function()
      {
         gmPanelShape(this, this.w, 52, 8, GM_BLUE_ACTIVE, GM_BLUE_BOTTOM, GM_GOLD, 1);
      };
      cell.onRollOut = function()
      {
         gmPanelShape(this, this.w, 52, 8, GM_BLUE_CELL, GM_BLUE_BOTTOM, GM_GOLD_DARK, 1);
      };
   }
   gmStatus(gmIcons.length + " icons used by the game's NPCs; the count is how many NPCs show it");
}

function gmIconApply(type, icon, state)
{
   if(type == "" || icon == "")
   {
      return;
   }
   gmSend("!npc icon " + (gmIconReplace ? "set " : "add ") + type + " " + icon + " " + state);
   gmIconPicker = false;
}

// --- text escaping (scripts, conversations, dialog) -----------------------------
// Free text travels as: '\' -> "\\", newline -> "\n", '|' -> "\p", tab -> "\t", and the spaces
// chat would trim or collapse (leading, trailing, or next to another space) -> "\s".

function gmEscapeText(s)
{
   var out = "";
   var i;
   for(i = 0; i < s.length; i++)
   {
      var c = s.charAt(i);
      if(c == "\\")
      {
         out += "\\\\";
      }
      else if(c == "\r" || c == "\n")
      {
         // Flash text fields use \r; treat \r\n as one line break.
         if(c == "\r" && s.charAt(i + 1) == "\n")
         {
            i++;
         }
         out += "\\n";
      }
      else if(c == "|")
      {
         out += "\\p";
      }
      else if(c == "\t")
      {
         out += "\\t";
      }
      else if(c == " " && (i == 0 || i == s.length - 1 || s.charAt(i - 1) == " " || s.charAt(i + 1) == " "))
      {
         // Chat trims leading/trailing spaces and collapses runs of them, so those travel as \s.
         out += "\\s";
      }
      else
      {
         out += c;
      }
   }
   return out;
}

function gmUnescapeText(s)
{
   var out = "";
   var i;
   for(i = 0; i < s.length; i++)
   {
      var c = s.charAt(i);
      if(c == "\\" && i + 1 < s.length)
      {
         var n = s.charAt(i + 1);
         i++;
         if(n == "n")
         {
            out += "\r";
         }
         else if(n == "p")
         {
            out += "|";
         }
         else if(n == "s")
         {
            out += " ";
         }
         else if(n == "t")
         {
            out += "\t";
         }
         else
         {
            out += n;
         }
      }
      else
      {
         out += c;
      }
   }
   return out;
}

// Sends commands one at a time so the chat channel isn't flooded.
var gmSendQueue = [];
var gmSendTimer = undefined;
function gmQueueSend(cmd)
{
   gmSendQueue.push(cmd);
   if(gmSendTimer == undefined)
   {
      gmSendTimer = setInterval(function()
      {
         if(gmSendQueue.length == 0)
         {
            clearInterval(gmSendTimer);
            gmSendTimer = undefined;
            return;
         }
         gmSend(String(gmSendQueue.shift()));
      }, 150);
   }
}

// --- script tab ------------------------------------------------------------------
// Left: the shared script library (templates first). Right: the editor for the selected
// NPC/prop's script, or a library entry. Templates take parameters through a small form.

// Selected entity kind for "!npc script" / "!prop script": the editor's selection.
function gmScriptKind()
{
   return gmEdit[0] == "prop" ? "prop" : "npc";
}

// guid is the entity guid, or "lib:<name>" when the editor holds a library entry.
var gmScript = { kind: "", guid: "", source: "", loading: "", status: "" };

// Library entries: { name, template, description, params: [[key, type, default], ...] }.
var gmScriptLib = [];
var gmScriptLibLoading = [];
var gmScriptLibSel = "";
var gmScriptLibAsked = false;
var gmScriptForm = false;
var GM_LIB_ROWS = 8;

var GM_SCRIPT_HELP = "Hooks: onSpawn()  onSecond()  onInteract(player)  onProximity(player)  onChoice(player, hook)\r" +
   "npc: guid name position  say(text)  sayLocalized(id)  moveTo(x,y,z[,direct])  emote(animId[,loop])  face(player)  startConversation(player[,nodeId])\r" +
   "player: name guid position  message(text)  giveItem(itemId[,qty])  acceptQuest(id)  hasCompletedQuest(id)\r" +
   "Lua: math string table pairs ipairs next select type tostring tonumber pcall error assert unpack print.  16 KB, 100 ms per hook.\r" +
   "Example:\r  function onInteract(player)\r      npc:say(\"Hello, \" .. player.name)\r  end";

// Every space escaped, for values passed as key=value arguments.
function gmEscapeArg(s)
{
   return frHelperClass.stringReplace(gmEscapeText(String(s)), " ", "\\s");
}

function gmScriptLibEntry(name)
{
   var i;
   for(i = 0; i < gmScriptLib.length; i++)
   {
      if(gmScriptLib[i].name == name)
      {
         return gmScriptLib[i];
      }
   }
   return undefined;
}

function gmRenderScript(content)
{
   if(!gmScriptLibAsked)
   {
      gmScriptLibAsked = true;
      gmSend("!scriptlib list");
   }

   gmRenderScriptLibrary(content);

   var x = 182;
   var hasEntity = gmEdit[0] == "npc" || gmEdit[0] == "prop";
   var kind = gmScriptKind();
   var isLib = gmScript.guid.substr(0, 4) == "lib:";
   var title = isLib ? "Library: " + gmScript.guid.substr(4)
      : hasEntity ? "Script for " + kind + " " + gmEdit[1] + (gmScript.guid == gmEdit[1] ? "" : " (press Load)")
      : "No NPC or prop selected (god mode, then click one)";
   gmText(content, "title_txt", title, x, 0, 322, 20, 12, GM_TEXT_GOLD, true).wordWrap = false;

   if(gmScriptForm)
   {
      gmRenderScriptForm(content, x);
   }
   else
   {
      gmRenderScriptEditor(content, x, hasEntity, kind, isLib);
   }

   gmText(content, "result_txt", gmScript.status, x, 226, 322, 20, 10, GM_TEXT_GOLD, false);
   gmStatus("Script editor");
}

function gmRenderScriptLibrary(content)
{
   gmText(content, "lib_label", "Library", 0, 0, 100, 18, 12, GM_TEXT_GOLD, true);
   gmButton(content, "libreload_mc", "Refresh", 112, 0, 62, 20, function()
   {
      gmSend("!scriptlib list");
   });
   var i;
   for(i = 0; i < GM_LIB_ROWS && i < gmScriptLib.length; i++)
   {
      var entry = gmScriptLib[i];
      var selected = entry.name == gmScriptLibSel;
      var row = content.createEmptyMovieClip("lib" + i + "_mc", content.getNextHighestDepth());
      row._y = 24 + i * 25;
      gmPanelShape(row, 174, 22, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_GOLD : GM_GOLD_DARK, 1);
      gmText(row, "label_txt", (entry.template ? "[T] " : "") + entry.name, 6, 2, 164, 18, 11, GM_TEXT, selected).wordWrap = false;
      row.entryName = entry.name;
      row.onRelease = function()
      {
         gmScriptLibSel = this.entryName;
         gmScriptForm = false;
         gmShowTab("script");
      };
   }
   if(gmScriptLib.length == 0)
   {
      gmText(content, "libempty_txt", "No library entries yet.", 0, 26, 174, 40, 11, GM_TEXT_DIM, false);
   }

   var sel = gmScriptLibEntry(gmScriptLibSel);
   if(sel == undefined)
   {
      return;
   }
   gmText(content, "libdesc_txt", sel.description, 0, 226, 174, 20, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmButton(content, "libuse_mc", "Use on NPC", 0, 230 - 26, 84, 20, function()
   {
      var e = gmScriptLibEntry(gmScriptLibSel);
      if(e.params.length > 0)
      {
         gmScriptForm = true;
         gmShowTab("script");
         return;
      }
      gmSend("!" + gmScriptKind() + " script use " + e.name);
   }, 0x2E7D5B);
   gmButton(content, "libopen_mc", "Open", 88, 230 - 26, 42, 20, function()
   {
      gmSend("!scriptlib show " + gmScriptLibSel);
   });
   if(!sel.template)
   {
      gmButton(content, "libdel_mc", "Del", 134, 230 - 26, 40, 20, function()
      {
         gmSend("!scriptlib delete " + gmScriptLibSel);
      }, 0xC0392B);
   }
}

function gmRenderScriptEditor(content, x, hasEntity, kind, isLib)
{
   var area = content.createEmptyMovieClip("area_mc", content.getNextHighestDepth());
   area._x = x;
   area._y = 22;
   area.lineStyle(2, GM_GOLD, 100);
   area.beginFill(0x0B1E3A, 100);
   gmRoundRect(area, 0, 0, 322, 154, 8);
   area.endFill();
   content.createTextField("source_txt", content.getNextHighestDepth(), x + 6, 26, 310, 146);
   var t = content.source_txt;
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.multiline = true;
   t.wordWrap = true;
   t.maxChars = 16000;
   t.setNewTextFormat(gmFormat(11, GM_TEXT, false));
   t.text = (isLib || gmScript.guid == gmEdit[1]) ? gmScript.source : "";
   area.field = t;
   area.onPress = function()
   {
      gmFocusInput(this.field);
   };

   if(hasEntity)
   {
      gmButton(content, "load_mc", "Load", x, 180, 50, 20, function()
      {
         gmSend("!" + gmScriptKind() + " script show");
      });
      gmButton(content, "save_mc", "Save to " + kind, x + 54, 180, 84, 20, function()
      {
         gmSaveScript("!" + gmScriptKind() + " script", "commit", gmEdit[1]);
      }, 0x2E7D5B);
      gmButton(content, "test_mc", "Test", x + 142, 180, 44, 20, function()
      {
         gmSend("!" + gmScriptKind() + " script test");
      });
      gmButton(content, "clear_mc", "Remove", x + 190, 180, 58, 20, function()
      {
         gmSend("!" + gmScriptKind() + " script clear");
      }, 0xC0392B);
   }
   gmButton(content, "help_mc", "API help", x + 252, 180, 70, 20, function()
   {
      var h = _root.gm_mc.content_mc.help_txt;
      h._visible = !h._visible;
   });

   // Save the editor text to the library under a name.
   gmInput(content, "libname_txt", x, 204, 160, 20, isLib ? gmScript.guid.substr(4) : "", "library name");
   gmButton(content, "libsave_mc", "Save to library", x + 164, 204, 110, 20, function()
   {
      var name = gmFieldText(_root.gm_mc.content_mc.libname_txt);
      if(name == "")
      {
         gmStatus("Type a library name first.");
         return;
      }
      gmSaveScript("!scriptlib", "commit " + name, "lib:" + name);
   });

   gmText(content, "help_txt", GM_SCRIPT_HELP, x, 22, 322, 154, 10, GM_TEXT, false);
   var help = content.help_txt;
   help.background = true;
   help.backgroundColor = 0x0B1E3A;
   help._visible = false;
}

// Parameter form for a template: one input per parameter, then apply to the selected NPC/prop.
function gmRenderScriptForm(content, x)
{
   var e = gmScriptLibEntry(gmScriptLibSel);
   gmText(content, "form_label", "Use \"" + e.name + "\" on the selected " + gmScriptKind(), x, 24, 322, 18, 11, GM_TEXT, true);
   var i;
   for(i = 0; i < e.params.length && i < 6; i++)
   {
      var p = e.params[i];
      gmText(content, "param" + i + "_label", p[0] + " (" + p[1] + ")", x, 48 + i * 26 + 3, 110, 18, 10, GM_TEXT_DIM, true).wordWrap = false;
      gmInput(content, "param" + i + "_txt", x + 112, 48 + i * 26, 210, 20, p[2], "");
   }
   gmButton(content, "apply_mc", "Apply", x, 206, 70, 20, function()
   {
      var e2 = gmScriptLibEntry(gmScriptLibSel);
      var cmd = "!" + gmScriptKind() + " script use " + e2.name;
      var j;
      for(j = 0; j < e2.params.length && j < 6; j++)
      {
         cmd += " " + e2.params[j][0] + "=" + gmEscapeArg(_root.gm_mc.content_mc["param" + j + "_txt"].text);
      }
      gmSend(cmd);
      gmScriptForm = false;
   }, 0x2E7D5B);
   gmButton(content, "cancel_mc", "Back", x + 74, 206, 60, 20, function()
   {
      gmScriptForm = false;
      gmShowTab("script");
   });
}

// Sends the editor text in escaped chunks: "<prefix> append <chunk>" ..., then "<prefix> <commit>".
function gmSaveScript(prefix, commit, target)
{
   var t = _root.gm_mc.content_mc.source_txt;
   if(t == undefined)
   {
      return;
   }
   var source = String(t.text);
   gmScript.source = source;
   gmScript.guid = target;
   // Escape per chunk so no escape sequence is split; each stays well under the chat line limit.
   var i;
   for(i = 0; i < source.length; i += 80)
   {
      gmQueueSend(prefix + " append " + gmEscapeText(source.substr(i, 80)));
   }
   gmQueueSend(prefix + " " + commit);
   gmScript.status = "Saving...";
   _root.gm_mc.content_mc.result_txt.text = gmScript.status;
   fscommand("FR_event", "killFocus()");
}

// scriptlib|begin, scriptlib|entry|<name>|<template>|<description>|<key:type:default;...>, scriptlib|end
function gmHandleScriptLib(parts)
{
   var step = parts[2];
   if(step == "begin")
   {
      gmScriptLibLoading = [];
      return;
   }
   if(step == "entry")
   {
      // One field per parameter: <key>:<type>:<escaped default> (the default may contain ':').
      var params = [];
      var i;
      for(i = 6; i < parts.length; i++)
      {
         if(String(parts[i]) == "")
         {
            continue;
         }
         var bits = String(parts[i]).split(":");
         params.push([bits[0], bits[1], gmUnescapeText(bits.slice(2).join(":"))]);
      }
      gmScriptLibLoading.push({ name: parts[3], template: parts[4] == "1", description: gmUnescapeText(parts[5]), params: params });
      return;
   }
   if(step == "end")
   {
      gmScriptLib = gmScriptLibLoading;
      if(_root.gm_mc._visible && gmTab == "script")
      {
         gmShowTab("script");
      }
   }
}

// --- conversation tab ------------------------------------------------------------
// Click-built: NPC lines are cards; answers lead to a new line, the end, or an existing line.
// Node ids are picked here (highest + 1), so nothing has to be typed but the text.

var gmConv = { guid: "", start: 1, nodes: [], choices: [] };
var gmConvSel = 0;
var gmConvScroll = 0;
var gmConvLinkFrom = undefined;
var GM_CONV_CARDS = 8;

function gmOneLine(s, max)
{
   var r = frHelperClass.stringReplace(String(s), "\r", " / ");
   return r.length > max ? r.substr(0, max - 2) + "..." : r;
}

function gmConvNode(id)
{
   var i;
   for(i = 0; i < gmConv.nodes.length; i++)
   {
      if(String(gmConv.nodes[i].id) == String(id))
      {
         return gmConv.nodes[i];
      }
   }
   return undefined;
}

function gmConvAnswers(id)
{
   var answers = [];
   var i;
   for(i = 0; i < gmConv.choices.length; i++)
   {
      if(String(gmConv.choices[i].node) == String(id))
      {
         answers.push(gmConv.choices[i]);
      }
   }
   return answers;
}

function gmConvNextId()
{
   var next = 1;
   var i;
   for(i = 0; i < gmConv.nodes.length; i++)
   {
      next = Math.max(next, Number(gmConv.nodes[i].id) + 1);
   }
   return next;
}

function gmConvTarget(next)
{
   if(String(next) == "0")
   {
      return "ends";
   }
   var node = gmConvNode(next);
   return node == undefined ? "line " + next : "\"" + gmOneLine(node.text, 22) + "\"";
}

function gmRenderConv(content)
{
   if(gmEdit[0] != "npc")
   {
      gmText(content, "none_txt", "Select an NPC first (god mode, then click it). Its conversation is authored here.", 0, 0, 500, 40, 12, GM_TEXT_DIM, false);
      gmStatus("Conversation");
      return;
   }

   var loaded = gmConv.guid == gmEdit[1];
   if(!loaded)
   {
      gmText(content, "load_txt", "Loading conversation...", 0, 0, 300, 20, 12, GM_TEXT_DIM, false);
      gmSend("!conv show");
      return;
   }

   if(gmConvNode(gmConvSel) == undefined)
   {
      gmConvSel = gmConv.nodes.length > 0 ? gmConv.nodes[0].id : 0;
   }

   gmText(content, "title_txt", (gmEdit[2] == "" ? "NPC" : gmEdit[2]) + " - conversation", 0, 2, 250, 20, 12, GM_TEXT_GOLD, true);
   gmButton(content, "newline_mc", "+ New line", 256, 0, 80, 22, function()
   {
      var id = gmConvNextId();
      gmConvSel = id;
      gmSend("!conv node " + id + " " + gmEscapeText("(new line)"));
   }, 0x2E7D5B);
   gmButton(content, "test_mc", "Test", 340, 0, 50, 22, function()
   {
      gmSend("!conv test");
   });
   gmButton(content, "clear_mc", "Clear all", 394, 0, 66, 22, function()
   {
      gmSend("!conv clear");
   }, 0xC0392B);
   gmButton(content, "reload_mc", "Reload", 464, 0, 40, 22, function()
   {
      gmSend("!conv show");
   });

   gmRenderConvCards(content);
   gmRenderConvEditor(content);
   gmStatus(gmConvLinkFrom != undefined ? "Click the line this answer should lead to." : gmConv.nodes.length + " line(s). The player sees the starred one first.");
}

// Left column: one card per NPC line.
function gmRenderConvCards(content)
{
   var list = content.createEmptyMovieClip("cards_mc", content.getNextHighestDepth());
   list._y = 28;
   gmConvScroll = Math.max(0, Math.min(gmConvScroll, gmConv.nodes.length - GM_CONV_CARDS));
   var i;
   for(i = 0; i < GM_CONV_CARDS && gmConvScroll + i < gmConv.nodes.length; i++)
   {
      var node = gmConv.nodes[gmConvScroll + i];
      var selected = String(node.id) == String(gmConvSel);
      var card = list.createEmptyMovieClip("card" + i, 10 + i);
      card._y = i * 27;
      gmPanelShape(card, 180, 24, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_GOLD : GM_GOLD_DARK, 1);
      var star = String(node.id) == String(gmConv.start) ? "* " : "";
      gmText(card, "label_txt", star + gmOneLine(node.text, 26), 6, 3, 170, 18, 11, GM_TEXT, selected).wordWrap = false;
      card.nodeId = node.id;
      card.onRelease = function()
      {
         if(gmConvLinkFrom != undefined)
         {
            gmQueueSend("!conv choice add " + gmConvLinkFrom.node + " " + this.nodeId + " " + gmEscapeText(gmConvLinkFrom.text));
            gmConvLinkFrom = undefined;
            return;
         }
         gmConvSel = this.nodeId;
         gmShowTab("conv");
      };
   }
   if(gmConv.nodes.length > GM_CONV_CARDS)
   {
      gmButton(content, "up_mc", "^", 184, 28, 20, 104, function()
      {
         gmConvScroll -= GM_CONV_CARDS - 1;
         gmShowTab("conv");
      });
      gmButton(content, "down_mc", "v", 184, 136, 20, 104, function()
      {
         gmConvScroll += GM_CONV_CARDS - 1;
         gmShowTab("conv");
      });
   }
   if(gmConv.nodes.length == 0)
   {
      gmText(content, "empty_txt", "No lines yet. Press + New line.", 0, 32, 200, 40, 11, GM_TEXT_DIM, false);
   }
}

// Right column: the selected line's text and its answers.
function gmRenderConvEditor(content)
{
   var node = gmConvNode(gmConvSel);
   if(node == undefined)
   {
      return;
   }
   var x = 210;

   gmText(content, "says_label", "NPC says", x, 30, 80, 18, 11, GM_TEXT_DIM, true);
   var box = content.createEmptyMovieClip("says_box", content.getNextHighestDepth());
   box._x = x;
   box._y = 48;
   box.lineStyle(2, GM_GOLD, 100);
   box.beginFill(0x0B1E3A, 100);
   gmRoundRect(box, 0, 0, 294, 46, 8);
   box.endFill();
   content.createTextField("says_txt", content.getNextHighestDepth(), x + 6, 51, 282, 42);
   var says = content.says_txt;
   says.embedFonts = true;
   says.type = "input";
   says.multiline = true;
   says.wordWrap = true;
   says.maxChars = 400;
   says.setNewTextFormat(gmFormat(11, GM_TEXT, false));
   says.text = node.text;
   box.field = says;
   box.onPress = function()
   {
      gmFocusInput(this.field);
   };

   gmButton(content, "savetext_mc", "Save text", x, 98, 70, 20, function()
   {
      gmSend("!conv node " + gmConvSel + " " + gmEscapeText(String(_root.gm_mc.content_mc.says_txt.text)));
   });
   gmButton(content, "start_mc", "Make first", x + 74, 98, 72, 20, function()
   {
      gmSend("!conv start " + gmConvSel);
   });
   gmButton(content, "delline_mc", "Delete line", x + 150, 98, 76, 20, function()
   {
      gmSend("!conv node del " + gmConvSel);
   }, 0xC0392B);

   gmText(content, "answers_label", "Player answers", x, 122, 120, 18, 11, GM_TEXT_DIM, true);
   var answers = gmConvAnswers(gmConvSel);
   var i;
   for(i = 0; i < answers.length && i < 3; i++)
   {
      var a = answers[i];
      var y = 140 + i * 22;
      var extra = (Number(a.give) > 0 ? " +item " + a.give : "") + (a.hook != "" ? " +script " + a.hook : "");
      gmText(content, "answer" + i + "_txt", "\"" + gmOneLine(a.text, 20) + "\" " + gmConvTarget(a.next) + extra, x, y + 2, 250, 18, 10, GM_TEXT, false).wordWrap = false;
      var del = gmButton(content, "answerdel" + i + "_mc", "x", x + 274, y, 20, 19, function()
      {
         gmSend("!conv choice del " + gmConvSel + " " + this.index);
      }, 0xC0392B);
      del.index = a.index;
   }
   if(answers.length > 3)
   {
      gmText(content, "more_txt", "+" + (answers.length - 3) + " more", x + 200, 122, 94, 18, 10, GM_TEXT_DIM, false, "right");
   }

   // New answer: type it, then pick where it leads.
   gmInput(content, "answer_txt", x, 208, 294, 20, "", "New answer the player can pick...");
   gmButton(content, "tonew_mc", "-> new line", x, 230, 80, 18, function()
   {
      var text = String(_root.gm_mc.content_mc.answer_txt.text);
      var id = gmConvNextId();
      gmQueueSend("!conv node " + id + " " + gmEscapeText("(new line)"));
      gmQueueSend("!conv choice add " + gmConvSel + " " + id + " " + gmEscapeText(text == "" ? "..." : text));
      gmConvSel = id;
   }, 0x2E7D5B);
   gmButton(content, "toend_mc", "-> ends", x + 84, 230, 60, 18, function()
   {
      var text = String(_root.gm_mc.content_mc.answer_txt.text);
      gmSend("!conv choice add " + gmConvSel + " 0 " + gmEscapeText(text == "" ? "Goodbye" : text));
   });
   gmButton(content, "tolink_mc", "-> link...", x + 148, 230, 70, 18, function()
   {
      var text = String(_root.gm_mc.content_mc.answer_txt.text);
      gmConvLinkFrom = { node: gmConvSel, text: text == "" ? "..." : text };
      gmStatus("Click the line this answer should lead to.");
   });
}

// --- quests tab ------------------------------------------------------------------
// Custom quests (ids 900000+) built on the quest system. The quest window shows generic
// locale text; the custom title, description and goal text reach the player as chat.

var gmQuestList = [];
var gmQuestListLoading = [];
var gmQuestListAsked = false;
var gmQuest = undefined;
var gmQuestLoading = undefined;
var gmQuestSel = 0;
var gmQuestNodes = [];
var gmQuestScroll = 0;
var GM_QUEST_ROWS = 7;

function gmRenderQuests(content)
{
   if(!gmQuestListAsked)
   {
      gmQuestListAsked = true;
      gmSend("!questedit list");
      gmSend("!questedit nodes");
   }

   // Left: quest list and "new".
   gmText(content, "quests_label", "Custom quests", 0, 0, 120, 18, 12, GM_TEXT_GOLD, true);
   gmButton(content, "qreload_mc", "Refresh", 120, 0, 54, 20, function()
   {
      gmSend("!questedit list");
   });
   gmQuestScroll = Math.max(0, Math.min(gmQuestScroll, gmQuestList.length - GM_QUEST_ROWS));
   var i;
   for(i = 0; i < GM_QUEST_ROWS && gmQuestScroll + i < gmQuestList.length; i++)
   {
      var q = gmQuestList[gmQuestScroll + i];
      var selected = String(q.id) == String(gmQuestSel);
      var row = content.createEmptyMovieClip("q" + i + "_mc", content.getNextHighestDepth());
      row._y = 24 + i * 24;
      gmPanelShape(row, 174, 21, 6, selected ? GM_BLUE_ACTIVE : GM_BLUE_CELL, GM_BLUE_BOTTOM, selected ? GM_GOLD : GM_GOLD_DARK, 1);
      gmText(row, "label_txt", (q.published ? "" : "(draft) ") + gmOneLine(q.title, 24), 6, 2, 164, 18, 10, GM_TEXT, selected).wordWrap = false;
      row.questId = q.id;
      row.onRelease = function()
      {
         gmQuestSel = this.questId;
         gmSend("!questedit show " + this.questId);
      };
   }
   if(gmQuestList.length > GM_QUEST_ROWS)
   {
      gmButton(content, "qdown_mc", "more...", 0, 24 + GM_QUEST_ROWS * 24, 174, 18, function()
      {
         gmQuestScroll = gmQuestScroll + GM_QUEST_ROWS >= gmQuestList.length ? 0 : gmQuestScroll + GM_QUEST_ROWS;
         gmShowTab("quests");
      });
   }
   gmInput(content, "qnew_txt", 0, 210, 120, 20, "", "New quest title");
   gmButton(content, "qnew_mc", "+ New", 124, 210, 50, 20, function()
   {
      var title = String(_root.gm_mc.content_mc.qnew_txt.text);
      gmSend("!questedit new " + gmEscapeText(title == "" ? "New quest" : title));
   }, 0x2E7D5B);

   if(gmQuest == undefined || String(gmQuest.id) != String(gmQuestSel))
   {
      gmText(content, "qhint_txt", "Pick a quest on the left, or create one. Its title, description and goal text are sent to the player in chat; the quest window itself shows generic text.", 184, 26, 320, 80, 11, GM_TEXT_DIM, false);
      gmStatus("Quest authoring");
      return;
   }
   gmRenderQuestEditor(content, 184);
}

// Field with a Set button that sends "!questedit set <id> <what> <value>".
function gmQuestField(content, name, label, x, y, labelW, inputW, value, what, isText)
{
   gmText(content, name + "_label", label, x, y + 3, labelW, 18, 10, GM_TEXT_DIM, true);
   var t = gmInput(content, name, x + labelW, y, inputW, 20, value, "", function(text)
   {
      gmQuestSet(this.what, this.isText, text);
   });
   t.what = what;
   t.isText = isText;
   var b = gmButton(content, name + "_set", "Set", x + labelW + inputW + 2, y, 30, 20, function()
   {
      gmQuestSet(this.field.what, this.field.isText, String(this.field.text));
   });
   b.field = t;
}

function gmQuestSet(what, isText, text)
{
   gmSend("!questedit set " + gmQuest.id + " " + what + " " + (isText ? gmEscapeText(text) : (text == "" ? "0" : text)));
}

function gmRenderQuestEditor(content, x)
{
   var q = gmQuest;
   gmText(content, "qtitle_txt", "#" + q.id + (q.published ? "  published" : "  draft"), x, 0, 150, 18, 11, q.published ? 0x7CFC9A : GM_TEXT_GOLD, true);
   gmButton(content, "qpub_mc", q.published ? "Unpublish" : "Publish", x + 150, 0, 70, 20, function()
   {
      gmSend("!questedit " + (gmQuest.published ? "unpublish " : "publish ") + gmQuest.id);
   }, q.published ? undefined : 0x2E7D5B);
   gmButton(content, "qdel_mc", "Delete", x + 224, 0, 54, 20, function()
   {
      gmSend("!questedit delete " + gmQuest.id);
   }, 0xC0392B);

   gmQuestField(content, "qt", "Title", x, 22, 34, 248, q.title, "title", true);
   gmQuestField(content, "qd", "Text", x, 44, 34, 248, q.desc, "desc", true);
   gmQuestField(content, "qg", "Giver", x, 66, 34, 52, q.giver, "giver", false);
   gmQuestField(content, "qi", "Turn-in", x + 122, 66, 46, 52, q.turnin, "turnin", false);
   gmQuestField(content, "qc", "Coins", x, 88, 34, 52, q.coins, "coins", false);
   gmQuestField(content, "qp", "After", x + 122, 88, 46, 52, q.prereq == "0" ? "" : q.prereq, "prereq", false);

   // Goals, in order.
   gmText(content, "goals_label", "Goals (in order)", x, 112, 120, 18, 10, GM_TEXT_DIM, true);
   var i;
   for(i = 0; i < q.goals.length && i < 4; i++)
   {
      var g = q.goals[i];
      var y = 128 + i * 19;
      var what = g.type == "talk" ? "talk to NPC " + g.a
         : g.type == "collect" ? "collect " + g.b + " x " + g.a
         : g.type == "reach" ? "reach " + Math.round(Number(g.b)) + ", " + Math.round(Number(g.d)) : g.type;
      gmText(content, "goal" + i + "_txt", (Number(g.index) + 1) + ". " + what + (g.text != "" ? " - " + gmOneLine(g.text, 18) : ""), x, y + 1, 226, 18, 10, GM_TEXT, false).wordWrap = false;
      var up = gmButton(content, "gup" + i + "_mc", "^", x + 228, y, 16, 17, function()
      {
         gmSend("!questedit goal up " + gmQuest.id + " " + this.index);
      });
      up.index = g.index;
      var down = gmButton(content, "gdn" + i + "_mc", "v", x + 246, y, 16, 17, function()
      {
         gmSend("!questedit goal down " + gmQuest.id + " " + this.index);
      });
      down.index = g.index;
      var del = gmButton(content, "gdel" + i + "_mc", "x", x + 264, y, 16, 17, function()
      {
         gmSend("!questedit goal del " + gmQuest.id + " " + this.index);
      }, 0xC0392B);
      del.index = g.index;
   }
   if(q.goals.length > 4)
   {
      gmText(content, "gmore_txt", "+" + (q.goals.length - 4) + " more", x + 200, 112, 80, 18, 10, GM_TEXT_DIM, false, "right");
   }

   // Add a goal: one input, three kinds.
   gmInput(content, "gval_txt", x, 206, 92, 20, "", "NPC id / node key");
   gmInput(content, "gcount_txt", x + 96, 206, 30, 20, "", "n");
   gmButton(content, "gtalk_mc", "+ Talk", x + 130, 206, 48, 20, function()
   {
      gmSend("!questedit goal add " + gmQuest.id + " talk " + gmFieldText(_root.gm_mc.content_mc.gval_txt));
   });
   gmButton(content, "gcollect_mc", "+ Collect", x + 182, 206, 60, 20, function()
   {
      var c = _root.gm_mc.content_mc;
      var count = gmFieldText(c.gcount_txt);
      gmSend("!questedit goal add " + gmQuest.id + " collect " + gmFieldText(c.gval_txt) + " " + (count == "" ? "1" : count));
   });
   gmButton(content, "greach_mc", "+ Reach here", x + 246, 206, 74, 20, function()
   {
      gmSend("!questedit goal add " + gmQuest.id + " reach");
   });

   // Rewards.
   var rewards = "";
   for(i = 0; i < q.rewards.length; i++)
   {
      rewards += (i > 0 ? ", " : "") + q.rewards[i][0] + " x" + q.rewards[i][1];
   }
   gmText(content, "rewards_txt", "Rewards: " + (rewards == "" ? "none" : gmOneLine(rewards, 40)), x, 230, 190, 18, 10, GM_TEXT_DIM, false).wordWrap = false;
   gmInput(content, "ritem_txt", x + 192, 228, 56, 18, "", "item id");
   gmButton(content, "radd_mc", "+", x + 250, 228, 22, 18, function()
   {
      gmSend("!questedit reward add " + gmQuest.id + " " + gmFieldText(_root.gm_mc.content_mc.ritem_txt) + " 1");
   });
   gmButton(content, "rdel_mc", "-", x + 274, 228, 22, 18, function()
   {
      gmSend("!questedit reward del " + gmQuest.id + " " + gmFieldText(_root.gm_mc.content_mc.ritem_txt));
   }, 0xC0392B);

   gmStatus(gmQuestNodes.length > 0 ? "Collect node keys: " + gmOneLine(gmQuestNodes.join(", "), 70) : "Quest editor");
}

function gmHandleQuestData(kind, parts)
{
   if(kind == "questlist")
   {
      if(parts[2] == "begin")
      {
         gmQuestListLoading = [];
      }
      else if(parts[2] == "entry")
      {
         gmQuestListLoading.push({ id: parts[3], title: gmUnescapeText(parts[4]), published: parts[5] == "1", goals: Number(parts[6]) });
      }
      else if(parts[2] == "end")
      {
         gmQuestList = gmQuestListLoading;
         if(_root.gm_mc._visible && gmTab == "quests")
         {
            gmShowTab("quests");
         }
      }
      return;
   }
   if(kind == "questedit")
   {
      if(parts[2] == "begin")
      {
         gmQuestLoading = { id: parts[3], title: gmUnescapeText(parts[4]), desc: gmUnescapeText(parts[5]), giver: parts[6], turnin: parts[7],
            coins: parts[8], prereq: parts[9], next: parts[10], published: parts[11] == "1", titleId: parts[12], descId: parts[13], goals: [], rewards: [] };
      }
      else if(parts[2] == "end" && gmQuestLoading != undefined)
      {
         gmQuest = gmQuestLoading;
         gmQuestSel = gmQuest.id;
         gmQuestLoading = undefined;
         if(_root.gm_mc._visible && gmTab == "quests")
         {
            gmShowTab("quests");
         }
         // Titles and published state show in the list too.
         gmSend("!questedit list");
      }
      return;
   }
   if(kind == "questgoal" && gmQuestLoading != undefined)
   {
      gmQuestLoading.goals.push({ index: parts[2], type: parts[3], a: parts[4], b: parts[5], c: parts[6], d: parts[7], e: parts[8], text: gmUnescapeText(parts[9]) });
      return;
   }
   if(kind == "questreward" && gmQuestLoading != undefined)
   {
      gmQuestLoading.rewards.push([parts[2], parts[3]]);
      return;
   }
   if(kind == "questnodes")
   {
      gmQuestNodes = parts.slice(2);
   }
}

// --- decorate bar (every player) -----------------------------------------------
// While a player decorates their house, the server sends decorate|on and, for the piece last placed,
// moved or turned with the client's own wheel, decorate|sel|name|yaw|pitch|roll. The client's wheel only
// turns in fixed yaw steps; these buttons add yaw, pitch and roll in any step.

var gmDecorate = { on: false, name: "", yaw: 0, pitch: 0, roll: 0, x: undefined, y: undefined };
var GM_DECORATE_W = 300;

function gmHandleDecorate(parts)
{
   if(parts[2] == "on" || parts[2] == "off")
   {
      gmDecorate.on = parts[2] == "on";
   }
   else if(parts[2] == "sel")
   {
      gmDecorate.name = parts[3];
      gmDecorate.yaw = parts[4];
      gmDecorate.pitch = parts[5];
      gmDecorate.roll = parts[6];
   }
   gmShowDecorate();
}

function gmShowDecorate()
{
   var old = _root.gmdecorate_mc;
   if(old != undefined)
   {
      gmDecorate.x = old._x;
      gmDecorate.y = old._y;
      old.removeMovieClip();
   }
   if(!gmDecorate.on)
   {
      return;
   }
   var d = _root.createEmptyMovieClip("gmdecorate_mc", 16002);
   var w = GM_DECORATE_W;
   var h = 176;
   var frame = d.createEmptyMovieClip("frame_mc", 1);
   gmPanelShape(frame, w, h, 12, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 3);
   frame.onPress = function()
   {
      this._parent.startDrag(false, 0, gmTop(), Math.max(0, gmRight() - w), Math.max(gmTop(), gmBottom() - h));
   };
   frame.onRelease = frame.onReleaseOutside = function()
   {
      this._parent.stopDrag();
   };

   var title = gmDecorate.name == "" ? "Place, move or turn a piece to rotate it" : "Rotate: " + gmDecorate.name;
   gmText(d, "title_txt", title, 12, 8, w - 24, 18, 12, GM_TEXT_GOLD, true).wordWrap = false;
   if(gmDecorate.name != "")
   {
      gmText(d, "angles_txt", "Yaw " + gmDecorate.yaw + "   Pitch " + gmDecorate.pitch + "   Roll " + gmDecorate.roll, 12, 26, w - 24, 18, 11, GM_TEXT_DIM, false);
   }

   var axes = [["Yaw", "yaw"], ["Pitch", "pitch"], ["Roll", "roll"]];
   var steps = ["-45", "-15", "+15", "+45"];
   var i;
   var j;
   for(i = 0; i < axes.length; i++)
   {
      var y = 48 + i * 28;
      gmText(d, "axis" + i + "_txt", axes[i][0], 12, y + 4, 50, 18, 11, GM_TEXT, true);
      for(j = 0; j < steps.length; j++)
      {
         var b = gmButton(d, "step" + i + "_" + j + "_mc", steps[j], 62 + j * 58, y, 54, 24, function()
         {
            gmSend("!house rotate " + this.axis + " " + this.degrees);
         });
         b.axis = axes[i][1];
         b.degrees = Number(steps[j]);
      }
   }
   gmButton(d, "flip_mc", "Flip", 62, 136, 112, 26, function()
   {
      gmSend("!house rotate roll 180");
   });
   gmButton(d, "level_mc", "Level", 178, 136, 110, 26, function()
   {
      gmSend("!house rotate level");
   }, 0x2E7D5B);

   d._x = gmDecorate.x != undefined ? gmDecorate.x : Math.max(0, gmScreenRight() - w - 40);
   d._y = gmDecorate.y != undefined ? gmDecorate.y : Math.max(gmTop(), gmBottom() - h - 240);
}

// --- player dialog (every player) ------------------------------------------------
// GM conversations open the client's own NPC dialog for its camera zoom (its pane stays hidden, see
// ClientMods/dialog); this draws the text where that pane sits: a speech bubble on the right of the
// screen with the answers as large buttons below it.

var GM_DIALOG_W = 420;
var GM_DIALOG_BUBBLE = 0xFFF8E7;
var GM_DIALOG_TEXT = 0x3A2400;

function gmShowDialog(parts)
{
   // dialog|npcGuid|name|nodeId|text|idx:choice|...
   _root.gmdialog_mc.removeMovieClip();
   var d = _root.createEmptyMovieClip("gmdialog_mc", 16001);
   var w = GM_DIALOG_W;
   var guid = parts[2];
   var node = parts[4];

   var bubble = d.createEmptyMovieClip("bubble_mc", 1);
   var name = gmUnescapeText(parts[3]);
   gmText(bubble, "name_txt", name == "" ? "..." : name, 20, 14, w - 70, 22, 16, GM_DIALOG_TEXT, true);
   var body = gmText(bubble, "body_txt", gmUnescapeText(parts[5]), 20, 42, w - 40, 20, 14, GM_DIALOG_TEXT, false);
   body.autoSize = "left";
   var bubbleH = 42 + Math.max(24, body.textHeight + 8) + 18;

   // Rounded cream bubble with a tail pointing left, toward the NPC.
   bubble.lineStyle(3, GM_GOLD_DARK, 100);
   bubble.beginFill(GM_DIALOG_BUBBLE, 100);
   gmRoundRect(bubble, 0, 0, w, bubbleH, 18);
   bubble.endFill();
   bubble.lineStyle(3, GM_GOLD_DARK, 100);
   bubble.beginFill(GM_DIALOG_BUBBLE, 100);
   bubble.moveTo(2, bubbleH - 46);
   bubble.lineTo(-22, bubbleH - 22);
   bubble.lineTo(2, bubbleH - 26);
   bubble.endFill();
   // Text fields sit above the drawing.
   bubble.name_txt.swapDepths(bubble.getNextHighestDepth());
   bubble.body_txt.swapDepths(bubble.getNextHighestDepth());

   gmButton(d, "close_mc", "X", w - 38, 10, 26, 24, function()
   {
      gmSend("!talk end");
   }, 0xC0392B);

   var choices = parts.slice(6);
   if(choices.length == 0 || (choices.length == 1 && choices[0] == ""))
   {
      choices = ["end:Goodbye"];
   }
   var y = bubbleH + 12;
   var i;
   for(i = 0; i < choices.length; i++)
   {
      var colon = choices[i].indexOf(":");
      var index = choices[i].substr(0, colon);
      var b = gmButton(d, "choice" + i + "_mc", gmUnescapeText(choices[i].substr(colon + 1)), 30, y, w - 30, 36, function()
      {
         gmSend(this.index == "end" ? "!talk end" : "!talk " + this.guid + " " + this.node + " " + this.index);
      }, index == "end" ? 0x2E7D5B : undefined);
      b.index = index;
      b.guid = guid;
      b.node = node;
      y += 42;
   }
   var h = y;

   d._x = Math.max(0, gmScreenRight() - w - 120);
   d._y = gmTop() + Math.max(20, (gmBottom() - gmTop() - h) / 2);
}

// Right edge of what is actually on screen. The chat window is a fixed 2000 wide, so it usually runs past
// the edge of the game; the screen width, when the player reports one, is the better bound.
function gmScreenRight()
{
   var screen = Number(System.capabilities.screenResolutionX);
   return screen > 0 && screen < gmRight() ? screen : gmRight() * 0.9;
}

function gmHandleScript(parts)
{
   var step = parts[2];
   if(step == "begin")
   {
      gmScript.kind = parts[3];
      gmScript.pending = parts[3] == "lib" ? "lib:" + parts[4] : parts[4];
      gmScript.loading = "";
      return;
   }
   if(step == "chunk")
   {
      gmScript.loading += parts[3];
      return;
   }
   if(step == "end")
   {
      gmScript.guid = gmScript.pending;
      gmScript.source = gmUnescapeText(gmScript.loading);
      gmScript.status = "Loaded.";
      if(_root.gm_mc._visible && gmTab == "script")
      {
         gmShowTab("script");
      }
      return;
   }
   if(step == "ok" || step == "error")
   {
      gmScript.status = (step == "error" ? "Error: " : "") + gmUnescapeText(parts[3]);
      var result = _root.gm_mc.content_mc.result_txt;
      if(result != undefined)
      {
         result.text = gmScript.status;
      }
   }
}

// Re-renders the Talk tab after a tree update, keeping the selected line.
function gmConvRefresh()
{
   gmShowTab("conv");
}

// --- server data ---------------------------------------------------------------

function gmHandleData(text)
{
   var parts = gmUnescape(text.substr(text.indexOf(gmToken))).split("|");
   var kind = parts[1];
   var root_mc = _root.gm_mc;

   if(kind == "open")
   {
      root_mc._visible = !root_mc._visible;
      if(root_mc._visible)
      {
         gmFit();
         gmShowTab(gmTab);
      }
      return;
   }
   if(kind == "items" || kind == "npcs" || kind == "houses" || kind == "players")
   {
      gmList = { kind: kind, search: parts[2], page: Number(parts[3]), pages: Number(parts[4]), total: Number(parts[5]) };
      gmResults = [];
      return;
   }
   if(kind == "item" || kind == "npc" || kind == "house" || kind == "player")
   {
      gmResults.push(parts.slice(2));
      return;
   }
   if(kind == "end")
   {
      if(root_mc._visible && gmTab == gmList.kind)
      {
         gmShowTab(gmTab);
      }
      return;
   }
   if(kind == "edit")
   {
      // A selection opens the panel on the editor (no toggling, unlike "open").
      gmEdit = parts.slice(2);
      if(gmEdit[0] != "none" && !root_mc._visible)
      {
         root_mc._visible = true;
         gmFit();
      }
      if(gmEdit[0] != "none" || gmTab == "edit")
      {
         gmShowTab("edit");
      }
      return;
   }
   if(kind == "script")
   {
      gmHandleScript(parts);
      return;
   }
   if(kind == "questlist" || kind == "questedit" || kind == "questgoal" || kind == "questreward" || kind == "questnodes")
   {
      gmHandleQuestData(kind, parts);
      return;
   }
   if(kind == "scriptlib")
   {
      gmHandleScriptLib(parts);
      return;
   }
   if(kind == "conv")
   {
      gmConv = { guid: parts[2], start: parts[3], nodes: [], choices: [] };
      gmConvLinkFrom = undefined;
      return;
   }
   if(kind == "convnode")
   {
      gmConv.nodes.push({ id: parts[2], text: gmUnescapeText(parts[3]) });
      return;
   }
   if(kind == "convchoice")
   {
      gmConv.choices.push({ node: parts[2], index: parts[3], next: parts[4], give: parts[5], qty: parts[6], hook: parts[7], text: gmUnescapeText(parts[8]) });
      return;
   }
   if(kind == "convend")
   {
      if(root_mc._visible && gmTab == "conv")
      {
         gmConvRefresh();
      }
      return;
   }
   if(kind == "dialog")
   {
      if(parts[2] == "close")
      {
         _root.gmdialog_mc.removeMovieClip();
      }
      else
      {
         gmShowDialog(parts);
      }
      return;
   }
   if(kind == "decorate")
   {
      gmHandleDecorate(parts);
      return;
   }
   if(kind == "fields")
   {
      gmHandleFields(parts);
      return;
   }
   if(kind == "icons")
   {
      gmHandleIcons(parts);
      return;
   }
   if(kind == "anims")
   {
      gmAnims = [];
      var a;
      for(a = 2; a < parts.length; a++)
      {
         // <id>:<label>:<loop 1/0>
         var anim = parts[a].split(":");
         gmAnims.push([anim[0], anim[1], anim[2] == "1"]);
      }
      if(root_mc._visible && gmTab == "edit")
      {
         gmShowTab("edit");
      }
      return;
   }
   if(kind == "pos")
   {
      gmStatus(parts[2] + "  (" + parts[3] + ", " + parts[4] + ", " + parts[5] + ")");
   }
}

// Swallow GM data lines instead of printing them. The server sends them as debug/system chat,
// which can arrive through either entry point.
var gmOriginalAddSystemMessage = addSystemMessage;
addSystemMessage = function(guid, charName, bubText, txtColor, bubColor, messageCount)
{
   if(String(bubText).indexOf(gmToken) != -1)
   {
      gmHandleData(String(bubText));
      return undefined;
   }
   return gmOriginalAddSystemMessage.apply(_root, arguments);
};

var gmOriginalAddChatMessege = addChatMessege;
addChatMessege = function(guid, charName, bubText, txtSize, txtColor, bubColor, channelName, isWhispNum, isNPC, isRef, areaName, messageCount)
{
   if(String(bubText).indexOf(gmToken) != -1)
   {
      gmHandleData(String(bubText));
      return undefined;
   }
   return gmOriginalAddChatMessege.apply(_root, arguments);
};

gmBuild();
// ===== End Sanctuary GM panel =====
