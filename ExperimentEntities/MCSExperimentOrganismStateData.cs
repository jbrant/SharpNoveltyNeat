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
    
    public partial class MCSExperimentOrganismStateData
    {
        public int ExperimentDictionaryID { get; set; }
        public int Run { get; set; }
        public int Generation { get; set; }
        public int Evaluation { get; set; }
        public bool IsViable { get; set; }
        public bool StopConditionSatisfied { get; set; }
        public double DistanceToTarget { get; set; }
        public double AgentXLocation { get; set; }
        public double AgentYLocation { get; set; }
    
        public virtual ExperimentDictionary ExperimentDictionary { get; set; }
    }
}
