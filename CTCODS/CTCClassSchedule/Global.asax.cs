﻿using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using MvcMiniProfiler;

namespace CTCClassSchedule
{
	// Note: For instructions on enabling IIS6 or IIS7 classic mode,
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication
	{
		public static void RegisterGlobalFilters(GlobalFilterCollection filters)
		{
			filters.Add(new HandleErrorAttribute());
		}

		public static void RegisterRoutes(RouteCollection routes)
		{
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
			routes.IgnoreRoute("ScheduleError.aspx");
			//routes.IgnoreRoute("favicon.ico");

			 //map all URLs to this route, but then restrict which routes to ignore via the constraints
			//dictionary. So in this case, this route will match (and thus ignore) all requests for
			//favicon.ico (no matter which directory). Since we told routing to ignore this requests,
			//normal ASP.NET processing of these requests will occur.
			routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });

			// API calls the application exposes
			routes.MapRoute("ApiSubjects", "Api/Subjects", new { controller = "Api", action = "Subjects" });
			routes.MapRoute("ApiSectionEdit", "Api/SectionEdit", new { controller = "Api", action = "SectionEdit" });
			routes.MapRoute("ApiClassEdit", "Api/ClassEdit", new { controller = "Api", action = "ClassEdit" });
			routes.MapRoute("ApiProgramEdit", "Api/ProgramEdit", new { controller = "Api", action = "ProgramEdit" });

			// Authentication
			routes.MapRoute("LogOn", "Authenticate", new { controller = "Classes", action = "Authenticate" });
			routes.MapRoute("LogOut", "Logout", new { controller = "Classes", action = "Logout" });

			// Couese data export
			//	*** Temp route for testing purposes ***
			routes.MapRoute("CoursesExport", "Export/{YearQuarterID}", new { controller = "Classes", action = "Export", YearQuarterID = UrlParameter.Optional });

			// default application routes

			routes.MapRoute("getSeats", "getseats", new { controller = "Classes", action = "getSeats" });
			routes.MapRoute("ClassDetails", "{YearQuarterID}/{Subject}/{ClassNum}", new { controller = "Classes", action = "ClassDetails" });
			routes.MapRoute("Subject", "All/{Subject}", new { controller = "Classes", action = "Subject" });

			routes.MapRoute("YearQuarterSubject", "{YearQuarter}/{Subject}", new { controller = "Classes", action = "YearQuarterSubject" });
			routes.MapRoute("AllClasses", "All", new { controller = "Classes", action = "AllClasses" });
			routes.MapRoute("Search", "Search", new { controller = "Search", action = "Index" });
			routes.MapRoute("YearQuarter", "{YearQuarter}", new { controller = "Classes", action = "YearQuarter" });
			routes.MapRoute("Default", "", new { controller = "Classes", action = "Index" });
		}

		/// <summary>
		///
		/// </summary>
		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();

#if ENABLE_PROFILING
			// Add profiling view engine
			var copy = ViewEngines.Engines.ToList();
			ViewEngines.Engines.Clear();
			foreach (var item in copy)
			{
					ViewEngines.Engines.Add(new ProfilingViewEngine(item));
			}

			// Add profiling action filter
			GlobalFilters.Filters.Add(new ProfilingActionFilter());
#endif

			RegisterGlobalFilters(GlobalFilters.Filters);
			RegisterRoutes(RouteTable.Routes);
		}

		/// <summary>
		///
		/// </summary>
		protected void Application_BeginRequest()
		{
#if ENABLE_PROFILING
			MiniProfiler.Start();
#endif
		}

		protected void Application_AuthenticateRequest(Object sender, EventArgs e)
		{
#if ENABLE_PROFILING
			// MiniProfiler is having issues w/ DotNetCasClient
			// (specifically, it doesn't seem to be able to find it)
			MiniProfiler.Stop(discardResults: true);
#endif
		}

		/// <summary>
		///
		/// </summary>
		protected void Application_EndRequest()
		{
#if ENABLE_PROFILING
			MiniProfiler.Stop(discardResults: true);
#endif
		}

		/// <summary>
		/// Part of the non-MVC error handling system
		/// </summary>
		/// <remarks>
		/// This method is part of the non-MVC error handling. For MVC-specific error handling, see <see cref="RegisterGlobalFilters"/>.
		/// </remarks>
		/// <seealso cref="RegisterGlobalFilters"/>
		protected void Application_Error()
		{
			if (Server.GetLastError() != null)
			{
// ReSharper disable ConstantNullCoalescingCondition
				Exception ex = Server.GetLastError().GetBaseException() ?? Server.GetLastError();
// ReSharper restore ConstantNullCoalescingCondition

				Application["LastError"] = ex;
			}
		}
	}
}
