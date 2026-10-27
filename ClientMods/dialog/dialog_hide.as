// ===== Sanctuary dialog =====
// Appended to dialog's main timeline by build_client_mods.py.
//
// GM-authored conversations (Sanctuary NpcContentManager) open the client's own NPC dialog for its camera
// zoom, but its pane only shows locale strings, so the server sends it empty and the Referee Tools panel
// (chatWindowCtrl) draws the conversation instead. An empty dialog keeps its bubble hidden here; real
// dialogs always carry text and show as before.
var sanctuaryOriginalSetDialogText = setDialogText;
setDialogText = function(dialogQuestion, dialogText)
{
   var question = dialogQuestion == undefined ? "" : String(dialogQuestion);
   var text = dialogText == undefined ? "" : String(dialogText);
   if(question == "" && text == "")
   {
      question_dialog._visible = false;
      response_dialog._visible = false;
      return undefined;
   }
   return sanctuaryOriginalSetDialogText.apply(_root, arguments);
};
// ===== End Sanctuary dialog =====
