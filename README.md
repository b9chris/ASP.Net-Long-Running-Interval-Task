#ASP.Net Long-Running Interval Task


Copyright 2011-2013 Chris Moschini, Brass Nine Design

This code is licensed under the LGPL or MIT license, whichever you prefer.

If neither is compatible with your project, contact me at chris@brass9.com, and I'll happily license it to you under whatever it is that meets your licensing restrictions.

##Overview

This library is meant to run a task on a regular interval in the background of an ASP.Net app. This is a simple way to skip the install/configuration burden of a Windows Service, Task Scheduler task, or Azure Worker.

Behind the scenes this class uses a standard System.Timer to briefly wake up and build a background thread running at BelowNormal priority. Although the Timer draws on the ThreadPool, the thread it creates does not, and all work performed occurs on this standard, lower-priority thread. The lower priority prevents the background work from slowing down any Request threads. Because the work occurs on a standard thread, you can do things like block, Sleep() and Join() without concern about harming the ThreadPool or impairing the ability of the server to service requests.

Skip the next paragraph if you get why a class like this would be useful.

###Background

I've worked on several ASP.Net projects where this sort of long-running task was needed, and the typical answer was "Use a Windows Service; ASP.Net tasks aren't meant to run long." Inevitably configuring that Windows Service created a major configuration burden that was never sorted out properly. Two instances of the thread would end up running causing overwrites, the service refused to start because of insufficient permissions, deployment would fail because of permission problems, or someone would have to remember to login and manually install the new Service each deployment - inevitably a missed detail resulting in old code working on data it shouldn't be.

###Why It's Useful

Using this library all of that goes out the window in favor of a conventional deployment. You can do a standard Publish from Visual Studio and the overwrite of Web.Config will restart the app. In a typical usage scenario, the IntervalTask is setup in Application_Start and finished in Application_End, causing it to restart when the application does (typically during a deploy).

##Caveats

There are 2 major caveats to using this class (that is, performing background work indefinitely from inside an ASP.Net app). One, ASP.Net is configured in IIS by default to recycle every 29 hours. This is easily resolved by modifying the Application Pool your app runs in to never do a timed recycle (set the interval to 0). See also:

http://haacked.com/archive/2011/10/16/the-dangers-of-implementing-recurring-background-tasks-in-asp-net.aspx 

Two, the app doesn't technically start until the first request. In situations where you for example restart a server to install a patch, a site with low traffic may not get its first visit for hours or even days, meaning the site, and the timer are dormant. You can resolve this with IIS 7.5 and a couple configuration tweaks:

http://weblogs.asp.net/scottgu/archive/2009/09/15/auto-start-asp-net-applications-vs-2010-and-net-4-0-series.aspx

Or you could write a script that visits all of your sites when the machine boots - not a bad idea for moderate traffic sites, so the cost of first request is spent serving a page to your startup script, not an unlucky user.

Of course if you don't have IIS 7.5 or the right permissions, you can't take either of these steps. You're then left with 3 options:

* Ignore it. If you have enough regular traffic, the app will generally be running, meaning so will the timer.

* Use something like a Service anyway - although it's likely you can't if you can't configure IIS.

* Write a simple task on another machine (dev machine if you have to) that just hits the servers shortly after their recycle every 29 hours to ensure the app always gets that first request promptly. You'd want to hit it after a restart as well.

##Sample

AspNetIntervalTaskSample is an ASP.Net MVC 3 Razor example site using IntervalTask. It includes an example task (just a bunch of Sleep() commands), and an example of how to use the ShuttingDown flag. Visit the root page to see it run.

##Included Classes

Included with this library is the underlying TimerInfo class it calls on. This is a wrapper class for the System.Threading.Timer class, that exposes the Interval the Timer is running on, whether it's currently active, simplified start (SetInterval()) and Stop methods, and a simplified callback that assumes you'll use a closure for any context, rather than passing in an object of some Context Class you have to carefully arrange.