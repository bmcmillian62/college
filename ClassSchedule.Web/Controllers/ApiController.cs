/*
This file is part of CtcClassSchedule.

CtcClassSchedule is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

CtcClassSchedule is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with CtcClassSchedule.  If not, see <http://www.gnu.org/licenses/>.
 */
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web.Mvc;
using Common.Logging;
using Ctc.Ods.Data;
using Ctc.Ods.Types;
using CTCClassSchedule.Common;
using CTCClassSchedule.Models;
using System;
using CtcApi.Extensions;
using CtcApi.Web.Mvc;
using System.Diagnostics;

namespace CTCClassSchedule.Controllers
{
	public class ApiController : Controller
	{
    // Any section that has more than this many courses cross-listed with it will produce a warning in the application log.
    const int MAX_COURSE_CROSSLIST_WARNING_THRESHOLD = 10;
    private readonly ILog _log = LogManager.GetLogger(typeof(ApiController));

	  public const int MAX_COURSE_PREFIXES = 5;

	  public ApiController()
	  {
			ViewBag.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
		}

    //
    // POST: /Api/Subjects
    /// <summary>
    /// Retrieves and updates Seats Available data for the specified <see cref="Section"/>
    /// </summary>
    /// <param name="classID"></param>
    /// <returns></returns>
    public ActionResult GetSeats(string classID)
    {
      int? seats = null;
      string friendlyTime = string.Empty;

      string itemNumber = classID.Substring(0, 4);
      string yrq = classID.Substring(4, 4);

      CourseHPQuery query = new CourseHPQuery();
      int hpSeats = query.FindOpenSeats(itemNumber, yrq);

      using (ClassScheduleDb db = new ClassScheduleDb())
      {
        //if the HP query didn't fail, save the changes. Otherwise, leave the SeatAvailability table alone.
        //This way, the correct number of seats can be pulled by the app and displayed instead of updating the
        //table with a 0 for seats available.
        if (hpSeats >= 0)
        {
          IQueryable<SectionSeat> seatsAvailableLocal = from s in db.SectionSeats
                                                        where s.ClassID == classID
                                                        select s;
          int rows = seatsAvailableLocal.Count();

          if (rows > 0)
          {
            // TODO: Should only be updating one record
            //update the value
            foreach (SectionSeat seat in seatsAvailableLocal)
            {
              seat.SeatsAvailable = hpSeats;
              seat.LastUpdated = DateTime.Now;
            }
          }
          else
          {
            //insert the value
            SectionSeat newseat = new SectionSeat();
            newseat.ClassID = classID;
            newseat.SeatsAvailable = hpSeats;
            newseat.LastUpdated = DateTime.Now;

            db.SectionSeats.Add(newseat);
          }

          db.SaveChanges();
        }

        // retrieve updated seats data
        IQueryable<SectionSeat> seatsAvailable = from s in db.SectionSeats
                                                 where s.ClassID == classID
                                                 select s;

        SectionSeat newSeat = seatsAvailable.First();

        seats = newSeat.SeatsAvailable;
        friendlyTime = newSeat.LastUpdated.GetValueOrDefault().ToString("h:mm tt").ToLower();
      }

      string jsonReturnValue = string.Format("{0}|{1}", seats, friendlyTime);
      return Json(jsonReturnValue);
    }

		//
		// POST: /Api/Subjects
		/// <summary>
		/// Returns an array of <see cref="Course"/> Subjects
		/// </summary>
		/// <param name="format"></param>
		///<param name="YearQuarter"></param>
		///<returns>
		///		Either a <see cref="PartialViewResult"/> which can be embedded in an MVC View,
		///		or the list of <see cref="Course"/> Subjects as a JSON array.
		/// </returns>
		/// <remarks>
		///		To receive the list as a JSON array call this method with <i>format=json</i>:
		///		<example>
		///			http://localhost/Api/Subjects?format=json
		///		</example>
		/// </remarks>
		public ActionResult Subjects(string format, string YearQuarter)
		{
		  _log.Trace(m => m("Called: [classes/Api/Subjects?format={0}&YearQuarter={1}], From (referrer): [{2}]", format, YearQuarter, Request.UrlReferrer));

		  IList<ScheduleCoursePrefix> subjectList = GetSubjectList(YearQuarter);

      if (format == "json")
			{
				// NOTE: AllowGet exposes the potential for JSON Hijacking (see http://haacked.com/archive/2009/06/25/json-hijacking.aspx)
				// but is not an issue here because we are receiving and returning public (e.g. non-sensitive) data
				return Json(subjectList, JsonRequestBehavior.AllowGet);
			}

		  FacetHelper facetHelper = new FacetHelper(Request);
		  ViewBag.LinkParams = facetHelper.LinkParameters;
			ViewBag.SubjectsColumns = 2;

      return PartialView(subjectList);
    }

	  /// <summary>
	  /// Retrieves course data for the prefixes specified
	  /// </summary>
	  /// <param name="prefix"></param>
	  /// <returns></returns>
    public ActionResult Courses(params string[] prefix)
	  {
      IList<Course> courses;
      using (OdsRepository repository = new OdsRepository())
	    {
	      if (prefix != null && prefix.Length > 0)
	      {
	        if (prefix.Length > MAX_COURSE_PREFIXES)
	        {
	          return new HttpStatusCodeResult((int)HttpStatusCode.BadRequest, string.Format("Requests are limited to {0} Course prefixes.", MAX_COURSE_PREFIXES));
	        }
	        if (prefix.Length > 1)
	        {
	          courses = repository.GetCourses(prefix.ToList());
	        }
	        else
	        {
	          courses = repository.GetCourses(prefix[0]);
	        }
	      }
	      else
	      {
	        return new HttpStatusCodeResult((int)HttpStatusCode.BadRequest, "Please provide one or more subject abbreviations.");
	      }
	    }

	    // NOTE: AllowGet exposes the potential for JSON Hijacking (see http://haacked.com/archive/2009/06/25/json-hijacking.aspx)
      // but is not an issue here because we are receiving and returning public (e.g. non-sensitive) data
      return Json(courses.Distinct().OrderBy(c => c.Subject).ThenBy(c => c.Number), JsonRequestBehavior.AllowGet);
	  }

    /// <summary>
	  ///
	  /// </summary>
	  /// <param name="courseID"></param>
	  /// <param name="yearQuarterID"></param>
	  /// <returns>
	  ///   <example>
	  ///     <code>
	  ///     [{"ID":{"Subject":"ENGL","Number":"101","IsCommonCourse":false},"Title":"English Composition I","Credits":5.0,"IsVariableCredits":false,"IsCommonCourse":true}]
	  ///     </code>
	  ///   </example>
	  /// </returns>
	  [HttpGet]
    public JsonResult CrossListedCourses(string courseID, string yearQuarterID)
	  {
	    // TODO: Validate incoming yearQuarterID value
      // see https://github.com/BellevueCollege/CtcApi/issues/8

	    if (yearQuarterID.Length != 4)
	    {
	      _log.Error(m => m("An invalid YearQuarterID was provided for looking up cross-listed sections: '{0}'", yearQuarterID));
	    }
	    else
	    {
	      using (ClassScheduleDb db = new ClassScheduleDb())
	      {
	        string[] classIDs = (from c in db.SectionCourseCrosslistings
	                             where c.CourseID == courseID
	                                   && c.ClassID.EndsWith(yearQuarterID)
	                             select c.ClassID).ToArray();

	        if (!classIDs.Any())
	        {
            _log.Warn(m => m("No cross-listed Sections were found for Course '{0}' in '{1}'", courseID, yearQuarterID));
          }
	        else
	        {
	          int count = classIDs.Count();
	          _log.Trace(m => m("{0} cross-listed sections found for '{1}': [{2}]", count, courseID, classIDs.Mash()));

            // HACK: SectionID constructors are currently protected, so we have to manually create them
	          IList<ISectionID> sectionIDs = new List<ISectionID>(count);
	          foreach (string id in classIDs)
	          {
	            sectionIDs.Add(SectionID.FromString(id));
	          }

	          // write a warning to the log if we've got too many courses cross-listed with this section
	          if (_log.IsWarnEnabled && sectionIDs.Count > MAX_COURSE_CROSSLIST_WARNING_THRESHOLD)
	          {
	            _log.Warn(m => m("Cross-listing logic assumes a very small number of Sections will be cross-listed with any given Course, but is now being asked to process {0} Sections for '{1}'. (This warning triggers when more than {2} are detected.)",
	                              sectionIDs.Count, courseID, MAX_COURSE_CROSSLIST_WARNING_THRESHOLD));
	          }

	          using (OdsRepository ods = new OdsRepository())
	          {
	            IList<Section> odsSections = ods.GetSections(sectionIDs);

	            IList<SectionWithSeats> classScheduleSections = Helpers.GetSectionsWithSeats(yearQuarterID,
	                                                                                         odsSections.ToList(), db);

	            IList<CrossListedCourseModel> crosslistings = (from c in classScheduleSections
	                                                           select new CrossListedCourseModel
	                                                                    {
	                                                                      // BUG: API doesn't property notify CourseID in derived class
	                                                                      CourseID = CourseID.FromString(c.CourseID),
	                                                                      SectionID = c.ID,
	                                                                      // HACK: Remove IsCommonCourse property when API is fixed (see above)
	                                                                      IsCommonCourse = c.IsCommonCourse,
	                                                                      Credits = c.Credits,
	                                                                      IsVariableCredits = c.IsVariableCredits,
	                                                                      Title = c.CourseTitle
	                                                                    }).ToList();

	            // NOTE: AllowGet exposes the potential for JSON Hijacking (see http://haacked.com/archive/2009/06/25/json-hijacking.aspx)
	            // but is not an issue here because we are receiving and returning public (e.g. non-sensitive) data
	            return Json(crosslistings, JsonRequestBehavior.AllowGet);
	          }
	        }
	      }
	    }
	    return Json(null, JsonRequestBehavior.AllowGet);
	  }


	  // TODO: If save successful, return new (possibly trimmed) footnote text
		/// <summary>
		/// Attempts to update a given sections footnote. If no footnote exists for the section, one is added.
		/// If the new footnote text is identical to the original, no changes are made.
		/// </summary>
		/// <param name="classId">The class ID of to modify (also section ID)</param>
		/// <param name="newFootnoteText">The text which will become the new footnote</param>
		/// <returns>Returns a JSON boolean true value if the footnote was modified</returns>
		[HttpPost]
		[AuthorizeFromConfig(RoleKey = "ApplicationEditor")]
		public ActionResult UpdateSectionFootnote(string classId, string newFootnoteText)
		{
			bool result = false;

			// Trim any whitespace
			if (!String.IsNullOrEmpty(newFootnoteText))
			{
				newFootnoteText = newFootnoteText.Trim();
			}


			if (HttpContext.User.Identity.IsAuthenticated == true)
			{
				using (ClassScheduleDb db = new ClassScheduleDb())
				{
					IQueryable<SectionsMeta> footnotes = db.SectionsMetas.Where(s => s.ClassID == classId);

					if (footnotes.Count() > 0)
					{
						// Should only update one section
						foreach (SectionsMeta footnote in footnotes)
						{
							if (!String.Equals(footnote.Footnote, newFootnoteText))
							{
								footnote.Footnote = newFootnoteText;
								footnote.LastUpdated = DateTime.Now;
								//footnote.LastUpdatedBy

								result = true;
							}
						}
					}
					else if (classId != null && !String.IsNullOrWhiteSpace(newFootnoteText))
					{
						// Insert footnote
						SectionsMeta newFootnote = new SectionsMeta();
						newFootnote.ClassID = classId;
						newFootnote.Footnote = newFootnoteText;
						newFootnote.LastUpdated = DateTime.Now;

						db.SectionsMetas.Add(newFootnote);
						result = true;
					}

					db.SaveChanges();
				}
			}

			return Json(new { result = result, footnote = newFootnoteText });
		}

    /// <summary>
    ///
    /// </summary>
    /// <param name="CourseNumber"></param>
    /// <param name="Subject"></param>
    /// <param name="IsCommonCourse"></param>
    /// <returns></returns>
		[AuthorizeFromConfig(RoleKey = "ApplicationAdmin")]
		[OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
		public ActionResult ClassEdit(string CourseNumber, string Subject, bool IsCommonCourse)
		{

			if (HttpContext.User.Identity.IsAuthenticated)
			{
				string fullCourseId = Helpers.BuildCourseID(CourseNumber, Subject, IsCommonCourse);
        ICourseID courseID = CourseID.FromString(fullCourseId);

				CourseMeta itemToUpdate = null;
				var hpFootnotes = string.Empty;
				string courseTitle = string.Empty;

				using (ClassScheduleDb db = new ClassScheduleDb())
				{
					if(db.CourseMetas.Any(s => s.CourseID.Trim().ToUpper() == fullCourseId.ToUpper()))
					{
						//itemToUpdate = db.CourseFootnotes.Single(s => s.CourseID.Substring(0, 5).Trim().ToUpper() == subject.Trim().ToUpper() &&
						//																				 s.CourseID.Trim().EndsWith(courseID.Number)
						itemToUpdate = db.CourseMetas.Single(s => s.CourseID.Trim().ToUpper() == fullCourseId.ToUpper());
					}

					using (OdsRepository repository = new OdsRepository())
					{
					  try
						{
							IList<Course> coursesEnum = repository.GetCourses(courseID);

							foreach (Course course in coursesEnum)
							{
							  hpFootnotes = course.Footnotes.ToArray().Mash(" ");

                // BUG: If more than one course is returned from the API, this will ignore all Titles except for the last one
								courseTitle = course.Title;
							}
						}
						catch(InvalidOperationException ex)
						{
							_log.Warn(m => m("Ignoring Exception while attempting to retrieve footnote and title data for CourseID '{0}'\n{1}", courseID, ex));
						}
					}

				  ClassFootnote localClass = new ClassFootnote();
					localClass.CourseID = MvcApplication.SafePropertyToString(itemToUpdate, "CourseID", fullCourseId);
					localClass.Footnote = MvcApplication.SafePropertyToString(itemToUpdate, "Footnote", string.Empty);
					localClass.HPFootnote = hpFootnotes;
					localClass.LastUpdated = MvcApplication.SafePropertyToString(itemToUpdate, "LastUpdated", string.Empty);
					localClass.LastUpdatedBy = MvcApplication.SafePropertyToString(itemToUpdate, "LastUpdatedBy", string.Empty);
					localClass.CourseTitle = courseTitle;

					return PartialView(localClass);
				}
			}

			return PartialView();
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="collection"></param>
		/// <returns></returns>
		[HttpPost]
		[ValidateInput(false)]
		[AuthorizeFromConfig(RoleKey = "ApplicationAdmin")]
		public ActionResult ClassEdit(FormCollection collection)
		{
			string referrer = collection["referrer"];

			if (HttpContext.User.Identity.IsAuthenticated)
			{
				string courseId = collection["CourseID"];
				string username = HttpContext.User.Identity.Name;
				string footnote = collection["Footnote"];

				footnote = Helpers.StripHtml(footnote);

				if (ModelState.IsValid)
				{
          CourseMeta itemToUpdate;

          using (ClassScheduleDb db = new ClassScheduleDb())
					{
					  bool exists = db.CourseMetas.Any(c => c.CourseID == courseId);
					  itemToUpdate = exists ? db.CourseMetas.Single(c => c.CourseID == courseId) : new CourseMeta();

						itemToUpdate.CourseID = courseId;
						itemToUpdate.Footnote = footnote;
						itemToUpdate.LastUpdated = DateTime.Now;
						itemToUpdate.LastUpdatedBy = username;

					  if (!exists)
					  {
					    db.CourseMetas.Add(itemToUpdate);
					  }
                        try
                        {
                            db.SaveChanges();
                        }
                        catch (System.Data.Entity.Validation.DbEntityValidationException e)
                        {
                            foreach (var eve in e.EntityValidationErrors)
                            {
                                _log.Debug(m => m("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                    eve.Entry.Entity.GetType().Name, eve.Entry.State));
                                foreach (var ve in eve.ValidationErrors)
                                {
                                    _log.Debug(m => m("- Property: \"{0}\", Value: \"{1}\", Error: \"{2}\"",
                                        ve.PropertyName,
                                        eve.Entry.CurrentValues.GetValue<object>(ve.PropertyName),
                                        ve.ErrorMessage));
                                }
                            }
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _log.Error(m => m("Class changes NOT saved - {0}", ex));
                            throw;
                        }
                        //db.SaveChanges();
                    }
				}
			}

			return Redirect(referrer);
		}

    /// <summary>
    /// GET: /API/Export/{YearQuarterID}
    /// </summary>
    /// <returns>An Adobe InDesign formatted text file with all course data.</returns>
    [AuthorizeFromConfig(RoleKey = "ApplicationEditor")]
    public FileResult Export(String YearQuarterID)
    {
      if (HttpContext.User.Identity.IsAuthenticated == true)
      {
        // Determine whether to use a specific Year Quarter, or the current Year Quarter
        YearQuarter yrq;
        if (String.IsNullOrEmpty(YearQuarterID))
        {
          using (OdsRepository _db = new OdsRepository())
          {
            yrq = _db.CurrentYearQuarter;
          }
        }
        else
        {
          yrq = YearQuarter.FromString(YearQuarterID);
        }

        // Return all the Class Schedule Data as a file
        return ClassScheduleExporter.GetFile(yrq);
      }

      throw new UnauthorizedAccessException("You do not have sufficient privileges to export course data.");
    }

	  #region Static methods
	  /// <summary>
	  ///
	  /// </summary>
	  /// <param name="yearQuarter"></param>
	  /// <returns></returns>
	  private static IList<ScheduleCoursePrefix> GetSubjectList(string yearQuarter)
	  {
	    IList<ScheduleCoursePrefix> subjectList;
	    using (OdsRepository db = new OdsRepository())
	    {
	      IList<CoursePrefix> data;
	      data = string.IsNullOrWhiteSpace(yearQuarter) || yearQuarter.Equals("All", StringComparison.OrdinalIgnoreCase)
	               ? db.GetCourseSubjects()
	               : db.GetCourseSubjects(YearQuarter.FromFriendlyName(yearQuarter));
	      IList<string> apiSubjects = data.Select(s => s.Subject).ToList();

	      using (ClassScheduleDb classScheduleDb = new ClassScheduleDb())
	      {
	        // Entity Framework doesn't support all the functionality that LINQ does, so first grab the records from the database...
	        var dbSubjects = (from s in classScheduleDb.Subjects
	                          join p in classScheduleDb.SubjectsCoursePrefixes on s.SubjectID equals p.SubjectID
	                          select new
	                                   {
	                                     s.Slug,
	                                     p.CoursePrefixID,
	                                     s.Title
	                                   })
	          .ToList();
	        // ...then apply the necessary filtering (e.g. TrimEnd() - which isn't fully supported by EF
	        subjectList = (from s in dbSubjects
	                       // TODO: replace hard-coded '&' with CommonCourseChar from .config
	                       where apiSubjects.Contains(s.CoursePrefixID.TrimEnd('&'))
	                       select new ScheduleCoursePrefix
	                                {
	                                  Slug = s.Slug,
	                                  Subject = s.CoursePrefixID,
	                                  Title = s.Title
	                                })
	          .OrderBy(s => s.Title)
	          .Distinct()
	          .ToList();
	      }
	    }
	    return subjectList;
	  }

	  #endregion

	  #region Private methods
	  #endregion
	}
}
