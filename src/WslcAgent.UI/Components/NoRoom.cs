using MudBlazor;

namespace WslcAgent.UI.Components;

/// <summary>
/// What a size field says when the dashboard refused the size (the owner,
/// 22 September 2026: a card or a part goes only where there is room, and
/// nothing else moves to make it). The field springs back to the size that
/// stands; this says why, so the refusal is not read as the field ignoring
/// the hand.
/// </summary>
public static class NoRoom
{
    /// <summary>Awaits <paramref name="change"/> and, when it was refused, says the cells are taken. Returns whether it went through.</summary>
    public static async Task<bool> SayIfRefusedAsync(ISnackbar snackbar, Task<bool> change)
    {
        if (await change)
        {
            return true;
        }

        snackbar.Add("There is no room for that size: the cells it needs are taken. Make room first, then try again.", Severity.Warning);
        return false;
    }
}
