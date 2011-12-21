using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AspNetIntervalTaskSample.ViewModels
{
	public class TaskStats
	{
		public string Counter { get; set; }
		public string TimerWokeup { get; set; }
		public string TimerStarted { get; set; }
		public string TaskStarted { get; set; }
		public string TaskEnded { get; set; }
	}
}