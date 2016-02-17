﻿#region

using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.Loggers;
using SharpNeat.Phenomes;

#endregion

namespace SharpNeat.Domains.MazeEnvironmentTest
{
    public class MazeEnvironmentTestEvaluator : IPhenomeEvaluator<IBlackBox, BehaviorInfo>
    {
        /// <summary>
        ///     Lock object for synchronizing evaluation counter increments.
        /// </summary>
        private readonly object _evaluationLock = new object();

        private readonly int _xStart, _xEnd, _yStart, _yEnd;
        private readonly double _pixelExpressionThreshold;

        public List<LoggableElement> GetLoggableElements(IDictionary<FieldElement, bool> logFieldEnableMap = null)
        {
            return null;
        }

        public ulong EvaluationCount { get; private set; }
        public bool StopConditionSatisfied { get; private set; }

        public BehaviorInfo Evaluate(IBlackBox phenome, uint currentGeneration, bool isBridgingEvaluation,
            IDataLogger evaluationLogger,
            string genomeXml)
        {
            ulong threadLocalEvaluationCount = default(ulong);

            lock (_evaluationLock)
            {
                // Increment evaluation count
                threadLocalEvaluationCount = EvaluationCount++;
            }

            // Reset the overall network state
            phenome.ResetState();

            for (int x = _xStart; x <= _xEnd; x++)
            {
                for (int y = _yStart; y <= _yEnd; y++)
                {
                    // Reset the input array
                    phenome.InputSignalArray.Reset();

                    // TODO: These inputs probably need to be normalized
                    // Load up the inputs with the coordinate
                    phenome.InputSignalArray[0] = x;
                    phenome.InputSignalArray[1] = y;

                    // Activate the network
                    phenome.Activate();

                    // Translate and apply the output (which will be the pixel intensity)
                    double curOutput = phenome.OutputSignalArray[0];

                    // TODO: Threshold the output such that the pixel is expressed if it exceeds a certain value
                }
            }

            // TODO: Load up the inputs with the coordinates of each position in the maze substrate and activate

            return null;
        }

        public void Initialize(IDataLogger evaluationLogger)
        {
            // Set the run phase
            evaluationLogger?.UpdateRunPhase(RunPhase.Primary);

            // Log the header
            evaluationLogger?.LogHeader(new List<LoggableElement>
            {
                new LoggableElement(EvaluationFieldElements.Generation, 0),
                new LoggableElement(EvaluationFieldElements.EvaluationCount, EvaluationCount),
                new LoggableElement(EvaluationFieldElements.StopConditionSatisfied, StopConditionSatisfied),
                new LoggableElement(EvaluationFieldElements.RunPhase, RunPhase.Initialization),
                new LoggableElement(EvaluationFieldElements.IsViable, false)
            });
        }

        public void Update<TGenome>(List<TGenome> population) where TGenome : class, IGenome<TGenome>
        {
        }

        public void Reset()
        {
        }
    }
}