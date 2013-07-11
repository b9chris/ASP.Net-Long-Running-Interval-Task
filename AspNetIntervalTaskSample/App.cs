using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;

using Brass9.Threading;


namespace AspNetIntervalTaskSample
{
	public class App
	{
		public static int Counter { get; set; }

		public static void Init()
		{
			var random = new Random();

			var task = IntervalTask.CreateTask(() =>
			{
				for (int i = 0; i < 10; i++)
				{
					if (IntervalTask.Current.ShuttingDown)
					{
						// ASP.Net is shutting down - stop doing heavy stuff
						// and quit out gracefully
						return;
					}

					// Sleeps anywhere between 100 and 2000ms to simulate variable
					// work loads
					Thread.Sleep(random.Next(100, 2000));
					App.Counter++;

					// We don't have to worry about multithreading issues on these
					// properties because the IntervalTask guarantees there will
					// only ever be at most one running.
				}
			});

			// Interval is 5 seconds - a real scenario would likely be much longer,
			// like 15 minutes
			task.SetInterval(5000);
		}

		public static void End()
		{
			IntervalTask.Current.Dispose();
		}
	}
}