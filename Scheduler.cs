using System.Text;
using System.Threading.Channels;
using IntervalTree;

/// <summary>
/// This is our scheduler that will consume the streams from the writers and let us
/// know which events have conflicts.
/// </summary>
public class Scheduler {
  private readonly ChannelReader<CalendarEvent> _reader;

  /// <summary>
  /// The internal interval tree representation of our schedule used for collision
  /// detection by checking if we have more than 1 entry at a given interval.  The
  /// interval tree allows us to detect overlaps.
  /// </summary>
  private readonly IntervalTree<long, CalendarEvent> _schedule
    = new IntervalTree<long, CalendarEvent>();

  /// <summary>
  /// Initialize our scheduler.
  /// </summary>
  /// <param name="reader">The reader side of the channel.</param>
  public Scheduler(ChannelReader<CalendarEvent> reader)
    => _reader = reader;

  /// <summary>
  /// The main method for our scehduler.
  /// </summary>
  public async Task Process() {
    while (await _reader.WaitToReadAsync()) {
      if (_reader.TryRead(out var calendarEvent)) {
        var start = calendarEvent.StartTime.ToUnixTimeSeconds();
        var end = calendarEvent.EndTime.ToUnixTimeSeconds();

        // Add the event to the tree
        _schedule.Add(
          start,
          end,
          calendarEvent);

        // Query to see if we have a conflict
        var events = _schedule.Query(start, end);

        if (events.Count() > 1) {
          var buffer = new StringBuilder();
          buffer.AppendLine("[CONFLICT]");

          events.Aggregate(buffer, (buffer, e) => {
            buffer.AppendFormat($"  {e.StartTime:yyyy-MM-dd HH:mm} - {e.EndTime:yyyy-MM-dd HH:mm}: {e.Title}");
            buffer.Append(Environment.NewLine);
            return buffer;
          });

          Console.WriteLine($"{buffer.ToString()}--------");
        }
      }
    }
  }
}