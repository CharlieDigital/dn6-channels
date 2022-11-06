using System.Threading.Channels;

/// <summary>
/// A mock provider of events from Google.
/// </summary>
public class GoogleEventProvider : EventProviderBase {
  /// <summary>
  /// We use this to hold our list of events that we are going to
  /// return.
  /// </summary>
  private readonly IEnumerable<CalendarEvent>[] _events;

  protected override IEnumerable<CalendarEvent>[] Events
    => _events;

  /// <summary>
  /// Simulates Google event source.
  /// </summary>
  public GoogleEventProvider(ChannelWriter<CalendarEvent> _writer)
    : base(_writer) {
    _events = new [] {
      // First set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 09:15"),
          MakeDate("2022-11-15 09:30"),
          "Morning breathing exercises"
        )
      },
      // Second set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 14:45"),
          MakeDate("2022-11-15 15:00"),
          "Afternoon breathing exercises"
        ),
      },
      // Third set.
      new CalendarEvent[] {
        new (
          MakeDate("2022-11-15 22:10"),
          MakeDate("2022-11-15 22:30"),
          "Evening breathing exercises"
        ),
      },
    };
  }
}