﻿#region

using System;
using System.Collections.Generic;
using SharpNeat.Core;

#endregion

namespace SharpNeat.MinimalCriterias
{
    /// <summary>
    ///     Defines the calculations for determining whether a given behavior satisfies the mileage criteria.
    /// </summary>
    public class MileageCriteria : IMinimalCriteria
    {
        /// <summary>
        ///     Hard-coded number of dimensions in euclidean space.
        /// </summary>
        private const int EuclideanDimensions = 2;

        /// <summary>
        ///     The minimum mileage that the candidate agent had to travel to be considered viable.
        /// </summary>
        private readonly double _minimumMileage;

        /// <summary>
        ///     The x-component of the starting position.
        /// </summary>
        private readonly double _startXPosition;

        /// <summary>
        ///     The y-component of the starting position;
        /// </summary>
        private readonly double _startYPosition;

        /// <summary>
        ///     Constructor for the mileage minimal criteria.
        /// </summary>
        /// <param name="startingXLocation">The x-component of the starting position.</param>
        /// <param name="startingYLocation">The y-component of the starting position.</param>
        /// <param name="minimumMileage">The minimum mileage that an agent has to travel.</param>
        public MileageCriteria(double startingXLocation, double startingYLocation, double minimumMileage)
        {
            _startXPosition = startingXLocation;
            _startYPosition = startingYLocation;
            _minimumMileage = minimumMileage;
        }

        /// <summary>
        ///     Updates the minimal criteria based on characteristics of the current population.
        /// </summary>
        /// <typeparam name="TGenome">Genome type parameter.</typeparam>
        /// <param name="population">The current population.</param>
        public void UpdateMinimalCriteria<TGenome>(List<TGenome> population)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Evaluate whether the given behavior characterization satisfies the minimal criteria based on the mileage (computed
        ///     as the distance transited between any two consecutive timesteps).
        /// </summary>
        /// <param name="behaviorInfo">The behavior info indicating the full trajectory of the agent.</param>
        /// <returns>Boolean value indicating whether the given behavior characterization satisfies the minimal criteria.</returns>
        public bool DoesCharacterizationSatisfyMinimalCriteria(BehaviorInfo behaviorInfo)
        {
            // If the behavior dimensionality doesn't align with the specified dimensionality, we can't compare it
            if (behaviorInfo.Behaviors.Length%EuclideanDimensions != 0)
            {
                throw new SharpNeatException(
                    "Cannot evaluate minimal criteria constraints because the behavior characterization is not of the correct dimensionality.");
            }

            double mileage = 0;

            for (int curPosition = 0;
                curPosition < behaviorInfo.Behaviors.Length/EuclideanDimensions;
                curPosition += EuclideanDimensions)
            {
                // Extract x and y components of location
                double curXPosition = behaviorInfo.Behaviors[curPosition];
                double curYPosition = behaviorInfo.Behaviors[curPosition + 1];

                // If this is the first behavior, calculate euclidean distance between the ending position
                // after the first timestep and the starting position
                if (curPosition < EuclideanDimensions)
                {
                    mileage +=
                        Math.Sqrt(Math.Pow(curXPosition - _startXPosition, 2) +
                                  Math.Pow(curYPosition - _startYPosition, 2));
                }

                // Otherwise, calculate the euclidean distance between the ending position at this timestep
                // and the ending position from the previous timestep
                else
                {
                    double prevXPosition = behaviorInfo.Behaviors[curPosition - EuclideanDimensions];
                    double prevYPosition = behaviorInfo.Behaviors[curPosition - EuclideanDimensions + 1];

                    mileage +=
                        Math.Sqrt(Math.Pow(curXPosition - prevXPosition, 2) + Math.Pow(curYPosition - prevYPosition, 2));
                }
            }

            // Only return true if the mileage is at least the minimum required mileage
            return mileage > _minimumMileage;
        }
    }
}