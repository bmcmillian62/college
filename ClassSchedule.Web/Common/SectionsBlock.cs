using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CTCClassSchedule.Models;
using Ctc.Ods.Types;

namespace CTCClassSchedule
{
	public class SectionsBlock
	{
		/// <summary>
		/// Collection of all sections of the same course, grouped into a block
		/// </summary>
		public IEnumerable<SectionWithSeats> Sections { get; set; }

		/// <summary>
		/// Collection of all linked sections, where the key is the item number and the value
		/// is is an array of linked sections.
		/// </summary>
		public List<SectionWithSeats> LinkedSections { get; set; }

		/// <summary>
		/// Collection of footnotes shared by all sections of the block
		/// </summary>
		public IEnumerable<string> CommonFootnotes { get; set; }

	  /// <summary>
    /// Indicates whether the current block of <see cref="Section"/>s (e.g. a <see cref="Course"/>)
    /// is cross-listed
    /// </summary>
	  public bool IsCrosslisted {get;set;}
	}
}