using System.Diagnostics;
using System.Threading.Channels;

// Create our channel.
var channel = Channel.CreateUnbounded<CalendarEvent>();
var reader = channel.Reader;
var writer = channel.Writer;

// Start our scheduler.
var schedulerTask = new Scheduler(reader).ProcessAsync();

// Create our calendar retrieval tasks.
var googleCalendarTask = Task.Run(async () =>
  await new GoogleEventProvider(writer).RunAsync()
);

var outlookCalendarTask = Task.Run(async () =>
  await new OutlookEventProvider(writer).RunAsync()
);

var appleCalendarTask = Task.Run(async () =>
  await new AppleEventProvider(writer).RunAsync()
);

// Our stopwatch to check the start/end time.
var stopwatch = new Stopwatch();
stopwatch.Start();

// Now start our tasks execute and complete concurrently.
await Task.WhenAll(
  googleCalendarTask,
  outlookCalendarTask,
  appleCalendarTask
).ContinueWith( _ => writer.Complete() );

stopwatch.Stop();

await schedulerTask;

// Write out our time metrics; should be around ~3s even though we
// make total of 9 simualted calls with up to 1s each.
var elapsedSeconds = TimeSpan.FromMilliseconds(
  stopwatch.ElapsedMilliseconds
).TotalSeconds;

Console.WriteLine($"Completed in {elapsedSeconds}s");