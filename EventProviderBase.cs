using System.Globalization;
using System.Threading.Channels;

/// <summary>
/// Represents a provider which connects to a remote origin to retrieve a list of
/// calendar events.  In this case, we are just using this as a mock proxy for an
/// actual API call.
/// </summary>
public abstract class EventProviderBase {
  public static readonly string DatePattern = "yyyy-MM-dd HH:mm";
  protected static readonly Random Random = new Random();
  private readonly ChannelWriter<CalendarEvent> _writer;
  private int _index = 0;

  /// <summary>
  /// Get the channel writer to write events to the channel.
  /// </summary>
  protected ChannelWriter<CalendarEvent> Writer
    => _writer;

  /// <summary>
  /// Base constructor
  /// </summary>
  protected EventProviderBase(ChannelWriter<CalendarEvent> writer) {
    _writer = writer;
  }

  /// <summary>
  /// Given a date and time string, create a DateTimeOffset instance.
  /// </summary>
  /// <param name="dateTime">The string value yyyy-MM-dd HH:mm</param>
  /// <returns>A DateTimeOffset representing the specified time.</returns>
  protected DateTimeOffset MakeDate(string dateTime)
    => DateTimeOffset.ParseExact(
      dateTime,
      DatePattern,
      CultureInfo.InstalledUICulture.DateTimeFormat
    );

  /// <summary>
  /// Inheriting classes to provide a list of events for iteration.
  /// </summary>
  protected abstract IEnumerable<CalendarEvent>[] Events { get; }

  /// <summary>
  /// Provides a list of events; this is only for simulation.
  /// </summary>
  public async Task<IEnumerable<CalendarEvent>> GetCalendarEventsAsync() {
    // Simulate API call delay.
    await Task.Delay(Random.Next(250, 1000));

    if (_index > Events.Length - 1) {
      return Array.Empty<CalendarEvent>(); // Simulate no more events.
    }

    var events = Events[_index];
    _index++;
    return events;
  }

  /// <summary>
  /// The actual logic to do the retrieval would go here; in this code, we
  /// are just simulating.
  /// </summary>
  public async Task RunAsync() {
    CalendarEvent[] events;

    do {
      events = (await GetCalendarEventsAsync()).ToArray();

      if (events.Length == 0) {
        break;
      }

      foreach(var e in events) {
        await _writer.WriteAsync(e);
      }

  } while (true);
  }
}