﻿#region

using System;
using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.Loggers;

#endregion

namespace SharpNeat.MinimalCriterias
{
    /// <summary>
    ///     Defines the minimal criteria in terms of location in euclidean space.
    /// </summary>
    public class EuclideanLocationCriteria : IMinimalCriteria
    {
        /// <summary>
        ///     Hard-coded number of dimensions in euclidean space.
        /// </summary>
        private const int EuclideanDimensions = 2;

        /// <summary>
        ///     The maximum x-component of location.
        /// </summary>
        private readonly double _xMax;

        /// <summary>
        ///     The minimum x-component of location.
        /// </summary>
        private readonly double _xMin;

        /// <summary>
        ///     The maximum y-component of location.
        /// </summary>
        private readonly double _yMax;

        /// <summary>
        ///     The minimum y-component of location.
        /// </summary>
        private readonly double _yMin;

        /// <summary>
        ///     Constructor for the euclidean minimal criteria.
        /// </summary>
        /// <param name="xMin">The minimum x-component of location.</param>
        /// <param name="xMax">The maximum x-component of location.</param>
        /// <param name="yMin">The minimum y-component of location.</param>
        /// <param name="yMax">The maximum y-component of location.</param>
        public EuclideanLocationCriteria(double xMin, double xMax, double yMin, double yMax)
        {
            _xMin = xMin;
            _xMax = xMax;
            _yMin = yMin;
            _yMax = yMax;
        }

        /// <summary>
        ///     Updates the minimal criteria based on characteristics of the current population.
        /// </summary>
        /// <typeparam name="TGenome">Genome type parameter.</typeparam>
        /// <param name="population">The current population.</param>
        public void UpdateMinimalCriteria<TGenome>(List<TGenome> population) where TGenome : class, IGenome<TGenome>
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Evalutes whether the given (presumably euclidean) behavior characterization satisfies the minimal criteria.
        /// </summary>
        /// <param name="behaviorInfo">The behavior info in euclidean space.</param>
        /// <param name="allowCriteriaReversal">
        ///     Permits reversing the minimal criteria (such that only those who do *not* meet the
        ///     minimal criteria are valid).
        /// </param>
        /// <returns>Boolean value indicating whether the given behavior characterization satisfies the minimal criteria.</returns>
        public bool DoesCharacterizationSatisfyMinimalCriteria(BehaviorInfo behaviorInfo)
        {
            // If the behavior dimensionality doesn't match, we can't compare it
            if (behaviorInfo.Behaviors.Length != EuclideanDimensions)
            {
                throw new SharpNeatException(
                    "Cannot evaluate minimal criteria constraints because the behavior characterization is not of the correct dimensionality.");
            }

            // Extract x and y components of location
            var xLocation = behaviorInfo.Behaviors[0];
            var yLocation = behaviorInfo.Behaviors[1];

            // Return false if the location falls outside of the bounds of the min/max x and y locations
            return !(xLocation < _xMin) && !(xLocation > _xMax) && !(yLocation < _yMin) && !(yLocation > _yMax);
        }

        /// <summary>
        ///     Returns EuclideanLocationCriteria loggable elements.
        /// </summary>
        /// <param name="logFieldEnableMap">
        ///     Dictionary of logging fields that can be enabled or disabled based on the specification
        ///     of the calling routine.
        /// </param>
        /// <returns>The loggable elements for EuclideanLocationCriteria.</returns>
        public List<LoggableElement> GetLoggableElements(IDictionary<FieldElement, bool> logFieldEnableMap = null)
        {
            throw new NotImplementedException();
        }
    }
}