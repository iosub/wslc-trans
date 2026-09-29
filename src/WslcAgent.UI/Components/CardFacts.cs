using System.Globalization;
using WslcAgent.ApiClient;
using WslcAgent.ApiClient.Contracts;

namespace WslcAgent.UI.Components;

/// <summary>
/// What a card's reading is made of (the owner, 22 September 2026): the
/// figures behind the number a card shows, and a line saying how they make
/// it, for the window a second tap opens on a card that opens nothing else.
/// The same readings the cards and the status bar are drawn from, written out.
/// </summary>
public static class CardFacts
{
    /// <summary>The facts of a card, and how they make its reading; empty for a card that has none.</summary>
    public static (IReadOnlyList<InspectFact> Facts, string How) Of(DashboardCard card, AlarmReadings watch, HomeStorage? storage)
    {
        var facts = new List<InspectFact>();
        var how = "";
        if (card.Ref is { } reference)
        {
            if (watch.Containers?.FirstOrDefault(c => c.Uid == reference.Uid) is { } container)
            {
                facts.Add(new("Container", container.Name));
                facts.Add(new("State", container.State));
                facts.Add(new("CPU", Readings.Dash(container.CpuPercent)));
                facts.Add(new("Memory", Readings.Dash(container.MemUsage)));
                facts.Add(new("Memory share", Readings.Dash(container.MemPercent)));
            }

            how = "The container's own stats, as wslc stats prints them for it: its CPU percentage — 100% is one whole CPU — and the memory it uses of the session's. A container that is not running reports none.";
            return (Alarms(card, facts), how);
        }

        switch (card.Card)
        {
            case "cpu" or "cpu-chart" or DashboardCatalogue.CpuAlarm:
                if (watch.Runtime is { } cpu)
                {
                    facts.Add(new("Used", Percent(cpu.CpuUsedPercent)));
                    facts.Add(new("Of", $"{cpu.CpuTotalPercent}% ({cpu.CpuCount} CPUs at 100% each)"));
                    facts.Add(new("Share", Share(cpu.CpuUsedPercent, cpu.CpuTotalPercent)));
                }

                how = "The CPU percentage of every container, as wslc stats prints it, added. One whole CPU is 100%, so the machine's own is its CPU count times 100%, and the share is the one an alarm watches.";
                break;
            case "memory" or "memory-chart" or DashboardCatalogue.MemoryAlarm:
                if (watch.Runtime is { } memory)
                {
                    facts.Add(new("Used", Bytes.Humanize(memory.MemoryUsedBytes)));
                    facts.Add(new("Of", memory.MemoryTotalBytes > 0 ? Bytes.Humanize(memory.MemoryTotalBytes) : "not known: no container is running to report it"));
                    facts.Add(new("Share", Share(memory.MemoryUsedBytes, memory.MemoryTotalBytes)));
                }

                how = "The memory every container uses, added, out of the memory the session has — what each stats row prints after its slash. With no container running the CLI reports no total, and the share cannot be worked out.";
                break;
            case DashboardCatalogue.DiskSpace or DashboardCatalogue.DiskAlarm:
                if (watch.Disk is { Error: false } disk)
                {
                    facts.Add(new("Drive", disk.Drive, Mono: true));
                    facts.Add(new("Used", Bytes.Humanize(disk.UsedBytes)));
                    facts.Add(new("Free", Bytes.Humanize(disk.TotalBytes - disk.UsedBytes)));
                    facts.Add(new("Size", Bytes.Humanize(disk.TotalBytes)));
                    facts.Add(new("Share", Share(disk.UsedBytes, disk.TotalBytes)));
                }

                how = "The Windows drive WSLC keeps its sessions' VHDX files on, as Windows reports it: its size less what is free is what is used. It is the disk that fills and stops everything, not the virtual disks' allocation.";
                break;
            case "storage-images":
                if (storage is { Images: { Error: false } images })
                {
                    facts.Add(new("Images", $"{images.Count}"));
                    facts.Add(new("Catalog", Bytes.Humanize(images.SizeBytes)));
                }

                how = "What image list reports for every image, added: the catalog on disk, not what a container adds on top of it.";
                break;
            case "storage-vhdx" or "storage-swap" or "storage-sessions":
                if (storage is { Vhdx: { Error.Length: 0 } vhdx })
                {
                    facts.Add(new("Sessions", $"{vhdx.Sessions.Count}"));
                    facts.Add(new("Storage", Bytes.Humanize(vhdx.TotalStorageBytes)));
                    facts.Add(new("Swap", Bytes.Humanize(vhdx.TotalSwapBytes)));
                    facts.Add(new("Allocated", Bytes.Humanize(vhdx.TotalBytes)));
                    facts.Add(new("Largest", vhdx.LargestSessionName.Length > 0 ? $"{vhdx.LargestSessionName} · {Bytes.Humanize(vhdx.LargestSessionBytes)}" : "—"));
                    facts.Add(new("Folder", vhdx.BasePath, Mono: true));
                }

                how = "The size of each session's VHDX files, its storage disk and its swap disk, added. It is what they take of the drive, not what is free on it: a VHDX keeps the room it once grew into until it is compacted.";
                break;
        }

        return (Alarms(card, facts), how);
    }

    /// <summary>The card's alarms after its figures: what each watches for.</summary>
    private static IReadOnlyList<InspectFact> Alarms(DashboardCard card, List<InspectFact> facts)
    {
        foreach (var alarm in DashboardCatalogue.Measures(card.Card).Select(measure => DashboardCatalogue.AlarmSetting(card, measure)))
        {
            facts.Add(new($"{DashboardCatalogue.MeasureLabel(alarm.Measure)} alarm", alarm.Off ? "off" : $"past {alarm.Threshold}%"));
        }

        return facts;
    }

    private static string Percent(double value) => $"{value.ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string Share(double part, double whole) => whole > 0 ? Percent(100.0 * part / whole) : "not known";
}
