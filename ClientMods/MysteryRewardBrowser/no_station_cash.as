// ===== Sanctuary no Station Cash =====
// Appended to MysteryRewardBrowser's (and MysteryRewardBrowserSmall's) main timeline by build_client_mods.py.
// Station Cash no longer exists, so the side panel's purchase widget drops its wallet balance and
// Add Station Cash button.

function sanctuaryHideStationCash(panel)
{
   panel.pWidget.AddFundsButton._visible = false;
   panel.pWidget.txtWalletBalance._visible = false;
   panel.pWidget.txtWalletBalanceLabel._visible = false;
}

var sanctuaryOriginalPopulate = SidePanel.prototype.populate;
SidePanel.prototype.populate = function()
{
   sanctuaryOriginalPopulate.apply(this,arguments);
   sanctuaryHideStationCash(this);
};

var sanctuaryOriginalPopulateUberPanel = SidePanel.prototype.populateUberPanel;
SidePanel.prototype.populateUberPanel = function()
{
   sanctuaryOriginalPopulateUberPanel.apply(this,arguments);
   sanctuaryHideStationCash(this);
};
// ===== End Sanctuary no Station Cash =====
