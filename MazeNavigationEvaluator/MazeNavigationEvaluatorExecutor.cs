﻿#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ExperimentEntities.entities;
using log4net;
using log4net.Config;
using MazeExperimentSupportLib;
using SharpNeat.Decoders.Maze;
using SharpNeat.Genomes.Maze;
using SharpNeat.Phenomes.Mazes;
using RunPhase = SharpNeat.Core.RunPhase;

#endregion

namespace MazeNavigationEvaluator
{
    /// <summary>
    ///     Handles post-hoc evaluation of maze navigation results and compute key metrics therefrom.
    /// </summary>
    internal static class NavigatorMazeMapEvaluatorExecutor
    {
        /// <summary>
        ///     This is the number of records that are written to the database in one pass.
        /// </summary>
        private const int CommitPageSize = 1000;

        /// <summary>
        ///     Encapsulates configuration parameters specified at runtime.
        /// </summary>
        private static readonly Dictionary<ExecutionParameter, string> _executionConfiguration =
            new Dictionary<ExecutionParameter, string>();

        /// <summary>
        ///     Console logger for reporting execution status.
        /// </summary>
        private static ILog _executionLogger;

        private static void Main(string[] args)
        {
            string baseImageOutputDirectory = null;

            // Initialise log4net (log to console and file).
            XmlConfigurator.Configure(LogManager.GetRepository(Assembly.GetEntryAssembly()),
                new FileInfo("log4net.config"));

            // Instantiate the execution logger
            _executionLogger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            // Extract the execution parameters and check for any errors (exit application if any found)
            if (ParseAndValidateConfiguration(args) == false)
                Environment.Exit(0);

            _executionLogger.Info("Invocation parameters validated - continuing with experiment execution.");

            // Get boolean indicator dictating whether to analyze the whole run or just the last batch (default is true - full run)
            var analysisScope =
                AnalysisScopeUtil.ConvertStringToAnalysisScope(
                    _executionConfiguration[ExecutionParameter.AnalysisScope]);

            // Get input and output neurons counts for navigator agent
            var inputNeuronCount = int.Parse(_executionConfiguration[ExecutionParameter.AgentNeuronInputCount]);
            var outputNeuronCount = int.Parse(_executionConfiguration[ExecutionParameter.AgentNeuronOutputCount]);

            // Get boolean indicator dictating whether to generate trajectory data (default is false)
            var generateTrajectoryData =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateTrajectoryData) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateTrajectoryData]);

            // Get boolean indicator dictating whether to write out numeric results of the batch simulations (default is true)
            var generateSimulationResults =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateSimulationResults) == false ||
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateSimulationResults]);

            // Get boolean indicator dictating whether to generate bitmaps of mazes (default is true)
            var generateMazeBitmaps = _executionConfiguration.ContainsKey(ExecutionParameter.GenerateMazeBitmaps) ==
                                      false ||
                                      bool.Parse(_executionConfiguration[ExecutionParameter.GenerateMazeBitmaps]);

            // Get boolean indicator dictating whether to generate bitmaps of agent trajectories (default is true)
            var generateTrajectoryBitmaps =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateAgentTrajectoryBitmaps) == false ||
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateAgentTrajectoryBitmaps]);

            // Get boolean indicator dictating whether to write simulation results to database (default is false)
            var writeResultsToDatabase =
                _executionConfiguration.ContainsKey(ExecutionParameter.WriteResultsToDatabase) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.WriteResultsToDatabase]);

            // Determine whether this is a distributed execution
            var isDistributedExecution =
                _executionConfiguration.ContainsKey(ExecutionParameter.IsDistributedExecution) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.IsDistributedExecution]);

            // Get boolean indicator dictating whether to write out agent trajectory diversity scores (default is false)
            var generateTrajectoryDiversityScores =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateAgentDiversityScores) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateAgentDiversityScores]);

            // Get boolean indicator dictating whether to write out maze diversity scores (default is false)
            var generateMazeDiversityScores =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateMazeDiversityScores) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateMazeDiversityScores]);

            // Get boolean indicator dictating whether to write out deceptive turn counts (default is false)
            var generateDeceptiveTurnCounts =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateDeceptiveTurnCounts) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateDeceptiveTurnCounts]);

            // Get boolean indicator dictating whether to write out natural agent trajectory clusters (default is false)
            var generateAgentTrajectoryClusters =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateAgentTrajectoryClusters) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateAgentTrajectoryClusters]);

            // Get boolean indicator dictating whether to write out natural maze clusters (default is false)
            var generateMazeClusters =
                _executionConfiguration.ContainsKey(ExecutionParameter.GenerateMazeClusters) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GenerateMazeClusters]);

            // If the generate natural clusters or trajectory diversity flag is set, get the sample size
            var sampleSize = generateAgentTrajectoryClusters || generateTrajectoryDiversityScores
                ? int.Parse(_executionConfiguration[ExecutionParameter.SampleSize])
                : 0;

            // Get boolean indicator dictating whether to sample the clustering data points from 
            // species or from the population (default is false, meaning sample from population)
            var sampleClusterObservationsFromSpecies =
                _executionConfiguration.ContainsKey(ExecutionParameter.SampleFromSpecies) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.SampleFromSpecies]);

            // Get boolean indicator dictating whether silhouette calculation should be performed greedily
            var useGreedySilhouetteEvaluation =
                _executionConfiguration.ContainsKey(ExecutionParameter.UseGreedySilhouetteCalculation) == false ||
                bool.Parse(_executionConfiguration[ExecutionParameter.UseGreedySilhouetteCalculation]);

            // Get boolean indicator dictating whether trajectory clustering samples should be applied evenly 
            // to all extant, and successfully navigable mazes
            var useEvenMazeTrajectoryDistribution =
                _executionConfiguration.ContainsKey(ExecutionParameter.UseEvenMazeTrajectoryDistribution) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.UseEvenMazeTrajectoryDistribution]);

            // If greedy silhouette evaluation is NOT being used, then get the range of clusters for which to compute silhouette width
            var clusterRange = useGreedySilhouetteEvaluation == false
                ? int.Parse(_executionConfiguration[ExecutionParameter.ClusterRange])
                : 0;

            // Get boolean indicator dictating whether to write out population entropy scores (default is false)
            var generatePopulationEntropy =
                _executionConfiguration.ContainsKey(ExecutionParameter.GeneratePopulationEntropy) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.GeneratePopulationEntropy]);

            // Get boolean indicator dictating whether to execute initialization trial analysis (default is false)
            var runInitializationAnalysis =
                _executionConfiguration.ContainsKey(ExecutionParameter.ExecuteInitializationTrials) &&
                bool.Parse(_executionConfiguration[ExecutionParameter.ExecuteInitializationTrials]);

            // Get the number of batches to skip in each iteration
            var batchInterval = _executionConfiguration.ContainsKey(ExecutionParameter.BatchInterval)
                ? int.Parse(_executionConfiguration[ExecutionParameter.BatchInterval])
                : 1;

            // Get the image chunk size
            var chunkSize = _executionConfiguration.ContainsKey(ExecutionParameter.ImageChunkSize)
                ? int.Parse(_executionConfiguration[ExecutionParameter.ImageChunkSize])
                : 10;

            // If bitmap generation was enabled, grab the base output directory
            if (generateTrajectoryBitmaps || generateMazeBitmaps)
            {
                baseImageOutputDirectory = _executionConfiguration[ExecutionParameter.BitmapOutputBaseDirectory];
            }

            // Extract the experiment names
            var experimentNames = _executionConfiguration[ExecutionParameter.ExperimentNames].Split(',');

            _executionLogger.Info($"[{experimentNames.Count()}] experiments specified for analysis.");

            // Process each experiment
            foreach (var experimentName in experimentNames)
            {
                // Get the run from which to start execution (if specified)
                var startingRun = _executionConfiguration.ContainsKey(ExecutionParameter.StartFromRun)
                    ? int.Parse(_executionConfiguration[ExecutionParameter.StartFromRun])
                    : 1;

                // Lookup the current experiment configuration
                var curExperimentConfiguration = ExperimentDataHandler.LookupExperimentConfiguration(experimentName);

                // Ensure that experiment configuration was found
                if (curExperimentConfiguration == null)
                {
                    _executionLogger.Error(
                        $"Unable to lookup experiment configuration for experiment with name [{experimentName}]");
                    Environment.Exit(0);
                }

                // Construct the experiment parameters
                var experimentParameters =
                    new ExperimentParameters(curExperimentConfiguration.MaxTimesteps,
                        curExperimentConfiguration.MinSuccessDistance,
                        curExperimentConfiguration.Primary_Maze_MazeHeight,
                        curExperimentConfiguration.Primary_Maze_MazeWidth,
                        curExperimentConfiguration.Primary_Maze_QuadrantHeight,
                        curExperimentConfiguration.Primary_Maze_QuadrantWidth,
                        curExperimentConfiguration.Primary_Maze_MazeScaleMultiplier,
                        curExperimentConfiguration.Primary_ActivationScheme,
                        curExperimentConfiguration.Primary_ActivationIters,
                        curExperimentConfiguration.Primary_ActivationDeltaThreshold);

                // Get the number of runs in the experiment. Note that if this is a distributed execution, each node
                // will only execute a single run analysis, so the number of runs will be equivalent to the run 
                // to start from (this ensures that the ensuing loop that executes all of the runs executes exactly once)
                var numRuns = isDistributedExecution
                    ? startingRun
                    : ExperimentDataHandler.GetNumRuns(curExperimentConfiguration.ExperimentDictionaryId);

                _executionLogger.Info(
                    $"Preparing to execute analysis for [{numRuns}] runs of experiment [{curExperimentConfiguration.ExperimentName}]");

                // Process each experiment run
                for (var curRun = startingRun; curRun <= numRuns; curRun++)
                {
                    // If simulation result generation is enabled and we're not writing to 
                    // the database, open the simulation result file writer
                    if (generateSimulationResults && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - Run{curRun}.csv"), OutputFileType.NavigatorMazeEvaluationData);
                    }

                    // If trajectory data generation is enabled and we're not writing
                    // to the database, open the trajectory data file writer
                    if (generateTrajectoryData && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - TrajectoryData - Run{curRun}.csv"), OutputFileType.TrajectoryData);
                    }

                    // If trajectory diversity score generation is enabled and we're not writing to 
                    // the database, open the trajectory diversity score file writer
                    if (generateTrajectoryDiversityScores && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - TrajectoryDiversity - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.TrajectoryDiversityData);
                    }

                    // If maze diversity score generation is enabled and we're not writing to 
                    // the database, open the maze diversity score file writer
                    if (generateMazeDiversityScores && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - MazeDiversity - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.MazeDiversityData);
                    }

                    // If maze deceptive turn count generation is enabled and we're not writing to
                    // the database, open the deceptive turn file writer
                    if (generateDeceptiveTurnCounts && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - DeceptiveTurns - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.DeceptiveTurnData);
                    }

                    if (generateAgentTrajectoryClusters && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - NaturalClusters - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.NaturalClusterData);
                    }

                    if (generateMazeClusters && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - MazeClusters - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.MazeClusterData);
                    }

                    if (generatePopulationEntropy && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.OpenFileWriter(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - PopulationEntropy - {analysisScope} - Run{curRun}.csv"),
                            OutputFileType.PopulationEntropyData);
                    }

                    // If we're analyzing the entire run, go ahead and process through the initialization phase
                    // and all primary phase batch results
                    if (AnalysisScope.Full == analysisScope)
                    {
                        // Get the number of initialization batches in the current run
                        var numInitializationBatches =
                            ExperimentDataHandler.GetNumBatchesForRun(
                                curExperimentConfiguration.ExperimentDictionaryId, curRun, RunPhase.Initialization);

                        // If we're running initialization analysis and there was an initialization phase, analyze those results
                        if (runInitializationAnalysis && numInitializationBatches > 0)
                        {
                            _executionLogger.Info(
                                $"Executing initialization phase analysis for run [{curRun}/{numRuns}] with [{numInitializationBatches}] batches");

                            // Begin initialization phase results processing
                            ProcessAndLogPerBatchResults(numInitializationBatches, batchInterval,
                                RunPhase.Initialization,
                                experimentParameters, inputNeuronCount, outputNeuronCount, curRun, numRuns,
                                curExperimentConfiguration, generateSimulationResults, generateTrajectoryData,
                                generateTrajectoryDiversityScores, generateAgentTrajectoryClusters,
                                generateMazeClusters, generatePopulationEntropy, useGreedySilhouetteEvaluation,
                                useEvenMazeTrajectoryDistribution, clusterRange, writeResultsToDatabase, sampleSize,
                                sampleClusterObservationsFromSpecies);
                        }

                        // Get the number of primary batches in the current run
                        var numBatches = ExperimentDataHandler.GetNumBatchesForRun(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, RunPhase.Primary);

                        _executionLogger.Info(
                            $"Executing primary phase analysis for run [{curRun}/{numRuns}] with [{numBatches}] batches");

                        // Image generation is handled more holistically (rather than batch-by-batch) and therefore doesn't align 
                        // with the manner in which non-image, quantitative experiment data is processed
                        if (generateMazeBitmaps || generateTrajectoryBitmaps)
                        {
                            WriteImageResults(experimentParameters, inputNeuronCount, outputNeuronCount, curRun,
                                numRuns,
                                curExperimentConfiguration, baseImageOutputDirectory, generateMazeBitmaps,
                                generateTrajectoryBitmaps, chunkSize);
                        }
                        // Maze diversity scores are computed over the entire run 
                        else if (generateMazeDiversityScores)
                        {
                            WriteMazeDiversityScores(curExperimentConfiguration, curRun);
                        }
                        // Deceptive turn counts are computed over the entire run
                        else if (generateDeceptiveTurnCounts)
                        {
                            WriteDeceptiveTurnCounts(curExperimentConfiguration, curRun);
                        }
                        // Begin primary phase results processing
                        else
                        {
                            ProcessAndLogPerBatchResults(numBatches, batchInterval, RunPhase.Primary,
                                experimentParameters,
                                inputNeuronCount, outputNeuronCount, curRun, numRuns, curExperimentConfiguration,
                                generateSimulationResults, generateTrajectoryData, generateTrajectoryDiversityScores,
                                generateAgentTrajectoryClusters, generateMazeClusters, generatePopulationEntropy,
                                useGreedySilhouetteEvaluation, useEvenMazeTrajectoryDistribution, clusterRange,
                                writeResultsToDatabase, sampleSize, sampleClusterObservationsFromSpecies);
                        }
                    }
                    // Otherwise, we're just analyzing the ending population
                    else
                    {
                        // Get the last batch in the current run
                        var finalBatch =
                            ExperimentDataHandler.GetNumBatchesForRun(
                                curExperimentConfiguration.ExperimentDictionaryId, curRun, RunPhase.Primary);

                        _executionLogger.Info(
                            $"Executing analysis of end-stage mazes and navigator trajectories for run [{curRun}/{numRuns}] batch [{finalBatch}]");

                        // Begin maze/navigator trajectory image generation
                        ProcessAndLogPerBatchResults(finalBatch, batchInterval, RunPhase.Primary,
                            experimentParameters, inputNeuronCount, outputNeuronCount, curRun, numRuns,
                            curExperimentConfiguration, generateSimulationResults, generateTrajectoryData,
                            generateTrajectoryDiversityScores, generateAgentTrajectoryClusters,
                            generateMazeClusters, generatePopulationEntropy, useGreedySilhouetteEvaluation,
                            useEvenMazeTrajectoryDistribution, clusterRange, writeResultsToDatabase, sampleSize,
                            sampleClusterObservationsFromSpecies);
                    }

                    // If we're not writing to the database, close the simulation result file writer
                    // and write the sentinel file for the run
                    if (generateSimulationResults && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.NavigatorMazeEvaluationData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                experimentName), curRun);
                    }

                    // If we're not writing to the database, close the trajectory data file writer
                    // and write the sentinel file for the run
                    if (generateTrajectoryData && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.TrajectoryData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - TrajectoryData"), curRun);
                    }

                    // If we're not writing to the database, close the trajectory diversity 
                    // score file writer and write the sentinel file for the run
                    if (generateTrajectoryDiversityScores && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.TrajectoryDiversityData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - TrajectoryDiversity - {analysisScope}"), curRun);
                    }

                    // If we're not writing to the database, close the maze diversity 
                    // score file writer and write the sentinel file for the run
                    if (generateMazeDiversityScores && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.MazeDiversityData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - MazeDiversity - {analysisScope}"), curRun);
                    }

                    if (generateDeceptiveTurnCounts && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.DeceptiveTurnData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - DeceptiveTurns - {analysisScope}"), curRun);
                    }

                    // If we're not writing to the database, close the natural clustering file writer
                    // and write the sentinel file for the run
                    if (generateAgentTrajectoryClusters && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.NaturalClusterData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - NaturalClusters - {analysisScope}"), curRun);
                    }

                    // If we're not writing to the database, close the maze clustering file writer
                    // and write the sentinel file for the run
                    if (generateMazeClusters && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.MazeClusterData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - MazeClusters - {analysisScope}"), curRun);
                    }

                    // If we're not writing to the database, close the population entropy file writer
                    // and write the sentinel file for the run
                    if (generatePopulationEntropy && writeResultsToDatabase == false)
                    {
                        ExperimentDataHandler.CloseFileWriter(OutputFileType.PopulationEntropyData);
                        ExperimentDataHandler.WriteSentinelFile(
                            Path.Combine(_executionConfiguration[ExecutionParameter.DataFileOutputDirectory],
                                $"{experimentName} - PopulationEntropy - {analysisScope}"), curRun);
                    }
                }
            }
        }

        /// <summary>
        ///     Runs post-hoc image generation, depicting either the maze or the agent's trajectory through said maze.
        /// </summary>
        /// <param name="experimentParameters">Experiment configuration parameters.</param>
        /// <param name="inputNeuronCount">Count of neurons in controller input layer.</param>
        /// <param name="outputNeuronCount">Count of neurons in controller output layer.</param>
        /// <param name="curRun">The run number.</param>
        /// <param name="numRuns">The total number of runs.</param>
        /// <param name="curExperimentConfiguration">The experiment configuration parameters.</param>
        /// <param name="baseImageOutputDirectory">The path to the output directory for the trajectory images.</param>
        /// <param name="generateMazeBitmaps">Indicates whether to generate maze bitmap images.</param>
        /// <param name="generateTrajectoryBitmaps">Indicates whether to generate agent trajectory images.</param>
        /// <param name="chunkSize">The number of maze genomes to process at one time (optional).</param>
        private static void WriteImageResults(ExperimentParameters experimentParameters, int inputNeuronCount,
            int outputNeuronCount, int curRun, int numRuns, ExperimentDictionary curExperimentConfiguration,
            string baseImageOutputDirectory, bool generateMazeBitmaps, bool generateTrajectoryBitmaps,
            int chunkSize = 10)
        {
            _executionLogger.Info($"Executing image generation for run [{curRun}/{numRuns}]");

            // Determine whether experiments were executed with trial data generation enabled
            var isMazeTrialDataAvailable =
                ExperimentDataHandler.IsMazeTrialDataAvailable(curExperimentConfiguration.ExperimentDictionaryId,
                    curRun);

            // Get the distinct maze genome IDs for which to produce trajectory images
            var mazeGenomeIds = ExperimentDataHandler.GetMazeGenomeIds(
                curExperimentConfiguration.ExperimentDictionaryId, curRun);

            for (var curChunk = 0; curChunk < mazeGenomeIds.Count; curChunk += chunkSize)
            {
                List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>> successfulGenomeCombos = null;

                // Create the maze/navigator map
                var mapEvaluator = new MapEvaluator(experimentParameters, inputNeuronCount, outputNeuronCount);

                // Get maze genome IDs for the current chunk
                var curMazeGenomeIds = mazeGenomeIds.Skip(curChunk).Take(chunkSize).ToList();

                _executionLogger.Info(
                    $"Evaluating maze genomes with IDs [{curMazeGenomeIds.Min()}] through [{curMazeGenomeIds.Max()}]");

                if (isMazeTrialDataAvailable)
                {
                    successfulGenomeCombos =
                        GetSuccessfulGenomeCombosFromMazeTrials(curExperimentConfiguration.ExperimentDictionaryId,
                            curRun, curMazeGenomeIds);
                }
                else
                {
                    successfulGenomeCombos =
                        GetSuccessfulGenomeCombosFromNavigationResults(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curMazeGenomeIds);
                }

                // Initialize the maze/navigator map with combinations that are known to be successful
                mapEvaluator.Initialize(successfulGenomeCombos);

                // Generate navigator trajectories
                mapEvaluator.RunTrajectoryEvaluations();

                if (generateMazeBitmaps)
                {
                    // Generate bitmaps of distinct mazes extant at the current point in time
                    ImageGenerationHandler.GenerateMazeBitmaps(baseImageOutputDirectory,
                        curExperimentConfiguration.ExperimentName, curExperimentConfiguration.ExperimentDictionaryId,
                        curRun, mapEvaluator.EvaluationUnits);
                }

                if (generateTrajectoryBitmaps)
                {
                    // Generate bitmaps of trajectory for all successful trials
                    ImageGenerationHandler.GenerateBitmapsForSuccessfulTrials(
                        baseImageOutputDirectory, curExperimentConfiguration.ExperimentName,
                        curExperimentConfiguration.ExperimentDictionaryId,
                        curRun, mapEvaluator.EvaluationUnits, RunPhase.Primary);
                }
            }
        }

        /// <summary>
        ///     Determines the distance between mazes based on computing manhattan distance between their solution paths, and
        ///     writes out the maze distances.
        /// </summary>
        /// <param name="experimentConfiguration">The experiment configuration parameters</param>
        /// <param name="run">The run number.</param>
        /// <param name="chunkSize">The number of maze genomes to process at one time (optional).</param>
        private static void WriteMazeDiversityScores(ExperimentDictionary experimentConfiguration, int run,
            int chunkSize = 100)
        {
            // Extract maze genomes and decode to maze structure phenotype
            var allMazes = GetAndDecodeMazes(experimentConfiguration.ExperimentDictionaryId, run,
                experimentConfiguration.Primary_Maze_MazeScaleMultiplier);

            // Get the total number of mazes
            var numMazes = allMazes.Count;

            for (var curChunk = 0; curChunk < numMazes; curChunk += chunkSize)
            {
                _executionLogger.Info(
                    $"Evaluating mazes [{curChunk}] through [{chunkSize + curChunk}] of [{numMazes}]");

                // Get mazes for the current chunk
                var curMazes = allMazes.Skip(curChunk).Take(chunkSize).ToList();

                // Calculate maze path diversity and persist
                ExperimentDataHandler.WriteMazeDiversityData(experimentConfiguration.ExperimentDictionaryId, run,
                    EvaluationHandler.CalculateMazeDiversity(curMazes, allMazes), false);
            }
        }

        /// <summary>
        ///     Computes the number of deceptive turns for each maze in the population and writes out the deceptive turn tallies.
        /// </summary>
        /// <param name="experimentConfiguration">The experiment configuration parameters</param>
        /// <param name="run">The run number.</param>
        /// <param name="chunkSize">The number of maze genomes to process at one time (optional).</param>
        private static void WriteDeceptiveTurnCounts(ExperimentDictionary experimentConfiguration, int run,
            int chunkSize = 100)
        {
            // Extract maze genomes and decode to maze structure phenotype
            var mazes = GetAndDecodeMazes(experimentConfiguration.ExperimentDictionaryId, run,
                experimentConfiguration.Primary_Maze_MazeScaleMultiplier);

            // Get the total number of mazes
            var numMazes = mazes.Count;

            for (var curChunk = 0; curChunk < numMazes; curChunk += chunkSize)
            {
                _executionLogger.Info(
                    $"Evaluating mazes [{curChunk}] through [{chunkSize + curChunk}] of [{numMazes}]");

                // Get mazes for the current chunk
                var curMazes = mazes.Skip(curChunk).Take(chunkSize).ToList();

                // Tally the number of deceptive turns and persist
                ExperimentDataHandler.WriteMazeDeceptiveTurnCount(experimentConfiguration.ExperimentDictionaryId, run,
                    EvaluationHandler.CalculateDeceptiveTurnCount(curMazes), false);
            }
        }

        /// <summary>
        ///     Queries database to get maze genomes corresponding to the given experiment and run, and decodes them to maze
        ///     structure phenotypes.
        /// </summary>
        /// <param name="experimentId">The unique ID of the current experiment.</param>
        /// <param name="run">The current run number.</param>
        /// <param name="mazeScaleMultiplier">
        ///     The amount by which to scale the maze encoding to produce the desired phenotype
        ///     dimensions.
        /// </param>
        /// <returns>List of decoded maze structure phenomes.</returns>
        private static IList<MazeStructure> GetAndDecodeMazes(int experimentId, int run, int? mazeScaleMultiplier)
        {
            var mazeStructuresBag = new ConcurrentBag<MazeStructure>();

            // Get XML-serialized genomes for the current experiment/run
            var serializedMazeGenomes =
                ExperimentDataHandler.GetMazeGenomeXml(experimentId, run);

            // Initialize maze decoder for converting maze genomes to phenomes (structures)
            var mazeDecoder = new MazeDecoder(mazeScaleMultiplier ?? 32);

            // Convert serialized maze XML strings to maze genomes
            var mazeGenomes = MazeGenomeXmlIO.ReadMazeGenomesFromXml(serializedMazeGenomes, new MazeGenomeFactory());

            // Decode each maze genome to construct the solution path
            Parallel.ForEach(mazeGenomes, mazeGenome => { mazeStructuresBag.Add(mazeDecoder.Decode(mazeGenome)); });

            // Convert to list to avoid multiple enumeration during parallel comparison
            return mazeStructuresBag.ToList();
        }

        /// <summary>
        ///     Extracts successful maze and navigator genome pairs from experiment maze trials.
        /// </summary>
        /// <param name="experimentId">The unique ID of the current experiment.</param>
        /// <param name="run">The current run number.</param>
        /// <param name="mazeGenomeIds">The list of maze genome IDs for which to find successful navigators.</param>
        /// <returns>Successful maze and navigator genome pairs.</returns>
        private static List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>>
            GetSuccessfulGenomeCombosFromMazeTrials(int experimentId, int run, IList<int> mazeGenomeIds)
        {
            // Get successful navigation trials during experiments (if produced)
            var perMazeSuccessfulTrials =
                ExperimentDataHandler.GetSuccessfulNavigationTrialPerMaze(
                    experimentId, run, mazeGenomeIds);

            var successfulGenomeCombos =
                new List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>>(perMazeSuccessfulTrials
                    .Count());

            // Get distinct maze and navigator genomes
            var mazeGenomeData =
                ExperimentDataHandler.GetMazeGenomeData(experimentId, run,
                    perMazeSuccessfulTrials.Select(trial => trial.MazeGenomeId).Distinct().ToList());
            var navigatorGenomeData =
                ExperimentDataHandler.GetNavigatorGenomeData(experimentId,
                    run, RunPhase.Primary,
                    perMazeSuccessfulTrials.Select(trial => trial.PairedNavigatorGenomeId).Distinct().ToList());

            // Build list of successful maze/navigator combinations
            successfulGenomeCombos.AddRange(
                perMazeSuccessfulTrials.Select(
                    successfulTrial =>
                        new Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>(
                            mazeGenomeData.First(gd => successfulTrial.MazeGenomeId == gd.GenomeId),
                            navigatorGenomeData.First(gd => successfulTrial.PairedNavigatorGenomeId == gd.GenomeId))));

            return successfulGenomeCombos;
        }

        /// <summary>
        ///     Extracts successful maze and navigator genome pairs from post-hoc experiment results analysis.
        /// </summary>
        /// <param name="experimentId">The unique ID of the current experiment.</param>
        /// <param name="run">The current run number.</param>
        /// <param name="mazeGenomeIds">The list of maze genome IDs for which to find successful navigators.</param>
        /// <returns>Successful maze and navigator genome pairs.</returns>
        private static List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>>
            GetSuccessfulGenomeCombosFromNavigationResults(int experimentId, int run, IList<int> mazeGenomeIds)
        {
            // Get any existing navigation results (this avoids rerunning failed combinations)
            var perMazeSuccessfulNavigations =
                ExperimentDataHandler.GetSuccessfulNavigationResultPerMaze(experimentId, run, mazeGenomeIds);

            var successfulGenomeCombos =
                new List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>>(perMazeSuccessfulNavigations
                    .Count());

            // Get distinct maze and navigator genomes
            var mazeGenomeData =
                ExperimentDataHandler.GetMazeGenomeData(experimentId, run,
                    perMazeSuccessfulNavigations.Select(nav => nav.MazeGenomeId).Distinct().ToList());
            var navigatorGenomeData =
                ExperimentDataHandler.GetNavigatorGenomeData(experimentId,
                    run, RunPhase.Primary,
                    perMazeSuccessfulNavigations.Select(nav => nav.NavigatorGenomeId).Distinct().ToList());

            // Build list of successful maze/navigator combinations
            successfulGenomeCombos.AddRange(
                perMazeSuccessfulNavigations.Select(
                    successfulNav =>
                        new Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>(
                            mazeGenomeData.First(gd => successfulNav.MazeGenomeId == gd.GenomeId),
                            navigatorGenomeData.First(gd => successfulNav.NavigatorGenomeId == gd.GenomeId))));

            return successfulGenomeCombos;
        }

        /// <summary>
        ///     Runs post-hoc analysis for all batches in the given experiment/run.  This can be part of either the initialization
        ///     or primary run phase.
        /// </summary>
        /// <param name="numBatches">The total number of batches containing genome data.</param>
        /// <param name="batchInterval">The number of batches to move forward on each iteration of the analysis loop.</param>
        /// <param name="runPhase">
        ///     Indicates whether this is part of the initialization or primary run phase.
        /// </param>
        /// <param name="experimentParameters">Experiment configuration parameters.</param>
        /// <param name="inputNeuronCount">Count of neurons in controller input layer.</param>
        /// <param name="outputNeuronCount">Count of neurons in controller output layer.</param>
        /// <param name="curRun">The run number.</param>
        /// <param name="numRuns">The total number of runs.</param>
        /// <param name="curExperimentConfiguration">The experiment configuration parameters.</param>
        /// <param name="generateSimulationResults">Indicates whether to write out the results of the batch simulation.</param>
        /// <param name="writeResultsToDatabase">
        ///     Indicates whether to write results directly into a database (if not, results are
        ///     written to a flat file).
        /// </param>
        /// <param name="generateTrajectoryData">Indicates whether the full navigator trajectory should be simulated and persisted.</param>
        /// <param name="generateTrajectoryDiversityScore">
        ///     Indicates whether quantification of navigator trajectory diversity
        ///     should be written out.
        /// </param>
        /// <param name="generateAgentTrajectoryClustering">Indicates whether the natural population clusters should be analyzed.</param>
        /// <param name="generateMazeClusters">Indicates whether the naturally occurring maze clusters should be analyzed.</param>
        /// <param name="generatePopulationEntropy">
        ///     Indicates whether the entropy of the resulting population clusters should be
        ///     analyzed.
        /// </param>
        /// <param name="useGreedySilhouetteCalculation">Indicates whether to use greedy silhouette width calculation strategy.</param>
        /// <param name="useEvenMazeTrajectoryDistribution">
        ///     Indicates whether to apply trajectory clustering evaluations evenly
        ///     across extant/navigable mazes (only applicable to non-specie based evaluation unit selection).
        /// </param>
        /// <param name="clusterRange">
        ///     The ceiling on the range of clusters for which to compute the silhouette width when using a
        ///     non-greedy silhouette width calculation.
        /// </param>
        /// <param name="sampleSize">The number of genomes sampled from the extant species for trajectory or clustering analysis.</param>
        /// <param name="sampleClusterObservationsFromSpecies">
        ///     Indicates whether to sample observations used in clustering analysis
        ///     from species or from the population as a whole.
        /// </param>
        private static void ProcessAndLogPerBatchResults(int numBatches, int batchInterval, RunPhase runPhase,
            ExperimentParameters experimentParameters, int inputNeuronCount, int outputNeuronCount, int curRun,
            int numRuns, ExperimentDictionary curExperimentConfiguration, bool generateSimulationResults,
            bool generateTrajectoryData, bool generateTrajectoryDiversityScore, bool generateAgentTrajectoryClustering,
            bool generateMazeClusters, bool generatePopulationEntropy,
            bool useGreedySilhouetteCalculation, bool useEvenMazeTrajectoryDistribution, int clusterRange,
            bool writeResultsToDatabase, int sampleSize, bool sampleClusterObservationsFromSpecies)
        {
            IList<MccexperimentMazeGenome> staticInitializationMazes = null;

            // If this invocation is processing initialization results, just get the maze up front as it will remain
            // the same throughout the initialization process
            if (runPhase == RunPhase.Initialization)
            {
                staticInitializationMazes =
                    ExperimentDataHandler.GetMazeGenomeData(curExperimentConfiguration.ExperimentDictionaryId,
                        curRun, 0);
            }

            // Iterate through each batch and evaluate maze/navigator combinations
            for (var curBatch = 1;
                curBatch <= numBatches;
                curBatch += curBatch == 1 ? batchInterval - 1 : batchInterval)
            {
                // Create the maze/navigator map
                var mapEvaluator = new MapEvaluator(experimentParameters, inputNeuronCount, outputNeuronCount);

                _executionLogger.Info(
                    $"Executing {runPhase} run phase analysis for batch [{curBatch}] of run [{curRun}/{numRuns}]");

                // Get any existing navigation results (this avoids rerunning failed combinations)
                var successfulNavigations =
                    ExperimentDataHandler.GetSuccessfulNavigations(curExperimentConfiguration.ExperimentDictionaryId,
                        curRun, curBatch);

                // If successful navigation results were found and we're not re-running the simulations,
                // initialize the map evaluator with only those combinations known to be successful
                if (generateSimulationResults == false && successfulNavigations != null &&
                    successfulNavigations.Count > 0)
                {
                    var successfulGenomeCombos =
                        new List<Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>>(successfulNavigations
                            .Count());

                    // Get distinct maze and navigator genomes
                    var mazeGenomeData =
                        ExperimentDataHandler.GetMazeGenomeData(curExperimentConfiguration.ExperimentDictionaryId,
                            curRun, curBatch - batchInterval + 1, curBatch,
                            successfulNavigations.Select(n => n.MazeGenomeId).Distinct().ToList());
                    var navigatorGenomeData =
                        ExperimentDataHandler.GetNavigatorGenomeData(curExperimentConfiguration.ExperimentDictionaryId,
                            curRun, curBatch, runPhase,
                            successfulNavigations.Select(n => n.NavigatorGenomeId).Distinct().ToList());

                    // Build list of successful maze/navigator combinations
                    successfulGenomeCombos.AddRange(
                        successfulNavigations.Select(
                            successfulNav =>
                                new Tuple<MccexperimentMazeGenome, MccexperimentNavigatorGenome>(
                                    mazeGenomeData.First(gd => successfulNav.MazeGenomeId == gd.GenomeId),
                                    navigatorGenomeData.First(gd => successfulNav.NavigatorGenomeId == gd.GenomeId))));

                    // Initialize the maze/navigator map with combinations that are known to be successful
                    mapEvaluator.Initialize(successfulGenomeCombos);
                }
                // Otherwise, just initialize with all combinations
                else
                {
                    // Initialize the maze/navigator map with the serialized maze and navigator data (this does the parsing)
                    // Note that we consider maze genomes one batch in the past in the event that a solved one aged out
                    mapEvaluator.Initialize(
                        runPhase == RunPhase.Initialization
                            ? staticInitializationMazes
                            : ExperimentDataHandler.GetMazeGenomeData(curExperimentConfiguration.ExperimentDictionaryId,
                                curRun, curBatch - batchInterval + 1, curBatch),
                        ExperimentDataHandler.GetNavigatorGenomeData(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch, runPhase));
                }

                // Evaluate all of the maze/navigator combinations in the batch (if analysis is based on trajectory data)
                if (generateSimulationResults || generateTrajectoryData || generateTrajectoryDiversityScore ||
                    generateAgentTrajectoryClustering || generatePopulationEntropy)
                {
                    mapEvaluator.RunTrajectoryEvaluations();
                }

                if (generateSimulationResults)
                {
                    // Save the evaluation results
                    ExperimentDataHandler.WriteNavigatorMazeEvaluationData(
                        curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch, runPhase,
                        mapEvaluator.EvaluationUnits, CommitPageSize, writeResultsToDatabase);
                }

                if (generateTrajectoryData && runPhase != RunPhase.Initialization)
                {
                    // Write out the full trajectory of all agents through all solved mazes
                    ExperimentDataHandler.WriteTrajectoryData(curExperimentConfiguration.ExperimentDictionaryId, curRun,
                        curBatch, mapEvaluator.EvaluationUnits, CommitPageSize, writeResultsToDatabase);
                }

                // Compare trajectories of agents through maze to get quantitative sense of solution diversity
                // Mean euclidean distance will be calculated for selected trajectory against:
                // 1. Other agent trajectories in the current maze only
                // 2. Other agent trajectories on *another* maze only
                // 3. All other agent trajectories (regardless of maze)
                if (generateTrajectoryDiversityScore && runPhase != RunPhase.Initialization)
                {
                    // If sample size is specified, then trajectory diversity is based on a sample of the population 
                    // instead of an exhaustive evaluation
                    if (sampleSize > 0)
                    {
                        var rnd = new Random();

                        ExperimentDataHandler.WriteTrajectoryDiversityData(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                            EvaluationHandler.CalculateTrajectoryDiversity(
                                mapEvaluator.EvaluationUnits.OrderBy(eu => rnd.Next()).Take(sampleSize).ToList()),
                            writeResultsToDatabase);
                    }
                    // Otherwise, trajectory diversity comparison is exhaustive
                    else
                    {
                        ExperimentDataHandler.WriteTrajectoryDiversityData(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                            EvaluationHandler.CalculateTrajectoryDiversity(mapEvaluator.EvaluationUnits),
                            writeResultsToDatabase);
                    }
                }

                // Only write clustering results for primary runs when the number of trajectories have surpassed 
                // the minimum cluster count of 3
                if (generateAgentTrajectoryClustering && runPhase != RunPhase.Initialization)
                {
                    // If sample size is specified, then clustering evaluation is based on a sample of the population
                    // instead of an exhaustive evaluation
                    if (sampleSize > 0)
                    {
                        // Extract uniform samples of maze and navigator genomes either from each extant specie
                        // or from the entire population on which to run clustering analysis (note that event maze
                        // trajectory sampling is only available for non-specie evaluation unit extraction)
                        var evaluationSamples = sampleClusterObservationsFromSpecies
                            ? DataManipulationUtil.ExtractEvaluationUnitSamplesFromSpecies(
                                curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                                mapEvaluator.EvaluationUnits.Where(eu => eu.IsMazeSolved).ToList(), sampleSize)
                            : DataManipulationUtil.ExtractEvaluationUnitSamplesFromPopulation(
                                mapEvaluator.EvaluationUnits.Where(eu => eu.IsMazeSolved).ToList(),
                                sampleSize, useEvenMazeTrajectoryDistribution);

                        // Calculate natural clustering of the population trajectories at each point in time and persist
                        ExperimentDataHandler.WriteClusteringDiversityData(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                            EvaluationHandler.CalculateAgentTrajectoryClustering(evaluationSamples,
                                useGreedySilhouetteCalculation, clusterRange),
                            OutputFileType.NaturalClusterData, writeResultsToDatabase);
                    }
                    // Otherwise, evaluation is exhaustive (i.e. includes all trajectories in the population)
                    else
                    {
                        ExperimentDataHandler.WriteClusteringDiversityData(
                            curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                            EvaluationHandler.CalculateAgentTrajectoryClustering(
                                mapEvaluator.EvaluationUnits.Where(eu => eu.IsMazeSolved).ToList(),
                                useGreedySilhouetteCalculation, clusterRange),
                            OutputFileType.NaturalClusterData, writeResultsToDatabase);
                    }
                }

                if (generateMazeClusters && runPhase != RunPhase.Initialization)
                {
                    // Calculate maze clustering and persist
                    ExperimentDataHandler.WriteClusteringDiversityData(
                        curExperimentConfiguration.ExperimentDictionaryId, curRun, curBatch,
                        EvaluationHandler.CalculateMazeClustering(
                            ExperimentDataHandler.GetMazeGenomeXml(curExperimentConfiguration.ExperimentDictionaryId,
                                curRun,
                                curBatch), useGreedySilhouetteCalculation, clusterRange),
                        OutputFileType.MazeClusterData,
                        writeResultsToDatabase);
                }

                if (generatePopulationEntropy && runPhase != RunPhase.Initialization)
                {
                    // Calculate population entropy and persist
                    ExperimentDataHandler.WritePopulationEntropyData(curExperimentConfiguration.ExperimentDictionaryId,
                        curRun, curBatch,
                        EvaluationHandler.CalculatePopulationEntropy(mapEvaluator.EvaluationUnits,
                            curExperimentConfiguration.NumSeedAgentGenomes), writeResultsToDatabase);
                }
            }
        }

        /// <summary>
        ///     Populates the execution configuration and checks for any errors in said configuration.
        /// </summary>
        /// <param name="executionArguments">The arguments with which the experiment executor is being invoked.</param>
        /// <returns>Boolean status indicating whether parsing the configuration suceeded.</returns>
        private static bool ParseAndValidateConfiguration(string[] executionArguments)
        {
            var isConfigurationValid = executionArguments != null;

            // Only continue if there are execution arguments
            if (executionArguments != null && executionArguments.Length > 0)
            {
                foreach (var executionArgument in executionArguments)
                {
                    // Get the key/value pair
                    var parameterValuePair = executionArgument.Split('=');

                    // Attempt to parse the current parameter
                    isConfigurationValid =
                        Enum.TryParse(parameterValuePair[0], true, out ExecutionParameter curParameter);

                    // If the current parameter is not valid, break out of the loop and return
                    if (isConfigurationValid == false)
                    {
                        _executionLogger.Error($"[{parameterValuePair[0]}] is not a valid configuration parameter.");
                        break;
                    }

                    // If the parameter is valid but it already exists in the map, break out of the loop and return
                    if (_executionConfiguration.ContainsKey(curParameter))
                    {
                        _executionLogger.Error(
                            $"Ambiguous configuration - parameter [{curParameter}] has been specified more than once.");
                        break;
                    }

                    switch (curParameter)
                    {
                        // Ensure valid agent input/output neuron counts were specified
                        case ExecutionParameter.AgentNeuronInputCount:
                        case ExecutionParameter.AgentNeuronOutputCount:
                        case ExecutionParameter.StartFromRun:
                        case ExecutionParameter.SampleSize:
                            int testInt;
                            if (int.TryParse(parameterValuePair[1], out testInt) == false)
                            {
                                _executionLogger.Error($"The value for parameter [{curParameter}] must be an integer.");
                                isConfigurationValid = false;
                            }

                            break;

                        // Ensure that valid boolean values were given
                        case ExecutionParameter.GenerateTrajectoryData:
                        case ExecutionParameter.GenerateSimulationResults:
                        case ExecutionParameter.WriteResultsToDatabase:
                        case ExecutionParameter.GenerateMazeBitmaps:
                        case ExecutionParameter.GenerateAgentTrajectoryBitmaps:
                        case ExecutionParameter.GenerateAgentDiversityScores:
                        case ExecutionParameter.GenerateMazeDiversityScores:
                        case ExecutionParameter.GenerateDeceptiveTurnCounts:
                        case ExecutionParameter.GenerateAgentTrajectoryClusters:
                        case ExecutionParameter.GenerateMazeClusters:
                        case ExecutionParameter.GeneratePopulationEntropy:
                        case ExecutionParameter.UseGreedySilhouetteCalculation:
                        case ExecutionParameter.IsDistributedExecution:
                        case ExecutionParameter.ExecuteInitializationTrials:
                        case ExecutionParameter.SampleFromSpecies:
                        case ExecutionParameter.UseEvenMazeTrajectoryDistribution:
                            if (bool.TryParse(parameterValuePair[1], out _) == false)
                            {
                                _executionLogger.Error($"The value for parameter [{curParameter}] must be a boolean.");
                                isConfigurationValid = false;
                            }

                            break;
                    }

                    // If all else checks out, add the parameter to the map
                    _executionConfiguration.Add(curParameter, parameterValuePair[1]);
                }
            }
            // If there are no execution arguments, the configuration is invalid
            else
            {
                isConfigurationValid = false;
            }

            // If the per-parameter configuration is valid but not a full list of parameters were specified, makes sure the necessary ones are present
            if (isConfigurationValid && _executionConfiguration.Count ==
                Enum.GetNames(typeof(ExecutionParameter)).Length == false)
            {
                // Check for existence of experiment names to execute
                if (_executionConfiguration.ContainsKey(ExecutionParameter.ExperimentNames) == false)
                {
                    _executionLogger.Error($"Parameter [{ExecutionParameter.ExperimentNames}] must be specified.");
                    isConfigurationValid = false;
                }

                // Check for existence of input neuron count
                if (_executionConfiguration.ContainsKey(ExecutionParameter.AgentNeuronInputCount) == false)
                {
                    _executionLogger.Error(
                        $"Parameter [{ExecutionParameter.AgentNeuronInputCount}] must be specified.");
                    isConfigurationValid = false;
                }

                // Check for existence of output neuron count
                if (_executionConfiguration.ContainsKey(ExecutionParameter.AgentNeuronOutputCount) == false)
                {
                    _executionLogger.Error(
                        $"Parameter [{ExecutionParameter.AgentNeuronOutputCount}] must be specified.");
                    isConfigurationValid = false;
                }

                // If we're generating experiment result data (default is true) and logging to a flat file instead of the database 
                // (default is true), the output directory must be set
                if ((_executionConfiguration.ContainsKey(ExecutionParameter.GenerateSimulationResults) == false ||
                     Convert.ToBoolean(_executionConfiguration[ExecutionParameter.GenerateSimulationResults])) &&
                    (_executionConfiguration.ContainsKey(ExecutionParameter.WriteResultsToDatabase) == false ||
                     Convert.ToBoolean(_executionConfiguration[ExecutionParameter.WriteResultsToDatabase]) == false) &&
                    _executionConfiguration.ContainsKey(ExecutionParameter.DataFileOutputDirectory) == false)
                {
                    _executionLogger.Error(
                        "The data file output directory must be specified when generating experiment result data and writing results to a flat file instead of the database.");
                    isConfigurationValid = false;
                }

                // If natural clustering is being generated, then the specie sample size must be specified
                if (_executionConfiguration.ContainsKey(ExecutionParameter.GenerateAgentTrajectoryClusters) &&
                    Convert.ToBoolean(_executionConfiguration[ExecutionParameter.GenerateAgentTrajectoryClusters]) &&
                    _executionConfiguration.ContainsKey(ExecutionParameter.SampleSize) == false)
                {
                    _executionLogger.Error(
                        "The specie sample size must be specified when generating natural clustering statistics.");
                    isConfigurationValid = false;
                }

                // If non-greedy silhouette calculations are being used, then cluster range must be specified and set to a valid value (greater than 0)
                if (_executionConfiguration.ContainsKey(ExecutionParameter.UseGreedySilhouetteCalculation) &&
                    Convert.ToBoolean(_executionConfiguration[ExecutionParameter.UseGreedySilhouetteCalculation]) ==
                    false &&
                    (_executionConfiguration.ContainsKey(ExecutionParameter.ClusterRange) == false ||
                     int.Parse(_executionConfiguration[ExecutionParameter.ClusterRange]) <= 0))
                {
                    _executionLogger.Error(
                        "The cluster range must be specified and set to a value greater than 0 if non-greedy silhouette calculations are being used.");
                    isConfigurationValid = false;
                }

                // If the executor is going to produce maze bitmap images (default is true), then the base output directory must be specified
                if ((_executionConfiguration.ContainsKey(ExecutionParameter.GenerateMazeBitmaps) == false ||
                     Convert.ToBoolean(_executionConfiguration[ExecutionParameter.GenerateMazeBitmaps])) &&
                    _executionConfiguration.ContainsKey(ExecutionParameter.BitmapOutputBaseDirectory) == false)
                {
                    _executionLogger.Error(
                        "The bitmap image base directory must be specified when producing maze images.");
                    isConfigurationValid = false;
                }

                // If the executor is going to produce navigator trajectory bitmap images (default is true), 
                // then the base output directory must be specified
                if ((_executionConfiguration.ContainsKey(ExecutionParameter.GenerateAgentTrajectoryBitmaps) == false ||
                     Convert.ToBoolean(_executionConfiguration[ExecutionParameter.GenerateAgentTrajectoryBitmaps])) &&
                    _executionConfiguration.ContainsKey(ExecutionParameter.BitmapOutputBaseDirectory) == false)
                {
                    _executionLogger.Error(
                        "The bitmap image base directory must be specified when producing navigator trajectory images.");
                    isConfigurationValid = false;
                }

                // If this is distributed execution, the StartFromRun parameter must be specified as this
                // is used to control which node is executing which experiment analysis run
                if (_executionConfiguration.ContainsKey(ExecutionParameter.IsDistributedExecution) &&
                    Convert.ToBoolean(_executionConfiguration[ExecutionParameter.IsDistributedExecution]) &&
                    _executionConfiguration.ContainsKey(ExecutionParameter.StartFromRun) == false)
                {
                    _executionLogger.Error(
                        "If this is a distributed execution, the StartFromRun parameter must be specified via the invoking job.");
                    isConfigurationValid = false;
                }

                // Ensure that the analysis scope was specified and that it matches one of the defined scopes
                if (_executionConfiguration.ContainsKey(ExecutionParameter.AnalysisScope) == false ||
                    _executionConfiguration[ExecutionParameter.AnalysisScope].Equals(AnalysisScope.Full.ToString(),
                        StringComparison.InvariantCultureIgnoreCase) &&
                    _executionConfiguration[ExecutionParameter.AnalysisScope].Equals(
                        AnalysisScope.Aggregate.ToString(), StringComparison.InvariantCultureIgnoreCase) &&
                    _executionConfiguration[ExecutionParameter.AnalysisScope].Equals(AnalysisScope.Last.ToString(),
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    _executionLogger.Error(
                        "The AnalysisScope parameter must be well-specified and via the invoking job.");
                    isConfigurationValid = false;
                }
            }

            // If there's still no problem with the configuration, go ahead and return valid
            if (isConfigurationValid) return true;

            // Log the boiler plate instructions when an invalid configuration is encountered
            _executionLogger.Error("The experiment executor invocation must take the following form:");
            _executionLogger.Error(string.Format(
                "NavigatorMazeMapEvaluator.exe \n\t" +
                $"Required: {ExecutionParameter.AgentNeuronInputCount}=[# Input Neurons] {ExecutionParameter.AgentNeuronOutputCount}=[# Output Neurons] \n\t" +
                $"Optional: {ExecutionParameter.AnalysisScope}=[Full, Aggregate, Last] \n\t" +
                $"Optional: {ExecutionParameter.GenerateSimulationResults}=[{{0}}] {ExecutionParameter.WriteResultsToDatabase}=[{{0}}] (Required: {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateMazeBitmaps}=[{{0}}] (Required: {ExecutionParameter.BitmapOutputBaseDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateAgentTrajectoryBitmaps}=[{{0}}] (Required: {ExecutionParameter.BitmapOutputBaseDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateAgentTrajectoryClusters}=[{{0}}] (Required: {ExecutionParameter.UseGreedySilhouetteCalculation}=[{{0}}] {ExecutionParameter.ClusterRange}=[{{2}}] {ExecutionParameter.SampleSize}=[{{3}}] {ExecutionParameter.UseEvenMazeTrajectoryDistribution}=[{{0}}] {ExecutionParameter.SampleFromSpecies}=[{{0}}] {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateMazeClusters}=[{{0}}] (Required: {ExecutionParameter.UseGreedySilhouetteCalculation}=[{{0}}] {ExecutionParameter.ClusterRange}=[{{2}}] {ExecutionParameter.SampleFromSpecies}=[{{0}}] {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateAgentDiversityScores}=[{{0}}] (Required: {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateMazeDiversityScores}=[{{0}}] (Required: {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GenerateTrajectoryData}=[{{0}}] (Required: {ExecutionParameter.SampleSize}=[{{3}}] {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.GeneratePopulationEntropy}=[{{0}}] (Required: {ExecutionParameter.DataFileOutputDirectory}=[{{1}}]) \n\t" +
                $"Optional: {ExecutionParameter.StartFromRun}=[starting run #] \n\t" +
                $"Optional: {ExecutionParameter.ExecuteInitializationTrials}=[{{0}}] \n\t" +
                $"Optional: {ExecutionParameter.IsDistributedExecution}=[{{0}}] \n\t" +
                $"Required: {ExecutionParameter.ExperimentNames}=[experiment,experiment,...]",
                "true|false", "directory", "cluster range #", "sample #")
            );

            return false;
        }
    }
}