using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.UI;

// from http://blog.stevensanderson.com/2008/10/15/partial-output-caching-in-aspnet-mvc/
// hacked slightly to allow for 0 url parameter pages to be cached

namespace CTCClassSchedule
{
	public class ActionOutputCacheAttribute : ActionFilterAttribute
	{
		// This hack is optional;
		private static MethodInfo _switchWriterMethod = typeof(HttpResponse).GetMethod("SwitchWriter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

		public ActionOutputCacheAttribute(string cacheDuration = "DefaultCacheTime")
		{
			if (cacheDuration == "DefaultCacheTime")
			{
				_cacheDuration = 1800;
			}
			else
			{
				_cacheDuration = Convert.ToInt32(ConfigurationManager.AppSettings[cacheDuration]);
			}

		}

		private int _cacheDuration;
		private TextWriter _originalWriter;
		private string _cacheKey;

		public override void OnActionExecuting(ActionExecutingContext filterContext)
		{
			_cacheKey = ComputeCacheKey(filterContext);
			string cachedOutput = (string)filterContext.HttpContext.Cache[_cacheKey];
			if (cachedOutput != null)
				filterContext.Result = new ContentResult { Content = cachedOutput };
			else
				_originalWriter = (TextWriter)_switchWriterMethod.Invoke(HttpContext.Current.Response, new object[] { new HtmlTextWriter(new StringWriter()) });
		}

		public override void OnResultExecuted(ResultExecutedContext filterContext)
		{
			if (_originalWriter != null) // Must complete the caching
			{
				//need the catch/finally in place to prevent the app from halting when the caching
				// mechanism attempts to convert HttpWriter to HtmlTextWriter upon fresh page reads
				try
				{
					HtmlTextWriter cacheWriter = (HtmlTextWriter)_switchWriterMethod.Invoke(HttpContext.Current.Response, new object[] { _originalWriter });
					string textWritten = ((StringWriter)cacheWriter.InnerWriter).ToString();
					filterContext.HttpContext.Response.Write(textWritten);

					filterContext.HttpContext.Cache.Add(_cacheKey, textWritten, null, DateTime.Now.AddSeconds(_cacheDuration), Cache.NoSlidingExpiration, System.Web.Caching.CacheItemPriority.Normal, null);
				}
				catch(Exception ex)
				{
					Debug.Print("Intentionally ignoring exception: {0}", ex);
				}
				finally { }
			}
		}

		private string ComputeCacheKey(ActionExecutingContext filterContext)
		{
			var keyBuilder = new StringBuilder();
			foreach (var pair in filterContext.RouteData.Values)
				keyBuilder.AppendFormat("rd{0}_{1}_", pair.Key.GetHashCode(), pair.Value.GetHashCode());
			foreach (var pair in filterContext.ActionParameters)
				keyBuilder.AppendFormat("ap{0}_{1}_", pair.Key.GetHashCode(), pair.Value == null ? 0 : pair.Value.GetHashCode());
			return keyBuilder.ToString();
		}
	}
}