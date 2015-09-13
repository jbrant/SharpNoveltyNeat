﻿#region

using System.Collections.Generic;
using SharpNeat.Core;

#endregion

namespace SharpNeat.Utility
{
    /// <summary>
    ///     Encapsulates utility methods for evaluating the fitness and behavior of genomes.  This essentially captures logic
    ///     that is common between varying implementations of IGenomeEvaluator.
    /// </summary>
    /// <typeparam name="TGenome">The genome type under evaluation.</typeparam>
    /// <typeparam name="TPhenome">The phenotype corresponding to that genome.</typeparam>
    public static class EvaluationUtils<TGenome, TPhenome>
        where TGenome : class, IGenome<TGenome>
        where TPhenome : class
    {
        /// <summary>
        ///     Evalutes the behavior and resulting "fitness" (behavioral novelty) of a given genome.  The phenome is decoded on
        ///     every run instead of caching it.
        /// </summary>
        /// <param name="genome">The genome under evaluation.</param>
        /// <param name="genomeDecoder">The decoder for decoding the genotype to its phenotypic representation.</param>
        /// <param name="phenomeEvaluator">The phenome evaluator.</param>
        public static void EvaluateBehavior_NonCaching(TGenome genome, IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
            IPhenomeEvaluator<TPhenome, BehaviorInfo> phenomeEvaluator)
        {
            var phenome = genomeDecoder.Decode(genome);
            if (null == phenome)
            {
                // Non-viable genome.
                genome.EvaluationInfo.SetFitness(0.0);
                genome.EvaluationInfo.AuxFitnessArr = null;
                genome.EvaluationInfo.BehaviorCharacterization = new double[0];
            }
            else
            {
                // EvaluateFitness the behavior and update the genome's behavior characterization
                var behaviorInfo = phenomeEvaluator.Evaluate(phenome);
                genome.EvaluationInfo.BehaviorCharacterization = behaviorInfo.Behaviors;
            }
        }

        /// <summary>
        ///     Evalutes the behavior and resulting "fitness" (behavioral novelty) of a given genome.  We first try to retrieve the
        ///     phenome from the genomes cache and then decode the genome if it has not yet been cached.
        /// </summary>
        /// <param name="genome">The genome under evaluation.</param>
        /// <param name="genomeDecoder">The decoder for decoding the genotype to its phenotypic representation.</param>
        /// <param name="phenomeEvaluator">The phenome evaluator.</param>
        public static void EvaluateBehavior_Caching(TGenome genome, IGenomeDecoder<TGenome, TPhenome> genomeDecoder,
            IPhenomeEvaluator<TPhenome, BehaviorInfo> phenomeEvaluator)
        {
            var phenome = (TPhenome) genome.CachedPhenome;
            if (null == phenome)
            {
                // Decode the phenome and store a ref against the genome.
                phenome = genomeDecoder.Decode(genome);
                genome.CachedPhenome = phenome;
            }

            if (null == phenome)
            {
                // Non-viable genome.
                genome.EvaluationInfo.SetFitness(0.0);
                genome.EvaluationInfo.AuxFitnessArr = null;
                genome.EvaluationInfo.BehaviorCharacterization = new double[0];
            }
            else
            {
                // EvaluateFitness the behavior and update the genome's behavior characterization
                var behaviorInfo = phenomeEvaluator.Evaluate(phenome);
                genome.EvaluationInfo.BehaviorCharacterization = behaviorInfo.Behaviors;
            }
        }

        /// <summary>
        ///     Evalutes the fitness of the given genome as the its behavioral novelty as compared to the rest of the population.
        /// </summary>
        /// <param name="genome">The genome to evaluate.</param>
        /// <param name="genomeList">The population against which the genome is being evaluated.</param>
        /// <param name="nearestNeighbors">The number of nearest neighbors in behavior space against which to evaluate.</param>
        /// <param name="noveltyArchive">The archive of novel individuals.</param>
        public static void EvaluateFitness(TGenome genome, IList<TGenome> genomeList, int nearestNeighbors,
            INoveltyArchive<TGenome> noveltyArchive)
        {
            // Compare the current genome's behavior to its k-nearest neighbors in behavior space
            var fitness =
                BehaviorUtils<TGenome>.CalculateBehavioralDistance(genome.EvaluationInfo.BehaviorCharacterization,
                    genomeList, nearestNeighbors, noveltyArchive);

            // Update the fitness as the behavioral novelty
            var fitnessInfo = new FitnessInfo(fitness, fitness);
            genome.EvaluationInfo.SetFitness(fitnessInfo._fitness);
            genome.EvaluationInfo.AuxFitnessArr = fitnessInfo._auxFitnessArr;
        }
    }
}