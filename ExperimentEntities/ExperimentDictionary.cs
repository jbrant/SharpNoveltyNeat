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
    
    public partial class ExperimentDictionary
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public ExperimentDictionary()
        {
            this.NoveltyExperimentOrganismStateDatas = new HashSet<NoveltyExperimentOrganismStateData>();
            this.NoveltyExperimentEvaluationDatas = new HashSet<NoveltyExperimentEvaluationData>();
        }
    
        public int ExperimentID { get; set; }
        public string ExperimentName { get; set; }
        public string ConfigurationFile { get; set; }
        public string ExperimentDomain { get; set; }
        public string ExperimentType { get; set; }
        public string AlgorithmType { get; set; }
        public Nullable<int> OffspringBatchSize { get; set; }
        public string BehaviorCharacterization { get; set; }
        public Nullable<int> ArchiveAdditionThreshold { get; set; }
        public Nullable<double> ArchiveThresholdDecreaseMultiplier { get; set; }
        public Nullable<double> ArchiveThresholdIncreaseMultiplier { get; set; }
        public Nullable<int> MaxGenerationalArchiveAddition { get; set; }
        public Nullable<int> MaxGenerationsWithoutArchiveAddition { get; set; }
        public Nullable<int> PopulationEvaluationFrequency { get; set; }
        public double ConnectionProportion { get; set; }
        public int PopulationSize { get; set; }
        public int NumSpecies { get; set; }
        public Nullable<double> ElitismProportion { get; set; }
        public Nullable<double> SelectionProportion { get; set; }
        public double AsexualProbability { get; set; }
        public double CrossoverProbability { get; set; }
        public double InterspeciesMatingProbability { get; set; }
        public double MutateConnectionWeightProbability { get; set; }
        public double MutateAddNeuronProbability { get; set; }
        public double MutateAddConnectionProbability { get; set; }
        public double MutateDeleteConnectionProbability { get; set; }
        public int ConnectionWeightRange { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<NoveltyExperimentOrganismStateData> NoveltyExperimentOrganismStateDatas { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<NoveltyExperimentEvaluationData> NoveltyExperimentEvaluationDatas { get; set; }
    }
}
