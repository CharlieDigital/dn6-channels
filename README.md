# Using Channels in dotnet 6 with C# 10

Channels are a construct that simplifies concurrent execution and pipelining of data without the use of lock based algorithms.

If you're building apps that process large volumes of data or needs to interact with multiple APIs for some processing job, using channels can improve the overall performance of the application by allowing you to partition the data processing or execute those API calls concurrently while aggregating the results and/or performing post processing as a stream.

Let's take a look at how we can use channels in C# 10 with dotnet 6.
## The Use Case

To start with, imagine that we are building a calendar reconciliation application.  A user as two or more calendars (such as Google, Outlook, and iCloud Calendar) that we'd like to read from and find conflicting events.

One way to do this is to simply loop over each calendar and collect all of the events:

```
# Pseudo code:

// Use an interval tree to hold our events
var interval_tree = new IntervalTree()

while (has_more_google_events) {
  // API calls to get the google events; 3s
}

while (has_more_outlook_events) {
  // API calls to get the outlook events; 4s
}

while (has_more_icloud_events)) {
  // API calls to get the iCloud events; 3s
}
```

We can use the [`intervalTree`](https://github.com/mbuchetics/RangeTree) as a mechanism to represent the events as intervals so we can easily query to see where we have conflicts.

The problem with this approach is that each of the API calls to retrieve the events from the providers is I/O bound; in other words, most of the time is going to be spent on the network making the API call and doing this sequentially means that our code has to spend a lot of time waiting on the network.

If the calls take on average `[3s, 4s, 3s]`, then the total time to process this operation sequentially is 10s.  But if we could do this concurrently, we could process each operation in 4s instead.

Why does this matter?  In a serverless world, you're often billed by a compute/time metric.  So if you can perform the same task in less time -- especially I/O bound tasks which don't put pressure on the CPU -- you can save operating costs.

## Our Base Code

To simulate this, we're going to create a set of simple mock providers that are going to mock API calls that return sets of events from Google Calendar, Outlook, and Apple iCloud Calendar (we aren't going to actually make remote calls, we'll just pretend that we make the call and get back sets of events from each endpoint).

```csharp
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
```

Each of our providers just, for the purposes of simulation, creates a list of events that we'll simulate paging over:

```csharp
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
      MakeDate("2022-11-15 15:15"),
      MakeDate("2022-11-15 15:30"),
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
```

This would result in 3 "pages" of data being returned in the call to `GetCalendarEventsAsync`

## The Concurrent Execution

The core of the application is a simple setup of a set of concurrent calls that will use a `System.Threading.Channel` to communicate between the concurrent execution and the aggregation and conflict checker.

We start by creating our channel:

```csharp
// Create our channel.
var channel = Channel.CreateUnbounded<CalendarEvent>();
var reader = channel.Reader;
var writer = channel.Writer;
```

Now we immediately start our `Scheduler`:

```csharp
// Start our scheduler.
var scheduler = new Scheduler(reader);
var schedulerTask = scheduler.Process();
```

And set up our concurrent tasks for each of the three calendars:

```csharp
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
```

The code above doesn't actually execute yet; we want to start them concurrently:

```csharp
// Now start our tasks and run concurrently.
await Task.WhenAll(
  googleCalendarTask,
  outlookCalendarTask,
  appleCalendarTask
).ContinueWith(
  _ => writer.Complete()
);
```

And finally, we await our scheduler to finish:

```csharp
await schedulerTask;
```

## The Output

In our example, we'll get the following output:

```
[CONFLICT]
  2022-11-15 11:00 - 2022-11-15 11:30: Followup with accountant on tax prep
  2022-11-15 11:15 - 2022-11-15 11:30: Quick 1:1 with Patrick
--------
[CONFLICT]
  2022-11-15 09:00 - 2022-11-15 09:30: Morning standup with team
  2022-11-15 09:15 - 2022-11-15 09:30: Morning breathing exercises
--------
[CONFLICT]
  2022-11-15 14:30 - 2022-11-15 15:00: Project meeting with Acme
  2022-11-15 14:50 - 2022-11-15 14:55: Dont forget to take meds!
--------
[CONFLICT]
  2022-11-15 16:15 - 2022-11-15 16:30: Status check with Joan
  2022-11-15 16:15 - 2022-11-15 16:30: Pick up Amy
--------
[CONFLICT]
  2022-11-15 14:30 - 2022-11-15 15:00: Project meeting with Acme
  2022-11-15 14:45 - 2022-11-15 15:00: Afternoon breathing exercises
  2022-11-15 14:50 - 2022-11-15 14:55: Dont forget to take meds!
--------
[CONFLICT]
  2022-11-15 22:00 - 2022-11-15 22:45: Medidate before bed
  2022-11-15 22:10 - 2022-11-15 22:30: Evening breathing exercises
--------
[CONFLICT]
  2022-11-15 20:00 - 2022-11-15 20:30: Check in with off-shore team.
  2022-11-15 20:15 - 2022-11-15 20:30: Prep the guest bedroom for in laws
--------
Completed in 2.712s
```

Note that even though we run 9 total calls with a random sleep up to 1s, our execution completes in only 2.7s (in this case)!  Very cool and barely any work to make it concurrent!

Now let's look at how we actually use the channel.

## The Producer Side

This is the side that _writes_ to the channel.  In other words, as we make API calls and retrieve pages of events, we want to push those events the scheduler:

It's surpisingly simple:

```csharp
public async Task RunAsync() {
  CalendarEvent[] events;

  do {
    // Get next page of events.
    events = (await GetCalendarEventsAsync()).ToArray();

    if (events.Length == 0) {
      break; // No more pages; we got eerything.
    }

    foreach(var e in events) {
      // This is it!
      await _writer.WriteAsync(e);
    }
  } while (true);
}
```

As we get events, we just write them to the channel using the writer end.

## The Consumer Side

This is the side that _reads_ from the channel.  In this case, as our calendar event providers make API calls and return results and write them to the channel, we're going to use the `Scheduler` to read from the channel and check for conflicts.

It is also surprisingly simple:

```csharp
public async Task Process() {
  // Read CalendarEvents from the channel in a loop.
  while (await _reader.WaitToReadAsync()) {
    if (_reader.TryRead(out var calendarEvent)) {
      // We'll add each event to the IntervalTree that we're going
      // to use to test to see if we have overlaps.
      var start = calendarEvent.StartTime.ToUnixTimeSeconds();
      var end = calendarEvent.EndTime.ToUnixTimeSeconds();

      _schedule.Add(start, end, calendarEvent);

      // Noq query the tree to see if we have a conflict
      var events = _schedule.Query(start, end);

      if (events.Count() > 1) {
        // Handle the conflict
      }
    }
  }
}
```

## Wrap Up

`System.Threading.Channels` is one of the plethora of great reasons to consider using dotnet and C# for your backend or compute intensive tasks.  For I/O intensive tasks that can be run concurrently, using channels can dramatically improve performance and throughput.