using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using Brass9.Threading.AspNetIntervalTask;

using AspNetIntervalTaskSample.ViewModels;


namespace AspNetIntervalTaskSample.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

		public ActionResult Stat()
		{
			var intervalTask = IntervalTask.Current;

			var vm = new TaskStats
			{
				Counter = App.Counter.ToString(),
				TimerWokeup = formatDateTime(intervalTask.TimerWokeup),
				TimerStarted = formatDateTime(intervalTask.TimerStarted),
				TaskStarted = formatDateTime(intervalTask.TaskStarted),
				TaskEnded = formatDateTime(intervalTask.TaskEnded)
			};

			return Json(vm);
		}

		protected string formatDateTime(DateTime dateTime)
		{
			if (dateTime == DateTime.MinValue)
				return "";

			return dateTime.ToLocalTime().ToString("h:mm:sstt").ToLower();
		}
    }
}
