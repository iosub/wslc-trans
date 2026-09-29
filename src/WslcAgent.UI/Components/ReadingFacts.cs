using MudBlazor;
using WslcAgent.ApiClient.Contracts;
using WslcAgent.UI.Components.Dialogs;

namespace WslcAgent.UI.Components;

/// <summary>
/// The window that says what a card's reading is made of (the owner,
/// 22 September 2026), opened from the two places that ask for it: a second
/// tap on a card of the dashboard that opens nothing else, and a tap on one of
/// the status bar's alarms. One call, so both show the same.
/// </summary>
public static class ReadingFacts
{
    public static Task ShowAsync(IDialogService dialogs, DashboardCard card, AlarmReadings watch, HomeStorage? storage, string what)
    {
        var (facts, how) = CardFacts.Of(card, watch, storage);
        var parameters = new DialogParameters<ReadingFactsDialog>
        {
            { d => d.Facts, facts },
            { d => d.How, how },
        };
        return DialogFlow.ShowAsync<ReadingFactsDialog>(dialogs, $"{what}: how it is read", parameters);
    }
}
