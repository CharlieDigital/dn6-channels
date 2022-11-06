/// <summary>
/// Represents an event in an origin calendar system.
/// </summary>
/// <param name="StartTime">The start time of the event.</param>
/// <param name="EndTime">The end time of th event.</param>
/// <param name="Title">The title of the event/param>
public record CalendarEvent(
  DateTimeOffset StartTime,
  DateTimeOffset EndTime,
  string Title
);