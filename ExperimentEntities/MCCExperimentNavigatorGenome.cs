//------------------------------------------------------------------------------
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
    using System.Collections.Generic;
    
    public partial class MCCExperimentNavigatorGenome
    {
        public int ExperimentDictionaryID { get; set; }
        public int Run { get; set; }
        public int GenomeID { get; set; }
        public string GenomeXml { get; set; }
        public int RunPhase_FK { get; set; }
    
        public virtual RunPhase RunPhase { get; set; }
        public virtual ExperimentDictionary ExperimentDictionary { get; set; }
    }
}
