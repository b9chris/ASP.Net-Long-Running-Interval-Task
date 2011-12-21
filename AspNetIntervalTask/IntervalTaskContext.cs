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
using System.Text;


namespace Brass9.Threading.AspNetIntervalTask
{
	/// <summary>
	/// The object passed to the task action each time it kicks off.
	/// 
	/// Mostly here for future expansion/extension purposes. For example
	/// a future version might support a custom context object that's
	/// passed in during Task creation, which would show up as a property
	/// of this object. Existing uses of the code fulfill this need with
	/// a closure, however.
	/// </summary>
	public class IntervalTaskContext
	{
		public IntervalTask IntervalTask { get; protected set; }

		public IntervalTaskContext(IntervalTask intervalTask)
		{
			this.IntervalTask = intervalTask;
		}
	}
}
