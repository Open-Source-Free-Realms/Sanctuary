// ===== Sanctuary no Station Cash =====
// Appended to HousingPreview's main timeline by build_client_mods.py.
// Station Cash no longer exists, so the purchase panel leaves out the Station Cash wallet balance.

function sanctuaryHideStationCash(panel)
{
   panel.txtWalletBalance._visible = false;
   panel.txtWalletBalanceLabel._visible = false;
}

var sanctuaryOriginalSetData = HousingPreview.prototype.setData;
HousingPreview.prototype.setData = function()
{
   sanctuaryOriginalSetData.apply(this,arguments);
   sanctuaryHideStationCash(this);
};

var sanctuaryOriginalSetWalletBalance = HousingPreview.prototype.setWalletBalance;
HousingPreview.prototype.setWalletBalance = function()
{
   sanctuaryOriginalSetWalletBalance.apply(this,arguments);
   sanctuaryHideStationCash(this);
};
// ===== End Sanctuary no Station Cash =====
