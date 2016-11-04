﻿#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ExperimentEntities;
using RunPhase = SharpNeat.Core.RunPhase;

#endregion

namespace MazeExperimentSuppotLib
{
    /// <summary>
    ///     Provides methods for interfacing with and writing reuslts to the experiment database or flat file.
    /// </summary>
    public static class ExperimentDataHandler
    {
        /// <summary>
        ///     Field delimiter used in output files.
        /// </summary>
        private const string FileDelimiter = ",";

        /// <summary>
        ///     Map of output file stream writers.
        /// </summary>
        private static readonly Dictionary<OutputFileType, StreamWriter> FileWriters =
            new Dictionary<OutputFileType, StreamWriter>();

        #region Public generic writer methods

        /// <summary>
        ///     Writes the given evaluation results to the experiment database or to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="runPhase">
        ///     Indicates whether this is part of the initialization or primary experiment phase.
        /// </param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        /// <param name="commitPageSize">The number of records that are committed within a single batch/context.</param>
        /// <param name="writeToDatabase">
        ///     Indicates whether evaluation results should be written directly to the database or to a
        ///     flat file.
        /// </param>
        public static void WriteNavigatorMazeEvaluationData(int experimentId, int run, int batch, RunPhase runPhase,
            IList<MazeNavigatorEvaluationUnit> evaluationUnits, int commitPageSize, bool writeToDatabase)
        {
            // Write results to the database if the option has been specified
            if (writeToDatabase)
            {
                WriteNavigatorMazeEvaluationDataToDatabase(experimentId, run, batch, runPhase, evaluationUnits,
                    commitPageSize);
            }
            // Otherwise, write to the flat file output
            else
            {
                WriteNavigatorMazeEvaluationDataToFile(experimentId, run, batch, runPhase, evaluationUnits);
            }
        }

        /// <summary>
        ///     Writes the given evaluation results containing the full trajectory characterization to the experiment database or
        ///     to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        /// <param name="commitPageSize">The number of records that are committed within a single batch/context.</param>
        /// <param name="writeToDatabase">
        ///     Indicates whether evaluation results should be written directly to the database or to a
        ///     flat file.
        /// </param>
        public static void WriteTrajectoryData(int experimentId, int run, int batch,
            IList<MazeNavigatorEvaluationUnit> evaluationUnits, int commitPageSize, bool writeToDatabase)
        {
            // Write results to the database if the option has been specified
            if (writeToDatabase)
            {
                throw new NotImplementedException("Direct write to database for trajectory data not yet implemented!");
            }
            // Otherwise, write to the flat file output
            WriteTrajectoryDataToFile(experimentId, run, batch, evaluationUnits);
        }

        /// <summary>
        ///     Writes the given trajectory diversity results to the experiment database or to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="trajectoryDiversityUnits">The trajectory diversity results.</param>
        /// <param name="writeToDatabase">
        ///     Indicates whether evaluation results should be written directly to the database or to a
        ///     flat file.
        /// </param>
        public static void WriteTrajectoryDiversityData(int experimentId, int run, int batch,
            IList<TrajectoryDiversityUnit> trajectoryDiversityUnits, bool writeToDatabase)
        {
            // Write results to the database if the option has been specified
            if (writeToDatabase)
            {
                throw new NotImplementedException(
                    "Direct write to database for trajectory diversity not yet implemented!");
            }
            // Otherwise, write to the flat file output
            WriteTrajectoryDiversityDataToFile(experimentId, run, batch, trajectoryDiversityUnits);
        }

        /// <summary>
        ///     Writes the coevolution vs. novelty search comparison results to the experiment database or to a flat file.
        /// </summary>
        /// <param name="coEvoExperimentId">The coevolution experiment that was executed.</param>
        /// <param name="nsExperimentId">The novelty search experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="mazeGenomeId">
        ///     The unique identifier of the maze domain on which the novelty search comparison was
        ///     executed.
        /// </param>
        /// <param name="mazeBirthBatch">The birth batch (generation) of the maze.</param>
        /// <param name="nsAgentMinComplexity">The minimum complexity of the novelty search population at the end of the run.</param>
        /// <param name="nsAgentMaxComplexity">The maximum complexity of the novelty search population at the end of the run.</param>
        /// <param name="nsAgentMeanComplexity">The mean complexity of the novelty search population at the end of the run.</param>
        /// <param name="coEvoEvaluations">
        ///     The number of evaluations executed by the coevolution algorithm in order to arrive at
        ///     the given maze structure.
        /// </param>
        /// <param name="nsEvaluations">
        ///     The total number of evaluations executed by the novelty search algorithm in order to solve
        ///     (or attempt to solve) the coevolution-discovered maze structure.
        /// </param>
        /// <param name="isSolved">Flag indicating whether novelty search was successful in solving the maze.</param>
        /// <param name="writeToDatabase">Flag indicating whether to write directly into the experiment database or to a flat file.</param>
        public static void WriteNoveltySearchComparisonResults(int coEvoExperimentId, int nsExperimentId, int run,
            int batch, int mazeGenomeId,
            int mazeBirthBatch, int nsAgentMinComplexity, int nsAgentMaxComplexity, double nsAgentMeanComplexity,
            int coEvoEvaluations, int nsEvaluations, bool isSolved, bool writeToDatabase)
        {
            // Write results to the database if the option has been specified
            if (writeToDatabase)
            {
                throw new NotImplementedException(
                    "Direct write to database for novelty search comparison not yet implemented!");
            }
            // Otherwise, write to the flat file output
            WriteNoveltySearchComparisonResultsToFile(coEvoExperimentId, nsExperimentId, run, batch, mazeGenomeId,
                mazeBirthBatch, nsAgentMinComplexity, nsAgentMaxComplexity, nsAgentMeanComplexity, coEvoEvaluations,
                nsEvaluations, isSolved);
        }

        #endregion

        #region Public database read methods

        /// <summary>
        ///     Looks up an experiment configuration given the unique experiment name.
        /// </summary>
        /// <param name="experimentName">The experiment name whose configuration to lookup.</param>
        /// <returns>The corresponding experiment configuration (i.e. experiment dictionary).</returns>
        public static ExperimentDictionary LookupExperimentConfiguration(string experimentName)
        {
            ExperimentDictionary experimentConfiguration;

            // Query for the experiment configuration given the name (which is guaranteed to be unique)
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                experimentConfiguration =
                    context.ExperimentDictionaries.Single(expName => expName.ExperimentName == experimentName);
            }

            return experimentConfiguration;
        }

        /// <summary>
        ///     Retrieves the number of runs that were executed for a given experiment.
        /// </summary>
        /// <param name="experimentId">The experiment for which to compute the number of corresponding runs.</param>
        /// <returns>The number of runs that were executed for the given experiment.</returns>
        public static int GetNumRuns(int experimentId)
        {
            int numRuns;

            // Query for the number of runs for the given experiment
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                numRuns = context.CoevolutionMCSMazeExperimentEvaluationDatas.Where(
                    expData => expData.ExperimentDictionaryID == experimentId).Select(row => row.Run).Distinct().Count();
            }

            return numRuns;
        }

        /// <summary>
        ///     Retrieves the number of batches that were executed during a given run of a given experiment.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <returns>The number of batches in the given run.</returns>
        public static int GetNumBatchesForRun(int experimentId, int run)
        {
            int numBatches;

            // Query for the number of batches in the current run of the given experiment
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                numBatches = context.CoevolutionMCSMazeExperimentEvaluationDatas.Where(
                    expData => expData.ExperimentDictionaryID == experimentId && expData.Run == run)
                    .Select(row => row.Generation)
                    .Max();
            }

            return numBatches;
        }

        /// <summary>
        ///     Retrieves the batches during a given run of a given experiment that have associated genome data.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="runPhase">The run phase (i.e. initialization or primary) for which to get the associated batches.</param>
        /// <returns>The collection of batch numbers in the given experiment/run that have associated genome data.</returns>
        public static IList<int> GetBatchesWithGenomeData(int experimentId, int run, RunPhase runPhase)
        {
            IList<int> batchesWithGenomeData;

            // Query for the distinct batches in the current run of the given experiment
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                batchesWithGenomeData = context.CoevolutionMCSNavigatorExperimentGenomes.Where(
                    expData =>
                        expData.ExperimentDictionaryID == experimentId && expData.Run == run &&
                        expData.RunPhase.RunPhaseName == runPhase.ToString())
                    .Select(row => row.Generation)
                    .Distinct().OrderBy(row => row).ToList();
            }

            return batchesWithGenomeData;
        }

        /// <summary>
        ///     Retrieves the maze genome XML for a particular batch of a given run/experiment.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <returns>The maze genome XML.</returns>
        public static IList<string> GetMazeGenomeXml(int experimentId, int run, int batch)
        {
            return GetMazeGenomeData(experimentId, run, batch).Select(mazeGenome => mazeGenome.GenomeXml).ToList();
        }

        /// <summary>
        ///     Retrieves the maze genome data (i.e. evaluation statistics and XML) for a particular batch of a given
        ///     run/experiemnt.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <returns>The maze genome data.</returns>
        public static IList<CoevolutionMCSMazeExperimentGenome> GetMazeGenomeData(int experimentId, int run, int batch)
        {
            IList<CoevolutionMCSMazeExperimentGenome> mazeGenomes;

            // Query for maze genomes logged during the current batch
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                mazeGenomes = context.CoevolutionMCSMazeExperimentGenomes.Where(
                    expData =>
                        expData.ExperimentDictionaryID == experimentId && expData.Run == run &&
                        expData.Generation == batch).ToList();
            }

            return mazeGenomes;
        }

        /// <summary>
        ///     Retrieves the navigator genome data (i.e. evaluation statistics and XML) for a particular batch of a given
        ///     run/experiemnt.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="runPhase">
        ///     Indicates whether this is part of the initialization or primary experiment phase.
        /// </param>
        /// <returns>The navigator genome data.</returns>
        public static IList<CoevolutionMCSNavigatorExperimentGenome> GetNavigatorGenomeData(int experimentId, int run,
            int batch, RunPhase runPhase)
        {
            IList<CoevolutionMCSNavigatorExperimentGenome> navigatorGenomes;

            // Query for navigator genomes logged during the current batch
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                navigatorGenomes =
                    context.CoevolutionMCSNavigatorExperimentGenomes.Where(
                        expData =>
                            expData.ExperimentDictionaryID == experimentId && expData.Run == run &&
                            expData.Generation == batch && expData.RunPhase.RunPhaseName == runPhase.ToString())
                        .ToList();
            }

            return navigatorGenomes;
        }

        /// <summary>
        ///     Retrieves the total number of initialization evaluations that were performed for the given experiment run.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <returns>The total number of initialization evaluations.</returns>
        public static int GetInitializationEvaluationsForRun(int experimentId, int run)
        {
            int initEvaluations;

            // Query for the maximum initialization evaluations
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                initEvaluations =
                    context.CoevolutionMCSNavigatorExperimentEvaluationDatas.Where(
                        navigatorData =>
                            navigatorData.ExperimentDictionaryID == experimentId && navigatorData.Run == run &&
                            navigatorData.RunPhase.RunPhaseName == RunPhase.Initialization.ToString())
                        .Max(navigatorData => navigatorData.TotalEvaluations);
            }

            return initEvaluations;
        }

        /// <summary>
        ///     Retrieves the total number of primary evaluations executes by mazes and navigators at the given experiment
        ///     run/batch.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <returns>The total number of primary evaluations from both the maze and navigator queues.</returns>
        public static int GetTotalPrimaryEvaluationsAtBatch(int experimentId, int run, int batch)
        {
            int mazeEvaluations, navigatorEvaluations;

            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                // Get the total maze evaluations at the given batch
                mazeEvaluations =
                    context.CoevolutionMCSMazeExperimentEvaluationDatas.Where(
                        mazeData =>
                            mazeData.ExperimentDictionaryID == experimentId && mazeData.Run == run &&
                            mazeData.Generation == batch).Select(mazeData => mazeData.TotalEvaluations).First();

                // Get the total navigator evaluations at the given batch
                navigatorEvaluations =
                    context.CoevolutionMCSNavigatorExperimentEvaluationDatas.Where(
                        navigatorData =>
                            navigatorData.ExperimentDictionaryID == experimentId && navigatorData.Run == run &&
                            navigatorData.Generation == batch &&
                            navigatorData.RunPhase.RunPhaseName == RunPhase.Primary.ToString())
                        .Select(navigatorData => navigatorData.TotalEvaluations)
                        .Single();
            }

            return mazeEvaluations + navigatorEvaluations;
        }

        #endregion

        #region Public file writer methods

        /// <summary>
        ///     Opens the file stream writer.
        /// </summary>
        /// <param name="fileName">The name of the flat file to write into.</param>
        /// <param name="fileType">The type of data being written.</param>
        public static void OpenFileWriter(string fileName, OutputFileType fileType)
        {
            // Make sure a writer of the given type has not already been opened.
            if (FileWriters.ContainsKey(fileType))
            {
                throw new Exception(string.Format("File writer for type {0} already opened.", fileType));
            }

            // Open the stream writer
            FileWriters.Add(fileType, new StreamWriter(fileName) {AutoFlush = true});
        }

        /// <summary>
        ///     Closes the file stream writer.
        /// </summary>
        public static void CloseFileWriter(OutputFileType fileType)
        {
            // Make sure the file writer actually exists before attempting to close it
            if (FileWriters.ContainsKey(fileType) == false)
            {
                throw new Exception(
                    string.Format("Cannot close file writer as no file writer of type {0} has been created.", fileType));
            }

            // Close the file writer and dispose of the stream
            FileWriters[fileType].Close();
            FileWriters[fileType].Dispose();
        }

        /// <summary>
        ///     Writes empty "sentinel" file so that data transfer agent on cluster can easily identify that a given experiment is
        ///     complete.
        /// </summary>
        /// <param name="experimentFilename">The base path and filename of the experiment under execution.</param>
        public static void WriteSentinelFile(string experimentFilename)
        {
            // Write sentinel file to the given output directory
            using (File.Create(string.Format("{0} - COMPLETE", experimentFilename)))
            {
            }
        }

        /// <summary>
        ///     Writes empty "sentinel" file so that data transfer agent on cluster can easily identify that a given experiment run
        ///     is complete.
        /// </summary>
        /// <param name="experimentFilename">The base path and filename of the experiment under execution.</param>
        /// <param name="run">The completed run.</param>
        public static void WriteSentinelFile(string experimentFilename, int run)
        {
            // Write sentinel file to the given output directory
            using (File.Create(string.Format("{0} - Run {1} - COMPLETE", experimentFilename, run)))
            {
            }
        }

        #endregion

        #region Private static methods

        /// <summary>
        ///     Retrieves the primary key associated with the given run phase.
        /// </summary>
        /// <param name="runPhase">The run phase for which to lookup the key.</param>
        /// <returns>The key (in the "RunPhase" database table) for the given run phase object.</returns>
        private static int GetRunPhaseKey(RunPhase runPhase)
        {
            int runPhaseKey;

            // Query for the run phase key
            using (ExperimentDataEntities context = new ExperimentDataEntities())
            {
                runPhaseKey =
                    context.RunPhases.First(runPhaseData => runPhaseData.RunPhaseName == runPhase.ToString()).RunPhaseID;
            }

            return runPhaseKey;
        }

        /// <summary>
        ///     Writes the given evaluation results to the experiment database.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="runPhase">
        ///     Indicates whether this is part of the initialization or primary experiment phase.
        /// </param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        /// <param name="commitPageSize">The number of records that are committed within a single batch/context.</param>
        private static void WriteNavigatorMazeEvaluationDataToDatabase(int experimentId, int run, int batch,
            RunPhase runPhase,
            IList<MazeNavigatorEvaluationUnit> evaluationUnits, int commitPageSize)
        {
            // Page through the result set, committing each in the specified batch size
            for (int curPage = 0; curPage <= evaluationUnits.Count/commitPageSize; curPage++)
            {
                IList<CoevolutionMCSMazeNavigatorResult> serializedResults =
                    new List<CoevolutionMCSMazeNavigatorResult>(commitPageSize);

                // Go ahead and lookup the run phase key for all of the records
                // (instead of hitting the database on every iteration of the below loop)
                int runPhaseKey = GetRunPhaseKey(runPhase);

                // Build a list of serialized results
                foreach (
                    MazeNavigatorEvaluationUnit evaluationUnit in
                        evaluationUnits.Skip(curPage*commitPageSize).Take(commitPageSize))
                {
                    serializedResults.Add(new CoevolutionMCSMazeNavigatorResult
                    {
                        ExperimentDictionaryID = experimentId,
                        Run = run,
                        Generation = batch,
                        RunPhase_FK = runPhaseKey,
                        MazeGenomeID = evaluationUnit.MazeId,
                        NavigatorGenomeID = evaluationUnit.AgentId,
                        IsMazeSolved = evaluationUnit.IsMazeSolved,
                        NumTimesteps = evaluationUnit.NumTimesteps
                    });
                }

                // Create a new context and persist the batch
                using (ExperimentDataEntities context = new ExperimentDataEntities())
                {
                    // Auto-detect changes and save validation are switched off to speed things up
                    context.Configuration.AutoDetectChangesEnabled = false;
                    context.Configuration.ValidateOnSaveEnabled = false;

                    context.CoevolutionMCSMazeNavigatorResults.AddRange(serializedResults);
                    context.SaveChanges();
                }
            }
        }

        /// <summary>
        ///     Writes the given evaluation results to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="runPhase">
        ///     Indicates whether this is part of the initialization or primary experiment phase.
        /// </param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        private static void WriteNavigatorMazeEvaluationDataToFile(int experimentId, int run, int batch,
            RunPhase runPhase, IList<MazeNavigatorEvaluationUnit> evaluationUnits)
        {
            // Make sure the file writer actually exists before attempting to write to it
            if (FileWriters.ContainsKey(OutputFileType.NavigatorMazeEvaluationData) == false)
            {
                throw new Exception(
                    string.Format("Cannot write to output stream as no file writer of type {0} has been created.",
                        OutputFileType.NavigatorMazeEvaluationData));
            }

            // Loop through the evaluation units and write each row
            foreach (MazeNavigatorEvaluationUnit evaluationUnit in evaluationUnits)
            {
                FileWriters[OutputFileType.NavigatorMazeEvaluationData].WriteLine(string.Join(FileDelimiter,
                    new List<string>
                    {
                        experimentId.ToString(),
                        run.ToString(),
                        batch.ToString(),
                        runPhase.ToString(),
                        evaluationUnit.MazeId.ToString(),
                        evaluationUnit.AgentId.ToString(),
                        evaluationUnit.IsMazeSolved.ToString(),
                        evaluationUnit.NumTimesteps.ToString()
                    }));
            }

            // Immediately flush to the output file
            FileWriters[OutputFileType.NavigatorMazeEvaluationData].Flush();
        }

        /// <summary>
        ///     Writes each position within each trajectory to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        private static void WriteTrajectoryDataToFile(int experimentId, int run, int batch,
            IList<MazeNavigatorEvaluationUnit> evaluationUnits)
        {
            // Make sure the file writer actually exists before attempting to write to it
            if (FileWriters.ContainsKey(OutputFileType.TrajectoryData) == false)
            {
                throw new Exception(
                    string.Format("Cannot write to output stream as no file writer of type {0} has been created.",
                        OutputFileType.TrajectoryData));
            }

            // Loop through evaluation units and through each trajectory unit and write each point on the trajectory
            foreach (MazeNavigatorEvaluationUnit evaluationUnit in evaluationUnits.Where(eu => eu.IsMazeSolved))
            {
                for (int idx = 0; idx < evaluationUnit.AgentTrajectory.Count(); idx += 2)
                {
                    FileWriters[OutputFileType.TrajectoryData].WriteLine(string.Join(FileDelimiter,
                        new List<string>
                        {
                            experimentId.ToString(),
                            run.ToString(),
                            batch.ToString(),
                            ((idx/2) + 1).ToString(), // this is the timestep
                            evaluationUnit.MazeId.ToString(),
                            evaluationUnit.AgentId.ToString(),
                            evaluationUnit.AgentTrajectory[idx].ToString(CultureInfo.InvariantCulture),
                            evaluationUnit.AgentTrajectory[idx + 1].ToString(CultureInfo.InvariantCulture)
                        }));
                }
            }
        }

        /// <summary>
        ///     Writes the given trajectory diversity results to the experiment database.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="evaluationUnits">The evaluation results to persist.</param>
        /// <param name="commitPageSize">The number of records that are committed within a single batch/context.</param>
        private static void WriteTrajectoryDataToDatabase(int experimentId, int run, int batch,
            IList<MazeNavigatorEvaluationUnit> evaluationUnits, int commitPageSize)
        {
            // Page through the result set, committing each in the specified batch size
            for (int curPage = 0; curPage <= evaluationUnits.Count/commitPageSize; curPage++)
            {
                IList<CoevolutionMCSFullTrajectory> serializedResults =
                    new List<CoevolutionMCSFullTrajectory>(commitPageSize);

                // Build a list of serialized results
                foreach (
                    MazeNavigatorEvaluationUnit evaluationUnit in
                        evaluationUnits.Skip(curPage*commitPageSize).Take(commitPageSize))
                {
                    for (int idx = 0; idx < evaluationUnit.AgentTrajectory.Count(); idx += 2)
                    {
                        serializedResults.Add(new CoevolutionMCSFullTrajectory
                        {
                            ExperimentDictionaryID = experimentId,
                            Run = run,
                            Generation = batch,
                            Timestep = ((idx/2) + 1), // this is the timestep
                            MazeGenomeID = evaluationUnit.MazeId,
                            NavigatorGenomeID = evaluationUnit.AgentId,
                            XPosition = Convert.ToDecimal(evaluationUnit.AgentTrajectory[idx]),
                            YPosition = Convert.ToDecimal(evaluationUnit.AgentTrajectory[idx + 1])
                        });
                    }
                }

                // Create a new context and persist the batch
                using (ExperimentDataEntities context = new ExperimentDataEntities())
                {
                    // Auto-detect changes and save validation are switched off to speed things up
                    context.Configuration.AutoDetectChangesEnabled = false;
                    context.Configuration.ValidateOnSaveEnabled = false;

                    context.CoevolutionMCSFullTrajectories.AddRange(serializedResults);
                    context.SaveChanges();
                }
            }
        }

        /// <summary>
        ///     Writes the given trajectory diversity results to a flat file.
        /// </summary>
        /// <param name="experimentId">The experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="trajectoryDiversityUnits">The trajectory diversity data to persist.</param>
        private static void WriteTrajectoryDiversityDataToFile(int experimentId, int run, int batch,
            IList<TrajectoryDiversityUnit> trajectoryDiversityUnits)
        {
            // Make sure the file writer actually exists before attempting to write to it
            if (FileWriters.ContainsKey(OutputFileType.TrajectoryDiversityData) == false)
            {
                throw new Exception(
                    string.Format("Cannot write to output stream as no file writer of type {0} has been created.",
                        OutputFileType.TrajectoryDiversityData));
            }

            // Loop through the trajectory diversity units and write each row
            foreach (TrajectoryDiversityUnit diversityUnit in trajectoryDiversityUnits)
            {
                FileWriters[OutputFileType.TrajectoryDiversityData].WriteLine(string.Join(FileDelimiter,
                    new List<string>
                    {
                        experimentId.ToString(),
                        run.ToString(),
                        batch.ToString(),
                        diversityUnit.MazeId.ToString(),
                        diversityUnit.AgentId.ToString(),
                        diversityUnit.IntraMazeDiversityScore.ToString(CultureInfo.InvariantCulture),
                        diversityUnit.InterMazeDiversityScore.ToString(CultureInfo.InvariantCulture),
                        diversityUnit.GlobalDiversityScore.ToString(CultureInfo.InvariantCulture)
                    }));
            }

            // Immediately flush to the output file
            FileWriters[OutputFileType.TrajectoryDiversityData].Flush();
        }

        /// <summary>
        ///     Writes the coevolution vs. novelty search comparison results to a flat file.
        /// </summary>
        /// <param name="coEvoExperimentId">The coevolution experiment that was executed.</param>
        /// <param name="nsExperimentId">The novelty search experiment that was executed.</param>
        /// <param name="run">The run number of the given experiment.</param>
        /// <param name="batch">The batch number of the given run.</param>
        /// <param name="mazeGenomeId">
        ///     The unique identifier of the maze domain on which the novelty search comparison was
        ///     executed.
        /// </param>
        /// <param name="mazeBirthBatch">The birth batch (generation) of the maze.</param>
        /// <param name="nsAgentMinComplexity">The minimum complexity of the novelty search population at the end of the run.</param>
        /// <param name="nsAgentMaxComplexity">The maximum complexity of the novelty search population at the end of the run.</param>
        /// <param name="nsAgentMeanComplexity">The mean complexity of the novelty search population at the end of the run.</param>
        /// <param name="coEvoEvaluations">
        ///     The number of evaluations executed by the coevolution algorithm in order to arrive at
        ///     the given maze structure.
        /// </param>
        /// <param name="nsEvaluations">
        ///     The total number of evaluations executed by the novelty search algorithm in order to solve
        ///     (or attempt to solve) the coevolution-discovered maze structure.
        /// </param>
        /// <param name="isSolved">Flag indicating whether novelty search was successful in solving the maze.</param>
        private static void WriteNoveltySearchComparisonResultsToFile(int coEvoExperimentId, int nsExperimentId, int run,
            int batch, int mazeGenomeId, int mazeBirthBatch, int nsAgentMinComplexity, int nsAgentMaxComplexity,
            double nsAgentMeanComplexity, int coEvoEvaluations, int nsEvaluations, bool isSolved)
        {
            // Make sure the file writer actually exists before attempting to write to it
            if (FileWriters.ContainsKey(OutputFileType.NoveltySearchComparisonData) == false)
            {
                throw new Exception(
                    string.Format("Cannot write to output stream as no file writer of type {0} has been created.",
                        OutputFileType.NoveltySearchComparisonData));
            }

            // Write comparison results row to flat file with the specified delimiter
            FileWriters[OutputFileType.NoveltySearchComparisonData].WriteLine(string.Join(FileDelimiter,
                new List<string>
                {
                    coEvoExperimentId.ToString(),
                    nsExperimentId.ToString(),
                    run.ToString(),
                    batch.ToString(),
                    mazeGenomeId.ToString(),
                    mazeBirthBatch.ToString(),
                    nsAgentMinComplexity.ToString(),
                    nsAgentMaxComplexity.ToString(),
                    nsAgentMeanComplexity.ToString(CultureInfo.InvariantCulture),
                    coEvoEvaluations.ToString(),
                    nsEvaluations.ToString(),
                    isSolved.ToString()
                }));

            // Immediately flush to the output file
            FileWriters[OutputFileType.NoveltySearchComparisonData].Flush();
        }

        #endregion
    }
}