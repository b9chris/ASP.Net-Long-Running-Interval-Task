using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;

using Brass9.Threading.AspNetIntervalTask;


namespace AspNetIntervalTaskSample
{
	public class App
	{
		public static int Counter { get; set; }

		public static void Init()
		{
			var random = new Random();

			var task = IntervalTask.CreateTask(context =>
			{
				for (int i = 0; i < 10; i++)
				{
					if (context.IntervalTask.ShuttingDown)
					{
						// ASP.Net is shutting down - stop doing heavy stuff
						// and quit out gracefully
						return;
					}

					// Sleeps anywhere between 100 and 2000ms to simulate variable
					// work loads
					Thread.Sleep(random.Next(100, 2000));
					App.Counter++;

					// Notice that we don't have to worry about multithreading issues on
					// these static properties because the IntervalTask guarantees there
					// will only ever be at most one running. Not advocating static
					// properties, just noting the threading considerations.
				}
			});

			// Interval is 5 seconds - a real scenario would likely be much longer,
			// like 15 minutes
			task.SetTimerInterval(5000);
		}

		public static void End()
		{
			IntervalTask.Current.Dispose();
		}
	}
}