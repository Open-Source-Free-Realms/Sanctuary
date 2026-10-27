// ===== Sanctuary no Station Cash =====
// Appended to UnifiedMessageWindow's main timeline by build_client_mods.py.
// Station Cash no longer exists, so the purchase panel drops its wallet balance and Add Funds button.

function sanctuaryHideStationCash(panel)
{
   panel.AddFundsButton._visible = false;
   panel.txtWalletBalance._visible = false;
   panel.txtWalletBalanceLabel._visible = false;
}

var sanctuaryOriginalSetData = NameChangePanel.prototype.setData;
NameChangePanel.prototype.setData = function()
{
   sanctuaryOriginalSetData.apply(this,arguments);
   sanctuaryHideStationCash(this);
};

var sanctuaryOriginalSetWalletBalance = NameChangePanel.prototype.setWalletBalance;
NameChangePanel.prototype.setWalletBalance = function()
{
   sanctuaryOriginalSetWalletBalance.apply(this,arguments);
   sanctuaryHideStationCash(this);
};
// ===== End Sanctuary no Station Cash =====
