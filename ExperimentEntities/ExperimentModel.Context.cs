﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ExperimentEntities
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class ExperimentDataEntities : DbContext
    {
        public ExperimentDataEntities()
            : base("name=ExperimentDataEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<ExperimentDictionary> ExperimentDictionaries { get; set; }
        public virtual DbSet<NoveltyExperimentOrganismStateData> NoveltyExperimentOrganismStateDatas { get; set; }
        public virtual DbSet<NoveltyExperimentEvaluationData> NoveltyExperimentEvaluationDatas { get; set; }
    }
}
