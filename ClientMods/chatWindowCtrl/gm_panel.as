// ===== Sanctuary GM panel =====
// Appended to chatWindowCtrl's main timeline by build_client_mods.py.
//
// Opened by the "Referee Tools" button on the main hub (ClientMods/gamedock), which sends "!gm".
// The server answers with hidden chat lines "[[SANCTUARY_GM]]|kind|field|..." (GmChatCommand)
// that are swallowed here and rendered. Every button sends a "!" command through chat.

var gmToken = "[[SANCTUARY_GM]]";

var GM_W = 540;
var GM_H = 340;
var GM_COLUMNS = 3;
var GM_CELL_W = 166;
var GM_CELL_H = 50;

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
   ["conv", "Talk"]
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
   root_mc._y = 14;
   root_mc._visible = false;

   var frame = root_mc.createEmptyMovieClip("frame_mc", 1);
   gmPanelShape(frame, GM_W, GM_H, 14, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 4);
   frame.onPress = function()
   {
      // Keep the panel inside the window while dragging; anything outside it is clipped.
      var p = _root.gm_mc;
      var w = GM_W * p._xscale / 100;
      var h = GM_H * p._yscale / 100;
      p.startDrag(false, 0, 14, Math.max(0, Stage.width - w), Math.max(14, Stage.height - h));
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
      var tab = gmButton(root_mc, "tab" + i + "_mc", gmTabs[i][1], 12 + i * 65, 32, 62, 24, function()
      {
         gmShowTab(this.tabId);
      });
      tab.tabId = gmTabs[i][0];
   }

   var inner = root_mc.createEmptyMovieClip("inner_mc", 50);
   inner._x = 10;
   inner._y = 62;
   inner.lineStyle(2, GM_GOLD_DARK, 100);
   inner.beginFill(GM_BLUE_INNER, 92);
   gmRoundRect(inner, 0, 0, GM_W - 20, GM_H - 92, 10);
   inner.endFill();

   gmText(root_mc, "status_txt", "", 14, GM_H - 26, GM_W - 28, 18, 11, GM_TEXT_DIM, false);

   root_mc.createEmptyMovieClip("content_mc", 60);
   gmClearContent();

   var stageListener = new Object();
   stageListener.onResize = function()
   {
      gmFit();
   };
   Stage.addListener(stageListener);
}

// Keeps the whole panel on screen at any window size; maximized fills the space available.
function gmFit()
{
   var root_mc = _root.gm_mc;
   var available = Math.min((Stage.width - 16) / GM_W, (Stage.height - 28) / GM_H);
   // The client clips each movie to its window, so the chat window's Stage is as big as the panel can get.
   var scale = gmMaximized ? available : Math.min(1, available);
   if(scale <= 0)
   {
      return;
   }
   root_mc._xscale = root_mc._yscale = scale * 100;
   if(gmMaximized || root_mc._x < 0 || root_mc._x + GM_W * scale > Stage.width || root_mc._y + GM_H * scale > Stage.height)
   {
      root_mc._x = Math.max(0, (Stage.width - GM_W * scale) / 2);
      root_mc._y = Math.max(14, (Stage.height - GM_H * scale) / 2);
   }
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
   content._x = 18;
   content._y = 68;
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
         ["Anywhere", "!house unbound"],
         ["Actors", "!house actors"]
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
      var b = gmButton(content, "prompt" + i + "_mc", prompts[i][0], (i % 6) * 84, 122 + Math.floor(i / 6) * 24, 81, 21, function()
      {
         gmPrompt(this.prefix);
      });
      b.prefix = prompts[i][1];
   }
   gmText(content, "game_txt", "Game systems", 0, 198, 300, 18, 12, GM_TEXT_GOLD, true);
   gmButtonRow(content, 216, [
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
   if(kind == "npc")
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

// --- text escaping (scripts, conversations, dialog) -----------------------------
// Free text travels as: '\' -> "\\", newline -> "\n", '|' -> "\p".

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

// Selected entity kind for "!npc script" / "!prop script": the editor's selection.
function gmScriptKind()
{
   return gmEdit[0] == "prop" ? "prop" : "npc";
}

var gmScript = { kind: "", guid: "", source: "", loading: "", status: "" };

var GM_SCRIPT_HELP = "Hooks: onSpawn()  onSecond()  onInteract(player)  onProximity(player)  onChoice(player, hook)\r" +
   "npc: guid name position  say(text)  sayLocalized(id)  moveTo(x,y,z[,direct])  emote(animId[,loop])  face(player)  startConversation(player[,nodeId])\r" +
   "player: name guid position  message(text)  giveItem(itemId[,qty])\r" +
   "Lua: math string table pairs ipairs next select type tostring tonumber pcall error assert unpack print.  16 KB, 100 ms per hook.";

function gmRenderScript(content)
{
   if(gmEdit[0] != "npc" && gmEdit[0] != "prop")
   {
      gmText(content, "none_txt", "Select an NPC or world prop first (god mode, then click it), then come back here to edit its script.", 0, 0, 500, 40, 12, GM_TEXT_DIM, false);
      gmStatus("Script");
      return;
   }
   var kind = gmScriptKind();
   var loaded = gmScript.guid == gmEdit[1];
   gmText(content, "title_txt", "Script for " + kind + " " + gmEdit[1] + (loaded ? "" : "  (not loaded)"), 0, 0, 330, 20, 13, GM_TEXT_GOLD, true);

   var area = content.createEmptyMovieClip("area_mc", content.getNextHighestDepth());
   area._y = 24;
   area.lineStyle(2, GM_GOLD, 100);
   area.beginFill(0x0B1E3A, 100);
   gmRoundRect(area, 0, 0, 504, 150, 8);
   area.endFill();
   content.createTextField("source_txt", content.getNextHighestDepth(), 6, 28, 492, 142);
   var t = content.source_txt;
   t.embedFonts = true;
   t.type = "input";
   t.selectable = true;
   t.multiline = true;
   t.wordWrap = true;
   t.maxChars = 16000;
   t.setNewTextFormat(gmFormat(11, GM_TEXT, false));
   t.text = loaded ? gmScript.source : "";
   area.field = t;
   area.onPress = function()
   {
      gmFocusInput(this.field);
   };

   gmEditButtons(content, 180, [
      ["Load", "!" + kind + " script show"],
      ["Test", "!" + kind + " script test"],
      ["Clear", "!" + kind + " script clear", 0xC0392B]
   ]);
   gmButton(content, "save_mc", "Save", 252, 180, 80, 22, function()
   {
      gmSaveScript();
   }, 0x2E7D5B);
   gmButton(content, "help_mc", "API help", 336, 180, 80, 22, function()
   {
      var h = _root.gm_mc.content_mc.help_txt;
      h._visible = !h._visible;
   });

   gmText(content, "help_txt", GM_SCRIPT_HELP, 0, 24, 504, 150, 10, GM_TEXT, false);
   var help = content.help_txt;
   help.background = true;
   help.backgroundColor = 0x0B1E3A;
   help._visible = false;

   gmText(content, "result_txt", gmScript.status, 0, 206, 504, 36, 11, GM_TEXT_GOLD, false);
   gmStatus("Script editor");
}

function gmSaveScript()
{
   var t = _root.gm_mc.content_mc.source_txt;
   if(t == undefined)
   {
      return;
   }
   var kind = gmScriptKind();
   var source = String(t.text);
   gmScript.source = source;
   gmScript.guid = gmEdit[1];
   // Escape per chunk so no escape sequence is split; each stays well under the chat line limit.
   var i;
   for(i = 0; i < source.length; i += 100)
   {
      gmQueueSend("!" + kind + " script append " + gmEscapeText(source.substr(i, 100)));
   }
   gmQueueSend("!" + kind + " script commit");
   gmScript.status = "Saving...";
   _root.gm_mc.content_mc.result_txt.text = gmScript.status;
   fscommand("FR_event", "killFocus()");
}

// --- conversation tab ------------------------------------------------------------

var gmConv = { guid: "", start: 1, nodes: [], choices: [], building: false };
var gmConvScroll = 0;

function gmConvLines()
{
   // Flattened tree: nodes, each followed by its choices.
   var lines = [];
   var i;
   var j;
   for(i = 0; i < gmConv.nodes.length; i++)
   {
      var node = gmConv.nodes[i];
      lines.push({ kind: "node", id: node.id, text: node.text });
      for(j = 0; j < gmConv.choices.length; j++)
      {
         var c = gmConv.choices[j];
         if(c.node == node.id)
         {
            lines.push({ kind: "choice", node: c.node, index: c.index, next: c.next, give: c.give, qty: c.qty, hook: c.hook, text: c.text });
         }
      }
   }
   return lines;
}

function gmOneLine(s)
{
   var r = frHelperClass.stringReplace(String(s), "\r", " / ");
   return r.length > 70 ? r.substr(0, 68) + "..." : r;
}

function gmRenderConv(content)
{
   if(gmEdit[0] != "npc")
   {
      gmText(content, "none_txt", "Select an NPC first (god mode, then click it), then come back here to author its conversation.", 0, 0, 500, 40, 12, GM_TEXT_DIM, false);
      gmStatus("Conversation");
      return;
   }
   var loaded = gmConv.guid == gmEdit[1];
   gmText(content, "title_txt", "Conversation  (start #" + (loaded ? gmConv.start : "?") + ")" + (loaded ? "" : "  not loaded"), 0, 2, 220, 20, 12, GM_TEXT_GOLD, true);
   gmEditField(content, "start_txt", "Start", 222, 0, 34, 34, loaded ? gmConv.start : "", "!conv start");
   gmButton(content, "load_mc", "Load", 334, 0, 52, 22, function()
   {
      gmSend("!conv show");
   });
   gmButton(content, "test_mc", "Test", 390, 0, 52, 22, function()
   {
      gmSend("!conv test");
   });
   gmButton(content, "clear_mc", "Clear", 446, 0, 58, 22, function()
   {
      gmSend("!conv clear");
   }, 0xC0392B);

   // Tree view, 7 lines with paging. Click a line to load it into the fields below.
   var list = content.createEmptyMovieClip("list_mc", content.getNextHighestDepth());
   list._y = 26;
   list.lineStyle(1, GM_GOLD_DARK, 100);
   list.beginFill(0x0B1E3A, 100);
   gmRoundRect(list, 0, 0, 474, 122, 6);
   list.endFill();
   var lines = loaded ? gmConvLines() : [];
   gmConvScroll = Math.max(0, Math.min(gmConvScroll, lines.length - 7));
   var i;
   for(i = 0; i < 7 && gmConvScroll + i < lines.length; i++)
   {
      var line = lines[gmConvScroll + i];
      var row = list.createEmptyMovieClip("row" + i, 10 + i);
      row._x = 4;
      row._y = 3 + i * 17;
      row.beginFill(0xFFFFFF, 0);
      gmRoundRect(row, 0, 0, 466, 16, 0);
      row.endFill();
      var label;
      if(line.kind == "node")
      {
         label = "#" + line.id + "  " + gmOneLine(line.text);
      }
      else
      {
         label = "     [" + line.index + "] -> " + (line.next == "0" ? "end" : "#" + line.next) + "  " + gmOneLine(line.text) +
            (Number(line.give) > 0 ? "  (give " + line.give + " x" + line.qty + ")" : "") + (line.hook != "" ? "  (hook " + line.hook + ")" : "");
      }
      gmText(row, "label_txt", label, 0, 0, 466, 17, 11, line.kind == "node" ? GM_TEXT : GM_TEXT_DIM, line.kind == "node");
      row.line = line;
      row.onRelease = function()
      {
         gmConvPick(this.line);
      };
   }
   gmButton(content, "up_mc", "^", 478, 26, 26, 58, function()
   {
      gmConvScroll -= 6;
      gmShowTab("conv");
   });
   gmButton(content, "down_mc", "v", 478, 90, 26, 58, function()
   {
      gmConvScroll += 6;
      gmShowTab("conv");
   });

   // Node: id + text.
   gmText(content, "node_label", "Node", 0, 158, 36, 18, 11, GM_TEXT_DIM, true);
   gmInput(content, "nodeid_txt", 36, 154, 36, 22, "", "id");
   gmInput(content, "nodetext_txt", 76, 154, 300, 22, "", "What the NPC says");
   gmButton(content, "setnode_mc", "Set node", 380, 154, 66, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      gmSend("!conv node " + gmFieldText(c.nodeid_txt) + " " + gmEscapeText(String(c.nodetext_txt.text)));
   });
   gmButton(content, "delnode_mc", "Delete", 450, 154, 54, 22, function()
   {
      gmSend("!conv node del " + gmFieldText(_root.gm_mc.content_mc.nodeid_txt));
   }, 0xC0392B);

   // New choice on that node.
   gmText(content, "choice_label", "Choice", 0, 186, 40, 18, 11, GM_TEXT_DIM, true);
   gmText(content, "next_label", "next", 40, 186, 28, 18, 11, GM_TEXT_DIM, false);
   gmInput(content, "next_txt", 68, 182, 34, 22, "", "0");
   gmInput(content, "choicetext_txt", 106, 182, 270, 22, "", "What the player answers");
   gmButton(content, "addchoice_mc", "Add choice", 380, 182, 124, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var next = gmFieldText(c.next_txt);
      gmSend("!conv choice add " + gmFieldText(c.nodeid_txt) + " " + (next == "" ? "0" : next) + " " + gmEscapeText(String(c.choicetext_txt.text)));
   });

   // Existing choice (node id + index): delete, give item, script hook.
   gmText(content, "idx_label", "Choice #", 0, 214, 52, 18, 11, GM_TEXT_DIM, true);
   gmInput(content, "index_txt", 52, 210, 30, 22, "", "0");
   gmButton(content, "delchoice_mc", "Del", 86, 210, 36, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      gmSend("!conv choice del " + gmFieldText(c.nodeid_txt) + " " + gmFieldText(c.index_txt));
   }, 0xC0392B);
   gmInput(content, "give_txt", 128, 210, 60, 22, "", "item id");
   gmInput(content, "qty_txt", 192, 210, 34, 22, "", "qty");
   gmButton(content, "give_mc", "Give", 230, 210, 44, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var item = gmFieldText(c.give_txt);
      gmSend("!conv choice give " + gmFieldText(c.nodeid_txt) + " " + gmFieldText(c.index_txt) + " " + (item == "" ? "0" : item) + " " + gmFieldText(c.qty_txt));
   });
   gmInput(content, "hook_txt", 280, 210, 130, 22, "", "script hook name");
   gmButton(content, "hook_mc", "Hook", 414, 210, 90, 22, function()
   {
      var c = _root.gm_mc.content_mc;
      var hook = gmFieldText(c.hook_txt);
      gmSend("!conv choice hook " + gmFieldText(c.nodeid_txt) + " " + gmFieldText(c.index_txt) + " " + (hook == "" ? "none" : hook));
   });

   gmStatus(loaded ? gmConv.nodes.length + " node(s), " + gmConv.choices.length + " choice(s)" : "Press Load to fetch this NPC's conversation.");
}

// Fills the authoring fields from a clicked tree line.
function gmConvPick(line)
{
   var c = _root.gm_mc.content_mc;
   if(line.kind == "node")
   {
      c.nodeid_txt.text = line.id;
      c.nodetext_txt.text = line.text;
      c.index_txt.text = "";
   }
   else
   {
      c.nodeid_txt.text = line.node;
      c.index_txt.text = line.index;
      c.next_txt.text = line.next;
      c.choicetext_txt.text = line.text;
      c.give_txt.text = Number(line.give) > 0 ? line.give : "";
      c.qty_txt.text = Number(line.give) > 0 ? line.qty : "";
      c.hook_txt.text = line.hook;
   }
   var names = ["nodeid_txt", "nodetext_txt", "index_txt", "next_txt", "choicetext_txt", "give_txt", "qty_txt", "hook_txt"];
   var i;
   for(i = 0; i < names.length; i++)
   {
      c[names[i]].onChanged();
   }
}

// --- player dialog (every player) ------------------------------------------------

function gmShowDialog(parts)
{
   // dialog|npcGuid|name|nodeId|text|idx:choice|...
   _root.gmdialog_mc.removeMovieClip();
   var d = _root.createEmptyMovieClip("gmdialog_mc", 16001);
   var w = 360;
   var guid = parts[2];
   var node = parts[4];

   var title = gmText(d, "name_txt", gmUnescapeText(parts[3]), 14, 10, w - 60, 20, 14, GM_TEXT_GOLD, true);
   var body = gmText(d, "body_txt", gmUnescapeText(parts[5]), 14, 34, w - 28, 20, 12, GM_TEXT, false);
   body.autoSize = "left";
   var y = 34 + Math.max(20, body.textHeight + 6) + 8;

   var choices = parts.slice(6);
   if(choices.length == 0 || (choices.length == 1 && choices[0] == ""))
   {
      choices = ["end:Goodbye"];
   }
   var i;
   for(i = 0; i < choices.length; i++)
   {
      var colon = choices[i].indexOf(":");
      var index = choices[i].substr(0, colon);
      var b = gmButton(d, "choice" + i + "_mc", gmUnescapeText(choices[i].substr(colon + 1)), 14, y, w - 28, 24, function()
      {
         gmSend(this.index == "end" ? "!talk end" : "!talk " + this.guid + " " + this.node + " " + this.index);
      });
      b.index = index;
      b.guid = guid;
      b.node = node;
      y += 28;
   }
   var h = y + 8;

   var frame = d.createEmptyMovieClip("frame_mc", -1);
   gmPanelShape(frame, w, h, 14, GM_BLUE_TOP, GM_BLUE_BOTTOM, GM_GOLD, 3);
   frame.onPress = function()
   {
      this._parent.startDrag(false, 0, 0, Math.max(0, Stage.width - w), Math.max(0, Stage.height - h));
   };
   frame.onRelease = frame.onReleaseOutside = function()
   {
      this._parent.stopDrag();
   };
   gmButton(d, "close_mc", "X", w - 34, 8, 24, 22, function()
   {
      gmSend("!talk end");
   }, 0xC0392B);

   d._x = Math.max(0, (Stage.width - w) / 2);
   d._y = Math.max(0, Stage.height - h - 60);
}

function gmHandleScript(parts)
{
   var step = parts[2];
   if(step == "begin")
   {
      gmScript.kind = parts[3];
      gmScript.pending = parts[4];
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

// Re-renders the Talk tab after a tree update, keeping what the GM typed in the fields.
function gmConvRefresh()
{
   var names = ["nodeid_txt", "nodetext_txt", "index_txt", "next_txt", "choicetext_txt", "give_txt", "qty_txt", "hook_txt"];
   var saved = [];
   var i;
   for(i = 0; i < names.length; i++)
   {
      saved.push(_root.gm_mc.content_mc[names[i]].text);
   }
   gmShowTab("conv");
   var c = _root.gm_mc.content_mc;
   for(i = 0; i < names.length; i++)
   {
      if(saved[i] != undefined && c[names[i]] != undefined)
      {
         c[names[i]].text = saved[i];
         c[names[i]].onChanged();
      }
   }
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
   if(kind == "conv")
   {
      gmConv = { guid: parts[2], start: parts[3], nodes: [], choices: [] };
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
