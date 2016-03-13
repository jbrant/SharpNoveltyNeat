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

using System;
using System.Collections.Generic;

#endregion

namespace SharpNeat.Core
{
    /// <summary>
    ///     A generic interface for evolution algorithm classes.
    /// </summary>
    public interface ICoevolutionAlgorithmContainer<TGenome1, TGenome2>
        where TGenome1 : class, IGenome<TGenome1>
        where TGenome2 : class, IGenome<TGenome2>
    {
        /// <summary>
        ///     Gets the current generation.
        /// </summary>
        uint CurrentGeneration { get; }

        /// <summary>
        ///     Gets the current number of evaluations executed.
        /// </summary>
        ulong CurrentEvaluations { get; }

        /// <summary>
        ///     Gets or sets the algorithm's update scheme.
        /// </summary>
        UpdateScheme UpdateScheme { get; set; }

        /// <summary>
        ///     Gets the current execution/run state of the IEvolutionAlgorithm.
        /// </summary>
        RunState RunState { get; }
        
        /// <summary>
        ///     Gets a value indicating whether some goal fitness has been achieved and that the algorithm has therefore stopped.
        /// </summary>
        bool StopConditionSatisfied { get; }

        /// <summary>
        ///     Notifies listeners that some state change has occured.
        /// </summary>
        event EventHandler UpdateEvent;

        /// <summary>
        ///     Notifies listeners that the algorithm has paused.
        /// </summary>
        event EventHandler PausedEvent;

        /// <summary>
        ///     Initializes the evolution algorithms with the provided IGenomeFitnessEvaluator, IGenomeFactory
        ///     and an initial population of genomes for both populations.
        /// </summary>
        void Initialize(IGenomeEvaluator<TGenome1> genomeFitnessEvaluator1,
            IGenomeFactory<TGenome1> genomeFactory1,
            List<TGenome1> genomeList1,
            IGenomeEvaluator<TGenome2> genomeFitnessEvaluator2,
            IGenomeFactory<TGenome2> genomeFactory2,
            List<TGenome2> genomeList2,
            int? maxGenerations,
            ulong? maxEvaluations);

        /// <summary>
        ///     Initializes the evolution algorithms with the provided IGenomeFitnessEvaluator
        ///     and an IGenomeFactory that can be used to create two initial populations of genomes.
        /// </summary>
        void Initialize(IGenomeEvaluator<TGenome1> genomeFitnessEvaluator1,
            IGenomeFactory<TGenome1> genomeFactory1,
            int populationSize1,
            IGenomeEvaluator<TGenome2> genomeFitnessEvaluator2,
            IGenomeFactory<TGenome2> genomeFactory2,
            int populationSize2,
            int? maxGenerations,
            ulong? maxEvaluations);

        /// <summary>
        ///     Starts the algorithm running. The algorithm will switch to the Running state from either
        ///     the Ready or Paused states.
        /// </summary>
        void StartContinue();

        /// <summary>
        ///     Resets the internal thread and other state of the evolution algorithm when such is
        ///     requested from the GUI.
        /// </summary>
        void Reset();

        /// <summary>
        ///     Requests that the algorithm pauses but doesn't wait for the algorithm thread to stop.
        ///     The algorithm thread will pause when it is next convenient to do so, and notifies
        ///     listeners via an UpdateEvent.
        /// </summary>
        void RequestPause();

        /// <summary>
        ///     Request that the algorithm pause and waits for the algorithm to do so. The algorithm
        ///     thread will pause when it is next convenient to do so and notifies any UpdateEvent
        ///     listeners prior to returning control to the caller. Therefore it's generally a bad idea
        ///     to call this method from a GUI thread that also has code that may be called by the
        ///     UpdateEvent - doing so will result in deadlocked threads.
        /// </summary>
        void RequestPauseAndWait();
    }
}