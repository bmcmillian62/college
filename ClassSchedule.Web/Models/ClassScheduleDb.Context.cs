﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CTCClassSchedule.Models
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class ClassScheduleDb : DbContext
    {
        public ClassScheduleDb()
            : base("name=ClassScheduleDb")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<ClassSearch> ClassSearches { get; set; }
        public virtual DbSet<CourseSearch> CourseSearches { get; set; }
        public virtual DbSet<Division> Divisions { get; set; }
        public virtual DbSet<CourseMeta> CourseMetas { get; set; }
        public virtual DbSet<SectionCourseCrosslisting> SectionCourseCrosslistings { get; set; }
        public virtual DbSet<SectionsMeta> SectionsMetas { get; set; }
        public virtual DbSet<SubjectsCoursePrefix> SubjectsCoursePrefixes { get; set; }
        public virtual DbSet<SectionSeat> SectionSeats { get; set; }
        public virtual DbSet<vw_Class> vw_Class { get; set; }
        public virtual DbSet<Department> Departments { get; set; }
        public virtual DbSet<Subject> Subjects { get; set; }
    }
}