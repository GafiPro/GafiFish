using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>Selects a configured consumable slot, clicks once, and restores the rod slot.</summary>
public sealed class HotbarItemCoordinator
{
    private readonly IRobloxInput _input;
    public HotbarItemCoordinator(IRobloxInput input) => _input = input ?? throw new ArgumentNullException(nameof(input));

    public void UseItem(int itemSlot, int rodSlot)
    {
        ValidateSlot(itemSlot, nameof(itemSlot));
        ValidateSlot(rodSlot, nameof(rodSlot));
        if (itemSlot == rodSlot) throw new ArgumentException("Item and rod slots must differ.", nameof(itemSlot));
        try
        {
            TapKey(itemSlot);
            _input.SetLeftButton(true);
        }
        finally
        {
            try { _input.SetLeftButton(false); }
            finally { TapKey(rodSlot); }
        }
    }

    private void TapKey(int slot)
    {
        var key = (ushort)(0x30 + slot);
        try { _input.SetKey(key, true); }
        finally { _input.SetKey(key, false); }
    }

    private static void ValidateSlot(int slot, string name)
    {
        if (slot is < 0 or > 9) throw new ArgumentOutOfRangeException(name, "Hotbar slot must be between 0 and 9.");
    }
}
