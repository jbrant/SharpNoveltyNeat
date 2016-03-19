﻿#region

using System;
using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.Loggers;
using SharpNeat.Phenomes;
using SharpNeat.Phenomes.Mazes;

#endregion

namespace SharpNeat.Domains.MazeNavigation.CoevolutionMCSExperiment
{
    public class MazeEnvironmentMCSEvaluator : IPhenomeEvaluator<MazeStructure, BehaviorInfo>
    {
        #region Constructors

        /// <summary>
        ///     Maze Environment MCS evaluator constructor.
        /// </summary>
        /// <param name="maxTimesteps">The maximum number of time steps in a single simulation.</param>
        /// <param name="minSuccessDistance">The minimum distance from the target to be considered a successful run.</param>
        /// <param name="behaviorCharacterizationFactory">The initialized behavior characterization factory.</param>
        /// <param name="numAgentsSolvedCriteria">
        ///     The number of successful attempts at maze navigation in order to satisfy the
        ///     minimal criterion.
        /// </param>
        /// <param name="numAgentsFailedCriteria">
        ///     The number of failed attempts at maze navigation in order to satisfy the minimal
        ///     criterion.
        /// </param>
        public MazeEnvironmentMCSEvaluator(int maxTimesteps, int minSuccessDistance,
            IBehaviorCharacterizationFactory behaviorCharacterizationFactory, int numAgentsSolvedCriteria,
            int numAgentsFailedCriteria)
        {
            _behaviorCharacterizationFactory = behaviorCharacterizationFactory;
            _numAgentsSolvedCriteria = numAgentsSolvedCriteria;
            _numAgentsFailedCriteria = numAgentsFailedCriteria;

            // Create factory for maze world generation
            _multiMazeWorldFactory = new MultiMazeNavigationWorldFactory<BehaviorInfo>(maxTimesteps, minSuccessDistance);
        }

        #endregion

        #region Private Members

        /// <summary>
        ///     The behavior characterization factory.
        /// </summary>
        private readonly IBehaviorCharacterizationFactory _behaviorCharacterizationFactory;

        /// <summary>
        ///     Lock object for synchronizing evaluation counter increments.
        /// </summary>
        private readonly object _evaluationLock = new object();

        /// <summary>
        ///     The multi maze navigation world factory.
        /// </summary>
        private readonly MultiMazeNavigationWorldFactory<BehaviorInfo> _multiMazeWorldFactory;

        /// <summary>
        ///     The list of of maze navigators against which to evaluate the given maze configurations.
        /// </summary>
        private IList<IBlackBox> _mazeNavigators;

        /// <summary>
        ///     The number of navigation attempts that must succeed for meeting the minimal criteria.
        /// </summary>
        private readonly int _numAgentsSolvedCriteria;

        /// <summary>
        ///     The number of navigation attempts that must fail for meeting the minimal criteria.
        /// </summary>
        private readonly int _numAgentsFailedCriteria;

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the total number of evaluations that have been performed.
        /// </summary>
        public ulong EvaluationCount { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether some goal fitness has been achieved and that the evolutionary algorithm/search
        ///     should stop.  This property's value can remain false to allow the algorithm to run indefinitely.
        /// </summary>
        public bool StopConditionSatisfied { get; private set; }

        #endregion

        #region Public Methods

        public BehaviorInfo Evaluate(MazeStructure mazeStructure, uint currentGeneration, bool isBridgingEvaluation,
            IDataLogger evaluationLogger, string genomeXml)
        {
            ulong threadLocalEvaluationCount = default(ulong);
            int curSuccesses = 0;
            int curFailures = 0;

            // TODO: Note that this will get overwritten until the last successful attempt (may need a better way of handling this for logging purposes)
            BehaviorInfo trialInfo = BehaviorInfo.NoBehavior;

            for (int cnt = 0;
                cnt < _mazeNavigators.Count &&
                (curSuccesses < _numAgentsSolvedCriteria || curFailures < _numAgentsFailedCriteria);
                cnt++)
            {
                lock (_evaluationLock)
                {
                    // Increment evaluation count
                    threadLocalEvaluationCount = EvaluationCount++;
                }

                // Default the stop condition satisfied to false
                bool goalReached = false;

                // Generate new behavior characterization
                IBehaviorCharacterization behaviorCharacterization =
                    _behaviorCharacterizationFactory.CreateBehaviorCharacterization();

                // Generate a new maze world
                MazeNavigationWorld<BehaviorInfo> world = _multiMazeWorldFactory.CreateMazeNavigationWorld(
                    mazeStructure,
                    behaviorCharacterization);

                // Run a single trial
                trialInfo = world.RunTrial(_mazeNavigators[cnt], SearchType.MinimalCriteriaSearch, out goalReached);

                // Set the objective distance
                trialInfo.ObjectiveDistance = world.GetDistanceToTarget();

                // Log trial information
                evaluationLogger?.LogRow(new List<LoggableElement>
                {
                    new LoggableElement(EvaluationFieldElements.Generation, currentGeneration),
                    new LoggableElement(EvaluationFieldElements.EvaluationCount, threadLocalEvaluationCount),
                    new LoggableElement(EvaluationFieldElements.StopConditionSatisfied, StopConditionSatisfied),
                    new LoggableElement(EvaluationFieldElements.RunPhase, RunPhase.Primary),
                    new LoggableElement(EvaluationFieldElements.IsViable, trialInfo.DoesBehaviorSatisfyMinimalCriteria)
                },
                    world.GetLoggableElements());

                // If the navigator reached the goal, increment the running count of successes
                if (goalReached)
                    curSuccesses++;
                // Otherwise, increment the number of failures
                else
                    curFailures++;
            }

            // If the number of successful maze navigations and failed maze navigations were both equivalent to their
            // respective minimums, then the minimal criteria has been satisfied
            if (curSuccesses >= _numAgentsSolvedCriteria && curFailures >= _numAgentsFailedCriteria)
            {
                trialInfo.DoesBehaviorSatisfyMinimalCriteria = true;
            }

            return trialInfo;
        }

        public void Initialize(IDataLogger evaluationLogger)
        {
            throw new NotImplementedException();
        }

        public void Update<TGenome>(List<TGenome> population) where TGenome : class, IGenome<TGenome>
        {
            throw new NotImplementedException();
        }

        public void UpdateEvaluatorPhenotypes(IEnumerable<object> evaluatorPhenomes)
        {
            _mazeNavigators = (IList<IBlackBox>) evaluatorPhenomes;
        }

        public void Reset()
        {
        }

        public List<LoggableElement> GetLoggableElements(IDictionary<FieldElement, bool> logFieldEnableMap = null)
        {
            return _behaviorCharacterizationFactory.GetLoggableElements(logFieldEnableMap);
        }

        #endregion
    }
}