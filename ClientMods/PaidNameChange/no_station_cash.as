// ===== Sanctuary no Station Cash =====
// Appended to PaidNameChange's main timeline by build_client_mods.py.
// Station Cash no longer exists, so the Station Cash wallet and its Add Station Cash button are hidden.

var sanctuaryOriginalWalletConfigUI = controls.MyWallet.prototype.configUI;
controls.MyWallet.prototype.configUI = function()
{
   sanctuaryOriginalWalletConfigUI.apply(this,arguments);
   this._visible = false;
};

var sanctuaryOriginalUpdateWalletInfo = controls.MyWallet.prototype.updateWalletInfo;
controls.MyWallet.prototype.updateWalletInfo = function()
{
   sanctuaryOriginalUpdateWalletInfo.apply(this,arguments);
   this._visible = false;
};
// ===== End Sanctuary no Station Cash =====
