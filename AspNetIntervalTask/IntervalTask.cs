/* Copyright 2011 Chris Moschini, Brass Nine Design
 * 
 * This code is licensed under the LGPL or MIT license, whichever you prefer.
 * 
 * If neither is compatible with your project, contact me at chris@brass9.com, and
 * I'll happily license it to you under whatever it is that meets your licensing
 * restrictions.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;


namespace Brass9.Threading.AspNetIntervalTask
{
	/// <summary>
	/// Starts a thread on a regular interval to perform a task at BelowNormal priority.
	/// 
	/// If your task thread runs longer than the timer interval, your thread is left be
	/// and the timer simply notices it's running and goes back to sleep. It will keep
	/// waking up at the specified interval until the task completes. The next time it
	/// awakes it will start the task again.
	/// 
	/// The task is defined as an Action&lt;IntervalTaskContext&gt;, where the argument
	/// it takes is a context object that has a single property: the IntervalTask. An
	/// example Action:
	/// 
	/// var task = IntervalTask.Create(context =>
	/// {
	///		// Do stuff in the background
	/// });
	/// 
	/// Behind the scenes this class uses a standard System.Timer to briefly wake up and
	/// build a background thread running at BelowNormal priority. Although the Timer draws
	/// on the ThreadPool, the thread it creates does not, and all work performed occurs on
	/// this standard, lower-priority thread. The lower priority prevents the background
	/// work from slowing down any Request threads. Because the work occurs on a standard
	/// thread, you can do things like block, Sleep() and Join() without concern about
	/// harming the ThreadPool or impairing the ability of the server to service requests.
	/// 
	/// If the ASP.Net app needs to shut down for some reason, the timer will be stopped.
	/// If the task is currently running, it will be left be, but a property on the context
	/// object passed into it, context.IntervalTask.ShuttingDown, will flip to true. This
	/// signals ASP.Net is going to tear the AppDomain (and your thread with it) in 30
	/// seconds.
	/// 
	/// The task should check ShuttingDown regularly (every 5 seconds of work
	/// or so) to see if it should cut its work short. Doing so will allow you to avoid
	/// having the thread torn down in the middle of its work, potentially corrupting
	/// data. The property is cheap (the getter just gets a bool - no hidden work) so
	/// checking it more often isn't harmful.
	/// 
	/// For example, if you had a long-running task that wakes up and polls an API
	/// endpoint for updates on local data, the combination of the web call and following
	/// data updates might constitute a couple seconds of work for each data point to be
	/// updated. You most likely have a loop running over these data points, updating
	/// each via the API. You would want to check ShuttingDown at the top of this loop's
	/// body, and return if it's set to true, to ensure your task ends gracefully.
	/// 
	/// If your task is one that needs to run all-or-nothing, but may take longer
	/// than 30 seconds, you have an architectural problem to solve. Aside from the
	/// obvious solution of using database transactions, your task may have clean
	/// shorter-term stopping points that logging can track effectively, so any of these
	/// milestones can be reached and the task then abandoned, so when the app comes up
	/// next time it can check the log and resume effectively.
	/// 
	/// This class assumes the work on the background thread is synchronous in nature, or
	/// at least won't return from the Action until work is complete. Behind the scenes,
	/// Running is toggled on when the Action is entered and off when it completes. If
	/// the Action were to actually kick off a lot of background threads and complete
	/// before they finish, Running will be set to false when actually work is still being
	/// performed. Using Thread.Join() or the Task Parallel Library's .Wait() or .WaitAll()
	/// to keep the thread active until additional worker threads complete is recommended.
	/// 
	/// Each time the thread is kicked off, it does so on a new thread that must be
	/// constructed and spun up, so setting a tight interval of less than 5 seconds is
	/// probably not a performant fit. This class is in use in production code for
	/// long-running tasks with a 15-minute wakeup interval.
	/// </summary>
	public class IntervalTask : System.Web.Hosting.IRegisteredObject, IDisposable
	{
		/// <summary>
		/// The only instance; set with CreateTask() or ReplaceTask().
		/// </summary>
		public static IntervalTask Current { get; protected set; }

		/// <summary>
		/// Whether the timer is enabled; set with SetTimerInterval() or StopTimer().
		/// </summary>
		public bool Enabled { get; protected set; }

		/// <summary>
		/// Wakeup interval (task may run less frequently than this if it runs long).
		/// </summary>
		public int Interval { get; protected set; }

		/// <summary>
		/// Whether the task is running right now.
		/// </summary>
		public bool Running { get; protected set; }

		/// <summary>
		/// Stats: The last time SetTimerInterval() was called
		/// </summary>
		public DateTime TimerStarted { get; protected set; }

		/// <summary>
		/// Stats: The last time the timer wokeup; the task may not have started if
		/// an instance was already running.
		/// </summary>
		public DateTime TimerWokeup { get; protected set; }

		/// <summary>
		/// Stats: The last time the task was started
		/// </summary>
		public DateTime TaskStarted { get; protected set; }

		/// <summary>
		/// Stats: The last time the task ended
		/// </summary>
		public DateTime TaskEnded { get; protected set; }

		/// <summary>
		/// Whether the ASP.Net app domain is tearing down.
		/// Tasks should check this property before performing large tasks;
		/// if set to true, the task should cleanup and return to try to
		/// keep cleanup time under 30 seconds. If the task isn't running
		/// when this flag becomes set, the timer will not wake up to start
		/// it again.
		/// </summary>
		public bool ShuttingDown { get; protected set; }

		protected Timer intervalTimer;
		protected Action<IntervalTaskContext> taskAction;
		protected Thread taskThread;

		private object syncLock = new object();

		protected IntervalTask(Action<IntervalTaskContext> taskAction)
		{
			System.Web.Hosting.HostingEnvironment.RegisterObject(this);
			this.taskAction = taskAction;
		}

		/// <summary>
		/// Creates a new IntervalTask (and doesn't run it - call SetTimerInterval() to start it).
		/// taskAction format: context => { /* do work */ }
		/// If a task has already been created, throws a FieldAccessException.
		/// </summary>
		/// <param name="taskAction">An Action to be run on an interval</param>
		/// <returns>The created IntervalTask. Call SetTimerInterval() on the returned object to start it.</returns>
		public static IntervalTask CreateTask(Action<IntervalTaskContext> taskAction)
		{
			if (Current != null)
				throw new FieldAccessException("CreateTask requested, but a task already exists that would be disposed. Use ReplaceTask instead.");

			Current = new IntervalTask(taskAction);
			return Current;
		}

		/// <summary>
		/// Replaces the current IntervalTask singleton. Call SetTimerInterval() to start it.
		/// taskAction format: context => { /* do work */ }
		/// If no task has been created, throws a NullReferenceException.
		/// </summary>
		/// <param name="taskAction"></param>
		/// <returns></returns>
		public static IntervalTask ReplaceTask(Action<IntervalTaskContext> taskAction)
		{
			if (Current == null)
				throw new NullReferenceException("ReplaceTask requested, but no task to replace. Use CreateTask instead.");

			Current.Dispose();

			Current = new IntervalTask(taskAction);
			return Current;
		}

		/// <summary>
		/// If the background task timer is running, changes its interval.
		/// If the timer isn't running, starts the timer (and so, the background task).
		/// </summary>
		/// <param name="interval">The timer interval in milliseconds.</param>
		public void SetTimerInterval(int interval)
		{
			Enabled = (interval != Timeout.Infinite && interval > 0);
			Interval = interval;

			if (Enabled)
				TimerStarted = DateTime.UtcNow;

			if (intervalTimer == null)
			{
				// set dueTime to Interval as well, so first run happens after interval has passed the first time
				intervalTimer = new Timer(intervalCallback, null, Interval, Interval);
			}
			else
			{
				int dueTime;

				if (Interval == Timeout.Infinite)
				{
					dueTime = Timeout.Infinite;
				}
				else
				{
					// set dueTime to time remaining in new interval, or 0 if it has already elapsed
					int timeElapsed = (int)(DateTime.UtcNow - TimerWokeup).TotalMilliseconds;
					dueTime = Math.Max(0, Interval - timeElapsed);
				}
				intervalTimer.Change(dueTime, Interval);
			}
		}

		/// <summary>
		/// Stops the timer. If the background task is running when this is called, it's
		/// left be so it can finish it's work, but will not be woken up to start again
		/// until SetTimerInterval() is called with a positive value.
		/// 
		/// Convenience method. This has the same effect as calling
		/// SetTimerInterval(Timeout.Infinite);
		/// </summary>
		public void StopTimer()
		{
			SetTimerInterval(Timeout.Infinite);
		}

		protected void intervalCallback(object unusedTimerContext)
		{
			TimerWokeup = DateTime.UtcNow;

			lock (syncLock)
			{
				// We just woke up. Verify we aren't either still running from a previous wakeup,
				// or stopping because the app is shutting down, before we proceed.
				if (Running || ShuttingDown)
					return;

				// The task isn't running. Flag that we're now running so the next wakeup won't
				// start a second thread until we unflag.
				Running = true;
			}

			// Track wakeups that actually kick off the task
			TaskStarted = DateTime.UtcNow;

			// Build the thread and run the task action on it at BelowNormal
			taskThread = new Thread(taskActionWrapper);
			taskThread.Priority = ThreadPriority.BelowNormal;
			taskThread.IsBackground = true;
			taskThread.Start();
		}

		protected void taskActionWrapper()
		{
			taskAction(new IntervalTaskContext(this));

			TaskEnded = DateTime.UtcNow;

			lock (syncLock)
			{
				Running = false;

				if (ShuttingDown)
					System.Web.Hosting.HostingEnvironment.UnregisterObject(this);
			}
		}


		#region IRegisteredObject Members
		/// <summary>
		/// Call if the app is shutting down. Should only be called by the ASP.Net container.
		/// </summary>
		/// <param name="immediate">ASP.Net sets this to false first, then to true the second
		/// call 30 seconds later.</param>
		public void Stop(bool immediate)
		{
			// See: http://haacked.com/archive/2011/10/16/the-dangers-of-implementing-recurring-background-tasks-in-asp-net.aspx

			lock (syncLock)
			{
				ShuttingDown = true;

				if (!Running)
					this.Dispose();
			}
		}
		#endregion

		#region IDisposable Members
		public void Dispose()
		{
			StopTimer();
			System.Web.Hosting.HostingEnvironment.UnregisterObject(this);
		}
		#endregion
	}
}