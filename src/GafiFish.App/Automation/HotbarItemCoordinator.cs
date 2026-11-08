using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Selects a configured consumable slot, clicks once, and restores the rod slot.
/// Any failed input operation disarms the adapter instead of sending more keys
/// into a window that may have lost focus.
/// </summary>
public sealed class HotbarItemCoordinator
{
    private readonly IRobloxInput _input;

    public HotbarItemCoordinator(IRobloxInput input) =>
        _input = input ?? throw new ArgumentNullException(nameof(input));

    public void UseItem(int itemSlot, int rodSlot)
    {
        ValidateSlot(itemSlot, nameof(itemSlot));
        ValidateSlot(rodSlot, nameof(rodSlot));
        if (itemSlot == rodSlot)
            throw new ArgumentException("Item and rod slots must differ.", nameof(itemSlot));

        if (!_input.IsArmed)
            throw new InvalidOperationException(
                "Cannot use a hotbar item while Roblox input is disarmed. Arm the macro with Roblox in the foreground.");

        try
        {
            TapKey(itemSlot);
            _input.SetLeftButton(true);
            _input.SetLeftButton(false);

            // A focus loss between the click and rod restoration disarms the
            // real input adapter. Never try to select another slot afterwards.
            if (!_input.IsArmed)
                throw new InvalidOperationException(
                    "Roblox input was disarmed after the item click. The rod slot was not changed.");

            TapKey(rodSlot);
        }
        catch
        {
            // Disarm releases all tracked keys/buttons and prevents this
            // operation from continuing after a partial or failed click.
            // Preserve the original input exception for the caller's log.
            try
            {
                _input.Disarm();
            }
            catch
            {
                // The input service keeps unresolved states tracked so a
                // later emergency stop/Arm can retry the release.
            }

            throw;
        }
    }

    private void TapKey(int slot)
    {
        var key = (ushort)(0x30 + slot);
        try
        {
            _input.SetKey(key, true);
        }
        finally
        {
            _input.SetKey(key, false);
        }
    }

    private static void ValidateSlot(int slot, string name)
    {
        if (slot is < 0 or > 9)
            throw new ArgumentOutOfRangeException(name, "Hotbar slot must be between 0 and 9.");
    }
}
