/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2006, 2009-2010 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SharpNEAT is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with SharpNEAT.  If not, see <http://www.gnu.org/licenses/>.
 */

#region

using System.Collections.Generic;

#endregion

namespace SharpNeat.Core
{
    /// <summary>
    ///     Generic interface for phenome evaluation classes.
    ///     Evaluates and assigns a fitness to individual TPhenome's.
    /// </summary>
    public interface IPhenomeEvaluator<in TPhenome, out TTrialInfo>
    {
        /// <summary>
        ///     Gets the total number of individual genome evaluations that have been performed by this evaluator.
        /// </summary>
        ulong EvaluationCount { get; }

        /// <summary>
        ///     Gets a value indicating whether some goal fitness has been achieved and that
        ///     the the evolutionary algorithm search should stop. This property's value can remain false
        ///     to allow the algorithm to run indefinitely.
        /// </summary>
        bool StopConditionSatisfied { get; }

        /// <summary>
        ///     EvaluateFitness the provided phenome and return its fitness score.
        /// </summary>
        TTrialInfo Evaluate(TPhenome phenome, uint currentGeneration, bool isBridgingEvaluation,
            IDataLogger evaluationLogger, string genomeXml);

        /// <summary>
        ///     Initializes state variables in the phenome evalutor.
        /// </summary>
        void Initialize(IDataLogger evaluationLogger);

        /// <summary>
        ///     Update the evaluator based on some characteristic of the given population.
        /// </summary>
        /// <typeparam name="TGenome">The genome type parameter.</typeparam>
        /// <param name="population">The current population.</param>
        void Update<TGenome>(List<TGenome> population);

        /// <summary>
        ///     Reset the internal state of the evaluation scheme if any exists.
        /// </summary>
        void Reset();
    }
}