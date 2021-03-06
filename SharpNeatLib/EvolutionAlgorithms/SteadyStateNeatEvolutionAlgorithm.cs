﻿#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Redzen.Numerics.Distributions;
using SharpNeat.Core;
using SharpNeat.DistanceMetrics;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Loggers;
using SharpNeat.SpeciationStrategies;
using SharpNeat.Utility;

#endregion

namespace SharpNeat.EvolutionAlgorithms
{
    /// <summary>
    ///     Implementation of a steady state NEAT evolution algorithm.
    ///     Incorporates:
    ///     - Speciation with fitness sharing.
    ///     - Creating offspring via both sexual and asexual reproduction.
    /// </summary>
    /// <typeparam name="TGenome">The genome type that the algorithm will operate on.</typeparam>
    public class SteadyStateComplexifyingEvolutionAlgorithm<TGenome> : AbstractComplexifyingEvolutionAlgorithm<TGenome>
        where TGenome : class, IGenome<TGenome>
    {
        #region Overridden Methods

        /// <summary>
        ///     Progress forward by one evaluation. Perform one iteration of the evolution algorithm.
        /// </summary>
        public override void PerformOneGeneration()
        {
            // Re-evaluate the fitness of the population after the specified number of evaluations have elapsed
            if (CurrentGeneration % _populationEvaluationFrequency == 0)
            {
                // Evaluate all genomes fitness, but don't run the simulation 
                // (this ensures that the total number of evaluations is not incremented)
                GenomeEvaluator.Evaluate(GenomeList, CurrentGeneration, false);

                // Clear all the species and respeciate
                ClearAllSpecies();
                SpeciationStrategy.SpeciateGenomes(GenomeList, SpecieList);

                // Sort the genomes in each specie. Fittest first (secondary sort - youngest first).
                SortSpecieGenomes();

                // Update the archive parameters and reset for next evaluation
                AbstractNoveltyArchive?.UpdateArchiveParameters();
            }

            // Calculate statistics for each specie (mean fitness and target size)
            SpecieStats[] specieStatsArr = CalcSpecieStats();

            // Produce number of offspring equivalent to the given batch size
            List<TGenome> childGenomes = CreateOffspring(specieStatsArr, _batchSize);

            // Evaluate the offspring batch
            GenomeEvaluator.Evaluate(childGenomes, GenomeList, CurrentGeneration);

            // Determine genomes to remove based on their adjusted fitness
            List<TGenome> genomesToRemove = SelectGenomesForRemoval(_batchSize);

            // Remove the worst individuals from the previous iteration
            (GenomeList as List<TGenome>)?.RemoveAll(x => genomesToRemove.Contains(x));

            // Add new children
            (GenomeList as List<TGenome>)?.AddRange(childGenomes);

            // Add each applicable genomes to archive based on whether they qualified
            foreach (TGenome childGenome in childGenomes)
            {
                AbstractNoveltyArchive?.TestAndAddCandidateToArchive(childGenome);
            }

            // Clear all the species and respeciate
            ClearAllSpecies();
            SpeciationStrategy.SpeciateGenomes(GenomeList, SpecieList);

            // Sort the genomes in each specie. Fittest first (secondary sort - youngest first).
            SortSpecieGenomes();

            // Update stats and store reference to best genome.
            UpdateBestGenome();
            UpdateStats(true, false);

            Debug.Assert(GenomeList.Count == PopulationSize);

            // If there is a logger defined, log the generation stats
            EvolutionLogger?.LogRow(GetLoggableElements(_logFieldEnabledMap),
                Statistics.GetLoggableElements(_logFieldEnabledMap),
                (CurrentChampGenome as NeatGenome)?.GetLoggableElements(_logFieldEnabledMap));

            // Dump the extant population to file
            foreach (TGenome genome in GenomeList)
            {
                PopulationLogger?.LogRow(new List<LoggableElement>
                {
                    _logFieldEnabledMap.ContainsKey(PopulationFieldElements.RunPhase) &&
                    _logFieldEnabledMap[PopulationFieldElements.RunPhase]
                        ? new LoggableElement(PopulationFieldElements.RunPhase, RunPhase)
                        : null,
                    _logFieldEnabledMap.ContainsKey(PopulationFieldElements.Generation) &&
                    _logFieldEnabledMap[PopulationFieldElements.Generation]
                        ? new LoggableElement(PopulationFieldElements.Generation, CurrentGeneration)
                        : null,
                    _logFieldEnabledMap.ContainsKey(PopulationFieldElements.GenomeId) &&
                    _logFieldEnabledMap[PopulationFieldElements.GenomeId]
                        ? new LoggableElement(PopulationFieldElements.GenomeId, genome.Id)
                        : null,
                    _logFieldEnabledMap[PopulationFieldElements.SpecieId]
                        ? new LoggableElement(PopulationFieldElements.SpecieId,
                            genome.SpecieIdx)
                        : null
                });
            }

            // Dump genome definitions for entire population if this is the first batch
            if (CurrentGeneration == 1)
            {
                foreach (TGenome genome in GenomeList)
                {
                    GenomeLogger?.LogRow(new List<LoggableElement>
                    {
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.RunPhase) &&
                        _logFieldEnabledMap[GenomeFieldElements.RunPhase]
                            ? new LoggableElement(GenomeFieldElements.RunPhase, RunPhase)
                            : null,
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.GenomeId) &&
                        _logFieldEnabledMap[GenomeFieldElements.GenomeId]
                            ? new LoggableElement(GenomeFieldElements.GenomeId, genome.Id)
                            : null,
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.GenomeXml) &&
                        _logFieldEnabledMap[GenomeFieldElements.GenomeXml]
                            ? new LoggableElement(GenomeFieldElements.GenomeXml, XmlIoUtils.GetGenomeXml(genome))
                            : null
                    });
                }
            }
            // Otherwise, just dump new child genome definitions to file
            else
            {
                foreach (TGenome childGenome in childGenomes)
                {
                    GenomeLogger?.LogRow(new List<LoggableElement>
                    {
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.RunPhase) &&
                        _logFieldEnabledMap[GenomeFieldElements.RunPhase]
                            ? new LoggableElement(GenomeFieldElements.RunPhase, RunPhase)
                            : null,
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.GenomeId) &&
                        _logFieldEnabledMap[GenomeFieldElements.GenomeId]
                            ? new LoggableElement(GenomeFieldElements.GenomeId, childGenome.Id)
                            : null,
                        _logFieldEnabledMap.ContainsKey(GenomeFieldElements.GenomeXml) &&
                        _logFieldEnabledMap[GenomeFieldElements.GenomeXml]
                            ? new LoggableElement(GenomeFieldElements.GenomeXml, XmlIoUtils.GetGenomeXml(childGenome))
                            : null
                    });
                }
            }
        }

        #endregion

        #region Instance Fields

        /// <summary>
        ///     The number of genomes to generate, evaluate, and remove in a single "generation".
        /// </summary>
        private readonly int _batchSize;

        /// <summary>
        ///     The number of generations after to which to re-evaluate the entire population.
        /// </summary>
        private readonly int _populationEvaluationFrequency;

        #endregion

        #region Constructors

        /// <summary>
        ///     Constructs steady state evolution algorithm with the default clustering strategy (k-means clustering) using
        ///     manhattan distance and null complexity regulation strategy.
        /// </summary>
        /// <param name="logger">The data logger (optional).</param>
        /// <param name="runPhase">
        ///     The experiment phase indicating whether this is an initialization process or the primary
        ///     algorithm.
        /// </param>
        public SteadyStateComplexifyingEvolutionAlgorithm(IDataLogger logger = null, RunPhase runPhase = RunPhase.Primary)
            : this(
                new KMeansClusteringStrategy<TGenome>(new ManhattanDistanceMetric()),
                new NullComplexityRegulationStrategy(), 10, 100, runPhase, logger)
        {
        }

        /// <summary>
        ///     Constructs steady state evolution algorithm with the given NEAT parameters, speciation strategy, and complexity
        ///     regulation strategy.
        /// </summary>
        /// <param name="speciationStrategy">The speciation strategy.</param>
        /// <param name="complexityRegulationStrategy">The complexity regulation strategy.</param>
        /// <param name="batchSize">The batch size of offspring to produce, evaluate, and remove.</param>
        /// <param name="populationEvaluationFrequency">The frequency at which to evaluate the fitness of the entire population.</param>
        /// <param name="runPhase">
        ///     The experiment phase indicating whether this is an initialization process or the primary
        ///     algorithm.
        /// </param>
        /// <param name="logger">The data logger (optional).</param>
        /// <param name="logFieldEnabledMap">Dictionary of logging fields that can be dynamically enabled or disabled.</param>
        /// <param name="populationLogger">The data logger for serializing the contents of the extant population (optional).</param>
        /// <param name="populationLoggingInterval">The interval at which the population should be logged.</param>
        public SteadyStateComplexifyingEvolutionAlgorithm(
            ISpeciationStrategy<TGenome> speciationStrategy,
            IComplexityRegulationStrategy complexityRegulationStrategy,
            int batchSize,
            int populationEvaluationFrequency,
            RunPhase runPhase = RunPhase.Primary,
            IDataLogger logger = null,
            IDictionary<FieldElement, bool> logFieldEnabledMap = null,
            IDataLogger populationLogger = null,
            int? populationLoggingInterval = null) : this(new EvolutionAlgorithmParameters(), speciationStrategy,
            complexityRegulationStrategy, batchSize, populationEvaluationFrequency, runPhase, logger,
            logFieldEnabledMap, populationLogger, populationLoggingInterval)
        {
        }

        /// <summary>
        ///     Constructs steady state evolution algorithm with the given NEAT parameters, speciation strategy, and complexity
        ///     regulation strategy.
        /// </summary>
        /// <param name="eaParams">The NEAT algorithm parameters.</param>
        /// <param name="speciationStrategy">The speciation strategy.</param>
        /// <param name="complexityRegulationStrategy">The complexity regulation strategy.</param>
        /// <param name="batchSize">The batch size of offspring to produce, evaluate, and remove.</param>
        /// <param name="populationEvaluationFrequency">The frequency at which to evaluate the fitness of the entire population.</param>
        /// <param name="runPhase">
        ///     The experiment phase indicating whether this is an initialization process or the primary
        ///     algorithm.
        /// </param>
        /// <param name="logger">The data logger (optional).</param>
        /// <param name="logFieldEnabledMap">Dictionary of logging fields that can be dynamically enabled or disabled.</param>
        /// <param name="populationLogger">The data logger for serializing the contents of the extant population (optional).</param>
        /// <param name="populationLoggingInterval">The interval at which the population should be logged.</param>
        public SteadyStateComplexifyingEvolutionAlgorithm(EvolutionAlgorithmParameters eaParams,
            ISpeciationStrategy<TGenome> speciationStrategy,
            IComplexityRegulationStrategy complexityRegulationStrategy,
            int batchSize,
            int populationEvaluationFrequency,
            RunPhase runPhase = RunPhase.Primary,
            IDataLogger logger = null, IDictionary<FieldElement, bool> logFieldEnabledMap = null,
            IDataLogger populationLogger = null,
            int? populationLoggingInterval = null) : base(eaParams, new Statistics.NeatAlgorithmStats(eaParams))
        {
            SpeciationStrategy = speciationStrategy;
            ComplexityRegulationStrategy = complexityRegulationStrategy;
            _batchSize = batchSize;
            _populationEvaluationFrequency = populationEvaluationFrequency;
            RunPhase = runPhase;
            EvolutionLogger = logger;
            _logFieldEnabledMap = logFieldEnabledMap;
            PopulationLogger = populationLogger;
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Updates the specie stats array, calculating and persisting the mean fitness of each species.
        /// </summary>
        /// <returns>The updated specie stats array.</returns>
        private SpecieStats[] CalcSpecieStats()
        {
            // Build stats array and get the mean fitness of each specie.
            int specieCount = SpecieList.Count;
            SpecieStats[] specieStatsArr = new SpecieStats[specieCount];
            for (int i = 0; i < specieCount; i++)
            {
                SpecieStats inst = new SpecieStats();
                specieStatsArr[i] = inst;
                inst.MeanFitness = SpecieList[i].CalcMeanFitness();
            }

            return specieStatsArr;
        }

        /// <summary>
        ///     Creates the specified number of offspring using discrete distribution species and genomes selection
        ///     (which is based on the fitness stats of the species in the given stats array).
        /// </summary>
        /// <param name="specieStatsArr">
        ///     The specie stats array, which is used to support specie reproduction selection based on
        ///     specie size and mean fitness.
        /// </param>
        /// <param name="offspringCount">The number of offspring to produce.</param>
        /// <returns>The list of offspring.</returns>
        private List<TGenome> CreateOffspring(SpecieStats[] specieStatsArr, int offspringCount)
        {
            List<TGenome> offspringList = new List<TGenome>(offspringCount);
            int specieCount = SpecieList.Count;

            // Probabilities for species discrete distribution sampler
            double[] specieProbabilities = new double[specieCount];

            // Discrete distribution array for genomes within species
            DiscreteDistribution[] genomeDistArr = new DiscreteDistribution[specieCount];

            // Build array of probabilities based on specie mean fitness
            for (int curSpecie = 0; curSpecie < specieCount; curSpecie++)
            {
                // Set probability for current species as specie mean fitness
                specieProbabilities[curSpecie] = specieStatsArr[curSpecie].MeanFitness;

                int genomeCount = SpecieList[curSpecie].GenomeList.Count;

                // Declare array for specie genome probabilities
                double[] genomeProbabilities = new double[genomeCount];

                // Build probability array for genome selection within species
                // based on genome fitness
                for (int curGenome = 0; curGenome < genomeCount; curGenome++)
                {
                    genomeProbabilities[curGenome] = SpecieList[curSpecie].GenomeList[curGenome].EvaluationInfo.Fitness;
                }

                // Create the genome discrete distribution for the current species
                genomeDistArr[curSpecie] = new DiscreteDistribution(genomeProbabilities);
            }

            // Complete construction of DiscreteDistribution for specie selection.
            DiscreteDistribution specieDist = new DiscreteDistribution(specieProbabilities);

            for (int curOffspring = 0; curOffspring < offspringCount; curOffspring++)
            {
                // Select specie from which to generate the next offspring
                int specieIdx = DiscreteDistribution.Sample(RandomNumGenerator, specieDist);

                // If random number is equal to or less than specified asexual offspring proportion or
                // if there is only one genome in the species, then use asexual reproduction
                if (RandomNumGenerator.NextDouble() <= EaParams.OffspringAsexualProportion ||
                    IsSpecieViableForSexualReproduction(SpecieList[specieIdx]) == false)
                {
                    // Throw ball to select genome from species (essentially intra-specie fitness proportionate selection)
                    int genomeIdx = DiscreteDistribution.Sample(RandomNumGenerator, genomeDistArr[specieIdx]);

                    // Create offspring asexually (from the above-selected parent)
                    TGenome offspring = SpecieList[specieIdx].GenomeList[genomeIdx].CreateOffspring(CurrentGeneration);

                    // Add that offspring to the genome list
                    offspringList.Add(offspring);
                }
                // Otherwise, mate two parents
                else
                {
                    TGenome parent1, parent2;

                    // If random number is equal to or less than specified interspecies mating proportion, then
                    // mate between two parent genomes from two different species
                    if (RandomNumGenerator.NextDouble() <= EaParams.InterspeciesMatingProportion)
                    {
                        // Throw ball again to get a second species
                        int specie2Idx = DiscreteDistribution.Sample(RandomNumGenerator, specieDist);

                        // Throw ball twice to select the two parent genomes (one from each species)
                        int parent1GenomeIdx =
                            DiscreteDistribution.Sample(RandomNumGenerator, genomeDistArr[specieIdx]);
                        int parent2GenomeIdx =
                            DiscreteDistribution.Sample(RandomNumGenerator, genomeDistArr[specie2Idx]);

                        // Get the two parents out of the two species genome list
                        parent1 = SpecieList[specieIdx].GenomeList[parent1GenomeIdx];
                        parent2 = SpecieList[specie2Idx].GenomeList[parent2GenomeIdx];
                    }
                    // Otherwise, mate two parents from within the currently selected species
                    else
                    {
                        // Throw ball twice to select the two parent genomes
                        int parent1GenomeIdx =
                            DiscreteDistribution.Sample(RandomNumGenerator, genomeDistArr[specieIdx]);
                        int parent2GenomeIdx = DiscreteDistribution.Sample(RandomNumGenerator,
                            genomeDistArr[specieIdx].RemoveOutcome(parent1GenomeIdx));

                        // Get the two parents out of the species genome list
                        parent1 = SpecieList[specieIdx].GenomeList[parent1GenomeIdx];
                        parent2 = SpecieList[specieIdx].GenomeList[parent2GenomeIdx];
                    }

                    // Perform recombination
                    TGenome offspring = parent1.CreateOffspring(parent2, CurrentGeneration);
                    offspringList.Add(offspring);
                }
            }

            // Update the total number of offspring (should be fixed)
            Statistics.TotalOffspringCount = (ulong) offspringList.Count;

            return offspringList;
        }

        /// <summary>
        ///     Determines whether or not a species is viable for sexual reproduction (i.e. crossover) based on the number of
        ///     genomes in the species as well as the fitness of its constituent genomes.
        /// </summary>
        /// <param name="candidateSpecie">The species being considered for sexual reproduction.</param>
        /// <returns>Whether or not the given species is viable for sexual reproduction (i.e. crossover).</returns>
        private bool IsSpecieViableForSexualReproduction(Specie<TGenome> candidateSpecie)
        {
            bool speciesReproductivelyViable = true;

            // If there is only one genome in the species, the species is automatically not viable
            // because there is no one with whom to mate
            if (candidateSpecie.GenomeList.Count <= 1)
            {
                speciesReproductivelyViable = false;
            }
            else
            {
                int nonZeroFitnessCnt = 0;

                // Iterate through the genomes in the species, making sure that 2 or more have non-zero fitness (this 
                // is because the roullette will selection will cause an endless loop if not)
                foreach (TGenome genome in GenomeList.Where(genome => genome.EvaluationInfo.Fitness > 0))
                {
                    nonZeroFitnessCnt++;

                    // If we've already found more than one genome that has non-zero fitness, we're good
                    if (nonZeroFitnessCnt > 1)
                    {
                        break;
                    }
                }

                // If there was one or fewer genomes found with non-zero fitness, this species is not viable for crossover
                if (nonZeroFitnessCnt <= 1)
                {
                    speciesReproductivelyViable = false;
                }
            }

            return speciesReproductivelyViable;
        }

        /// <summary>
        ///     Selects genomes to remove based on their adjusted fitness.
        /// </summary>
        /// <param name="numGenomesToRemove">The number of genomes to remove from the existing population.</param>
        /// <returns>The list of genomes selected for removal.</returns>
        private List<TGenome> SelectGenomesForRemoval(int numGenomesToRemove)
        {
            List<TGenome> genomesToRemove = new List<TGenome>(numGenomesToRemove);
            Dictionary<TGenome, double> removalCandidatesMap = new Dictionary<TGenome, double>();

            // Iterate through each genome from each species and calculate its adjusted fitness relative to others in that species
            foreach (var specie in SpecieList)
            {
                for (int genomeIdx = 0; genomeIdx < specie.GenomeList.Count; genomeIdx++)
                {
                    // Add adjusted fitness and the genome reference to the map (dictionary)
                    removalCandidatesMap.Add(specie.GenomeList[genomeIdx],
                        specie.CalcGenomeAdjustedFitness(genomeIdx));
                }
            }

            // Build a stack in ascending order of fitness (that is, lower fitness genomes will be popped first)
            var removalCandidatesStack =
                new Stack<KeyValuePair<TGenome, double>>(removalCandidatesMap.OrderByDescending(i => i.Value));

            // Iterate up to the number to remove, incrementally building the genomes to remove list
            for (int curRemoveIdx = 0; curRemoveIdx < numGenomesToRemove; curRemoveIdx++)
            {
                // Add genome to remove                
                genomesToRemove.Add(removalCandidatesStack.Pop().Key);
            }

            return genomesToRemove;
        }

        #endregion
    }
}