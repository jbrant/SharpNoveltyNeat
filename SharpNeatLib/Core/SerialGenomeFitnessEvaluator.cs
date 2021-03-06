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
using SharpNeat.Loggers;
using SharpNeat.Utility;

#endregion

namespace SharpNeat.Core
{
    /// <summary>
    ///     A concrete implementation of IGenomeFitnessEvaluator that evaulates genomes independently of each other
    ///     and in series on a single thread.
    ///     Genome decoding is performed by a provided IGenomeDecoder.
    ///     Phenome evaluation is performed by a provided IPhenomeEvaluator.
    ///     This class evaluates on a single thread only, and therefore is a good choice when debugging code.
    /// </summary>
    /// <typeparam name="TGenome">The genome type that is decoded.</typeparam>
    /// <typeparam name="TPhenome">The phenome type that is decoded to and then evaluated.</typeparam>
    public class SerialGenomeFitnessEvaluator<TGenome, TPhenome> : IGenomeEvaluator<TGenome>
        where TGenome : class, IGenome<TGenome>
        where TPhenome : class
    {
        #region Evaluation delegates

        /// <summary>
        ///     The delegate for population evaluation.
        /// </summary>
        /// <param name="genomeList">The list of genomes (population) to evaluate.</param>
        /// <param name="currentGeneration">The current generation for which the genomes are being evaluated.</param>
        private delegate void EvaluationMethod(IList<TGenome> genomeList, uint currentGeneration);

        #endregion

        #region Private Instance fields

        private readonly EvaluationMethod _evaluationMethod;
        private readonly IGenomeDecoder<TGenome, TPhenome> _genomeDecoder;
        private readonly IPhenomeEvaluator<TPhenome, FitnessInfo> _phenomeEvaluator;
        private readonly bool _enablePhenomeCaching;

        #endregion

        #region Constructor

        /// <summary>
        ///     Construct with the provided IGenomeDecoder and IPhenomeEvaluator.
        ///     Phenome caching is enabled by default.
        /// </summary>
        public SerialGenomeFitnessEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
            IPhenomeEvaluator<TPhenome, FitnessInfo> phenomeEvaluator, IDataLogger evaluationLogger = null)
        {
            _genomeDecoder = genomeDecoder;
            _phenomeEvaluator = phenomeEvaluator;
            _enablePhenomeCaching = true;
            _evaluationMethod = Evaluate_Caching;
        }

        /// <summary>
        ///     Construct with the provided IGenomeDecoder, IPhenomeEvaluator and enablePhenomeCaching flag.
        /// </summary>
        public SerialGenomeFitnessEvaluator(IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
            IPhenomeEvaluator<TPhenome, FitnessInfo> phenomeEvaluator,
            bool enablePhenomeCaching, IDataLogger evaluationLogger = null)
        {
            _genomeDecoder = genomeDecoder;
            _phenomeEvaluator = phenomeEvaluator;
            _enablePhenomeCaching = enablePhenomeCaching;

            if (_enablePhenomeCaching)
            {
                _evaluationMethod = Evaluate_Caching;
            }
            else
            {
                _evaluationMethod = Evaluate_NonCaching;
            }
        }

        #endregion

        #region IGenomeFitnessEvaluator<TGenome> Members

        /// <summary>
        ///     Gets the total number of individual genome evaluations that have been performed by this evaluator.
        /// </summary>
        public ulong EvaluationCount => _phenomeEvaluator.EvaluationCount;

        /// <summary>
        ///     Gets a value indicating whether some goal fitness has been achieved and that
        ///     the the evolutionary algorithm/search should stop. This property's value can remain false
        ///     to allow the algorithm to run indefinitely.
        /// </summary>
        public bool StopConditionSatisfied => _phenomeEvaluator.StopConditionSatisfied;

        /// <summary>
        ///     Initializes state variables in the genome evaluator.
        /// </summary>
        public void Initialize()
        {
            _phenomeEvaluator.Initialize();
        }

        /// <summary>
        ///     Calls child classes to clean up or dispose of variable states or close out loggers.
        /// </summary>
        public void Cleanup()
        {
            _phenomeEvaluator.Cleanup();
        }

        /// <summary>
        ///     Updates the environment or other evaluation criteria against which the evaluated genomes are being compared.  This
        ///     is typically used in a coevoluationary context.
        /// </summary>
        /// <param name="comparisonPhenomes">The phenomes against which the evaluation is being carried out.</param>
        /// <param name="lastGeneration">The generation that was just executed.</param>
        public void UpdateEvaluationBaseline(IEnumerable<object> comparisonPhenomes, uint lastGeneration)
        {
            _phenomeEvaluator.UpdateEvaluatorPhenotypes(comparisonPhenomes, lastGeneration);
        }

        /// <summary>
        ///     Decodes a list of genomes to their corresponding phenotypes.
        /// </summary>
        /// <param name="genomeList">The list of genomes to decode.</param>
        /// <returns>The decoded phenomes.</returns>
        public IEnumerable<object> DecodeGenomes(IList<TGenome> genomeList)
        {
            return EvaluationUtils<TGenome, TPhenome>.DecodeGenomes(genomeList, _genomeDecoder);
        }

        /// <summary>
        ///     Evaluates a list of genomes. Here we decode each genome in series using the contained
        ///     IGenomeDecoder and evaluate the resulting TPhenome using the contained IPhenomeEvaluator.
        /// </summary>
        /// <param name="genomeList">The list of genomes under evaluation.</param>
        /// <param name="currentGeneration">The current generation for which the genomes are being evaluated.</param>
        /// <param name="runSimulation">
        ///     Determines whether to run the simulation to get behavioral characteristics before
        ///     evaluating fitness or behavioral novelty (this isn't applicable for pure fitness evaluation).
        /// </param>
        public void Evaluate(IList<TGenome> genomeList, uint currentGeneration, bool runSimulation = true)
        {
            _evaluationMethod(genomeList, currentGeneration);
        }

        /// <summary>
        ///     Evalutes a single genome alone and against a list of other genomes.
        /// </summary>
        /// <param name="genomesToEvaluate">The genomes under evaluation.</param>
        /// <param name="population">The genomes against which to evaluate.</param>
        /// <param name="currentGeneration">The current generation for which the genomes are being evaluated.</param>
        /// <param name="runSimulation">
        ///     Determines whether to run the simulation to get behavioral characteristics before
        ///     evaluating fitness or behavioral novelty (this isn't applicable for pure fitness evaluation).
        /// </param>
        public void Evaluate(IList<TGenome> genomesToEvaluate, IList<TGenome> population, uint currentGeneration,
            bool runSimulation = true)
        {
            // TODO: Need to implement this
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Reset the internal state of the evaluation scheme if any exists.
        /// </summary>
        public void Reset()
        {
            _phenomeEvaluator.Reset();
        }

        #endregion

        #region Private Methods

        private void Evaluate_NonCaching(IList<TGenome> genomeList, uint currentGeneration)
        {
            // Decode and evaluate each genome in turn.
            foreach (var genome in genomeList)
            {
                var phenome = _genomeDecoder.Decode(genome);
                if (null == phenome)
                {
                    // Non-viable genome.
                    genome.EvaluationInfo.SetFitness(0.0);
                    genome.EvaluationInfo.AuxFitnessArr = null;
                }
                else
                {
                    var fitnessInfo = _phenomeEvaluator.Evaluate(phenome, currentGeneration);
                    genome.EvaluationInfo.SetFitness(fitnessInfo.Fitness);
                    genome.EvaluationInfo.AuxFitnessArr = fitnessInfo.AuxFitnessArr;
                }
            }
        }

        private void Evaluate_Caching(IList<TGenome> genomeList, uint currentGeneration)
        {
            // Decode and evaluate each genome in turn.
            foreach (var genome in genomeList)
            {
                var phenome = (TPhenome) genome.CachedPhenome;
                if (null == phenome)
                {
                    // Decode the phenome and store a ref against the genome.
                    phenome = _genomeDecoder.Decode(genome);
                    genome.CachedPhenome = phenome;
                }

                if (null == phenome)
                {
                    // Non-viable genome.
                    genome.EvaluationInfo.SetFitness(0.0);
                    genome.EvaluationInfo.AuxFitnessArr = null;
                }
                else
                {
                    var fitnessInfo = _phenomeEvaluator.Evaluate(phenome, currentGeneration);
                    genome.EvaluationInfo.SetFitness(fitnessInfo.Fitness);
                    genome.EvaluationInfo.AuxFitnessArr = fitnessInfo.AuxFitnessArr;
                }
            }
        }

        #endregion
    }
}