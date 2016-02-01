﻿#region

using System.Collections.Generic;
using System.Xml;
using ExperimentEntities;
using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpNeat.Loggers;
using SharpNeat.Phenomes;
using RunPhase = SharpNeat.Core.RunPhase;

#endregion

namespace SharpNeat.Domains.MazeNavigation.MCSExperiment
{
    public class QueueingMazeNavigationMCSExperiment : BaseMazeNavigationExperiment
    {
        private int _batchSize;
        private IBehaviorCharacterizationFactory _behaviorCharacterizationFactory;
        private int _bridgingApplications;
        private int _bridgingMagnitude;
        private IDataLogger _evaluationDataLogger;
        private IDataLogger _evolutionDataLogger;
        private IDictionary<FieldElement, bool> _experimentLogFieldEnableMap;
        private NoveltySearchMazeNavigationInitializer _mazeNavigationInitializer;

        public override void Initialize(string name, XmlElement xmlConfig, IDataLogger evolutionDataLogger,
            IDataLogger evaluationDataLogger)
        {
            base.Initialize(name, xmlConfig, evolutionDataLogger, evaluationDataLogger);

            // Read in the behavior characterization
            _behaviorCharacterizationFactory = ExperimentUtils.ReadBehaviorCharacterizationFactory(xmlConfig,
                "BehaviorConfig");

            // Read in number of offspring to produce in a single batch
            _batchSize = XmlUtils.GetValueAsInt(xmlConfig, "OffspringBatchSize");

            // Read in the bridging magnitude and number of applications
            _bridgingMagnitude = XmlUtils.TryGetValueAsInt(xmlConfig, "BridgingMagnitude") ?? default(int);
            _bridgingApplications = XmlUtils.TryGetValueAsInt(xmlConfig, "BridgingApplications") ?? default(int);

            // Read in the number of seed genomes to generate to bootstrap the primary algorithm
            SeedGenomeCount = XmlUtils.GetValueAsInt(xmlConfig, "SeedGenomeCount");

            // Read in log file path/name
            _evolutionDataLogger = evolutionDataLogger ??
                                   ExperimentUtils.ReadDataLogger(xmlConfig, LoggingType.Evolution);
            _evaluationDataLogger = evaluationDataLogger ??
                                    ExperimentUtils.ReadDataLogger(xmlConfig, LoggingType.Evaluation);

            // Setup the specific logging options based on parameters that are enabled/disabled
            _experimentLogFieldEnableMap = new Dictionary<FieldElement, bool>();

            // Enable or disable genome XML logging
            if (SerializeGenomeToXml)
            {
                _experimentLogFieldEnableMap.Add(EvolutionFieldElements.ChampGenomeXml, true);
            }

            // Enable or disable primary fitness logging (causing utilization of auxiliary fitness)
            if (_bridgingMagnitude > 0)
            {
                _experimentLogFieldEnableMap.Add(EvolutionFieldElements.ChampGenomeFitness, false);
            }

            // Initialize the initialization algorithm
            _mazeNavigationInitializer = new NoveltySearchMazeNavigationInitializer(MaxEvaluations, _evolutionDataLogger,
                _evaluationDataLogger,
                SerializeGenomeToXml);

            // Setup initialization algorithm
            _mazeNavigationInitializer.SetAlgorithmParameters(
                xmlConfig.GetElementsByTagName("InitializationAlgorithmConfig", "")[0] as XmlElement, InputCount,
                OutputCount);

            // Pass in maze experiment specific parameters
            _mazeNavigationInitializer.SetEnvironmentParameters(MaxDistanceToTarget, MaxTimesteps, MazeVariant,
                MinSuccessDistance);
        }

        public override void Initialize(ExperimentDictionary experimentDictionary)
        {
            base.Initialize(experimentDictionary);

            // Read in the behavior characterization
            _behaviorCharacterizationFactory = ExperimentUtils.ReadBehaviorCharacterizationFactory(
                experimentDictionary, true);

            // Read in number of offspring to produce in a single batch
            _batchSize = experimentDictionary.Primary_OffspringBatchSize ?? default(int);

            // Read in the bridging magnitude
            _bridgingMagnitude = experimentDictionary.Primary_MCS_BridgingMagnitude ?? default(int);
            _bridgingApplications = experimentDictionary.Primary_MCS_BridgingApplications ?? default(int);

            // TODO: Read seed genome count from the database

            // Read in log file path/name
            _evolutionDataLogger = new McsExperimentEvaluationEntityDataLogger(experimentDictionary.ExperimentName);
            _evaluationDataLogger =
                new McsExperimentOrganismStateEntityDataLogger(experimentDictionary.ExperimentName);

            // Initialize the initialization algorithm
            _mazeNavigationInitializer = new NoveltySearchMazeNavigationInitializer(MaxEvaluations, _evolutionDataLogger,
                _evaluationDataLogger,
                SerializeGenomeToXml);

            // Setup initialization algorithm
            _mazeNavigationInitializer.SetAlgorithmParameters(experimentDictionary, InputCount, OutputCount);

            // Pass in maze experiment specific parameters
            _mazeNavigationInitializer.SetEnvironmentParameters(MaxDistanceToTarget, MaxTimesteps, MazeVariant,
                MinSuccessDistance);
        }

        /// <summary>
        ///     Create and return a SteadyStateNeatEvolutionAlgorithm object (specific to fitness-based evaluations) ready for
        ///     running the
        ///     NEAT algorithm/search based on the given genome factory and genome list.  Various sub-parts of the algorithm are
        ///     also constructed and connected up.
        /// </summary>
        /// <param name="genomeFactory">The genome factory from which to generate new genomes</param>
        /// <param name="genomeList">The current genome population</param>
        /// <param name="startingEvaluations">The number of evaluations that have been executed prior to the current run.</param>
        /// <returns>Constructed evolutionary algorithm</returns>
        public override INeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(
            IGenomeFactory<NeatGenome> genomeFactory,
            List<NeatGenome> genomeList, ulong startingEvaluations)
        {
            ulong initializationEvaluations;

            // Instantiate the internal initialization algorithm
            _mazeNavigationInitializer.InitializeAlgorithm(ParallelOptions, genomeList,
                CreateGenomeDecoder(), startingEvaluations);

            // Run the initialization algorithm until the requested number of viable seed genomes are found
            List<NeatGenome> seedPopulation = _mazeNavigationInitializer.EvolveViableGenomes(SeedGenomeCount, true,
                out initializationEvaluations);

            // Create complexity regulation strategy.
            var complexityRegulationStrategy =
                ExperimentUtils.CreateComplexityRegulationStrategy(ComplexityRegulationStrategy, Complexitythreshold);

            // Create the evolution algorithm.
            AbstractNeatEvolutionAlgorithm<NeatGenome> ea =
                new QueueingNeatEvolutionAlgorithm<NeatGenome>(NeatEvolutionAlgorithmParameters,
                    complexityRegulationStrategy, _batchSize, RunPhase.Primary, (_bridgingMagnitude > 0),
                    false, _evolutionDataLogger, _experimentLogFieldEnableMap);

            // Create IBlackBox evaluator.
            IPhenomeEvaluator<IBlackBox, BehaviorInfo> mazeNavigationEvaluator =
                new MazeNavigationMCSEvaluator(MaxDistanceToTarget, MaxTimesteps,
                    MazeVariant, MinSuccessDistance, _behaviorCharacterizationFactory,
                    _bridgingMagnitude, _bridgingApplications, initializationEvaluations);

            // Create genome decoder.
            IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder = CreateGenomeDecoder();

//            IGenomeEvaluator<NeatGenome> fitnessEvaluator =
//                new SerialGenomeBehaviorEvaluator<NeatGenome, IBlackBox>(genomeDecoder, mazeNavigationEvaluator,
//                    SelectionType.Queueing, SearchType.MinimalCriteriaSearch, _evaluationDataLogger,
//                    SerializeGenomeToXml);

            IGenomeEvaluator<NeatGenome> fitnessEvaluator =
                new ParallelGenomeBehaviorEvaluator<NeatGenome, IBlackBox>(genomeDecoder, mazeNavigationEvaluator,
                    SelectionType.Queueing, SearchType.MinimalCriteriaSearch, _evaluationDataLogger,
                    SerializeGenomeToXml);

            // Initialize the evolution algorithm.
            ea.Initialize(fitnessEvaluator, genomeFactory, seedPopulation, DefaultPopulationSize,
                null, MaxEvaluations + startingEvaluations);

            // Finished. Return the evolution algorithm
            return ea;
        }
    }
}