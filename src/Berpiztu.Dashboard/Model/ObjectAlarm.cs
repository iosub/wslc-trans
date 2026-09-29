namespace Berpiztu.Dashboard.Model;

/// <summary>
/// One of an object's alarms as the user set it (the owner, 26 September
/// 2026): the measure it watches, the percent past which the object turns red
/// and blinks, in steps of five, and whether it is off, which keeps the
/// threshold and says nothing.
/// </summary>
public sealed record ObjectAlarm(string Measure, int Threshold = ObjectAlarm.DefaultThreshold, bool Off = false)
{
    public const int DefaultThreshold = 90;
}

/// <summary>
/// One of the dashboard's alarms shown in the status bar as well (the owner,
/// 26 September 2026, as today's Home): the object that holds it and the
/// measure. Each view keeps its own, since a phone's bar holds fewer.
/// </summary>
public sealed record StatusAlarm(string Object, string Measure);
