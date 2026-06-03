using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace JobCatcher
{
    public class Setting
    {
        public int Id { get; set; }
        public int LastVacancy { get; set; }
    }

    public class Profile
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Search { get; set; }
        public string Login { get; set; }
        public string Pass { get; set; }
        public string LoginFlRu { get; set; }
        public string PassFlRu { get; set; }
        public string ResumeIdHh { get; set; }
        public string ResumeLinkHh { get; set; }
        public int Salary { get; set; }
        public bool Remote { get; set; }
        public bool Answer { get; set; }
        public string Proxy { get; set; }
        public int LastVacancy { get; set; }
        public bool freelanceru { get; set; }
        public bool flru { get; set; }
    }

    public class Vacancy
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        [Index, StringLength(4)]
        public string Kind { get; set; }
        public string Site { get; set; }
        public string ProfileName { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string DateS { get; set; }
        public string City { get; set; }
        public string Company { get; set; }
        public string Salary { get; set; }
        [Index]
        public bool Viewed { get; set; }
        public bool Answered { get; set; }
        public string Content { get; set; }
        public string EmploymentType { get; set; }
        public string VacancyId { get; set; }
    }
}
