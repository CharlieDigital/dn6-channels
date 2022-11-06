using System.Threading.Channels;
/// <summary>
/// A mock provider of events from Microsoft Outlook.
/// </summary>
public class OutlookEventProvider : EventProviderBase {
  /// <summary>
  /// We use this to hold our list of events that we are going to
  /// return.
  /// </summary>
  private readonly IEnumerable<CalendarEvent>[] _events;

  protected override IEnumerable<CalendarEvent>[] Events
    => _events;

  /// <summary>
  /// Simulates Outlook event source.
  /// </summary>
  public OutlookEventProvider(ChannelWriter<CalendarEvent> _writer)
    : base(_writer) {
    _events = new [] {
      // First set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 09:00"),
          MakeDate("2022-11-15 09:30"),
         "Morning standup with team"
        ),
        new (
          MakeDate("2022-11-15 11:15"),
          MakeDate("2022-11-15 11:30"),
          "Quick 1:1 with Patrick"
        ),
        new (
          MakeDate("2022-11-15 11:45"),
          MakeDate("2022-11-15 12:00"),
          "Order lunch for team."
        ),
      },
      // Second set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 14:30"),
          MakeDate("2022-11-15 15:00"),
          "Project meeting with Acme"
        ),
        new (
          MakeDate("2022-11-15 16:15"),
          MakeDate("2022-11-15 16:30"),
         "Status check with Joan"
        ),
        new (
          MakeDate("2022-11-15 17:45"),
          MakeDate("2022-11-15 18:00"),
          "Update status of all project plans"
        ),
      },
      // Third set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 20:00"),
          MakeDate("2022-11-15 20:30"),
          "Check in with off-shore team."
        ),
      },
    };
  }
}