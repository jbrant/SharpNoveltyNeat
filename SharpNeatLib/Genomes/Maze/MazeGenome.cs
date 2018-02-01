﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using SharpNeat.Core;
using SharpNeat.Utility;

#endregion

namespace SharpNeat.Genomes.Maze
{
    /// <summary>
    ///     The maze genome contains maze genes (which themselves encode the walls and their passages in the form of
    ///     real-valued numbers) and provides routines for mutating those genes and producing offspring.
    /// </summary>
    public class MazeGenome : IGenome<MazeGenome>
    {
        #region Instance Variables

        private CoordinateVector _position;

        #endregion

        #region Maze Properties

        /// <summary>
        ///     Reference to the maze genome factory (for the purposes of creating offspring).
        /// </summary>
        public MazeGenomeFactory GenomeFactory { get; set; }

        /// <summary>
        ///     The list of wall genes composing the genome (each gene encodes the un-normalized location of a wall in the maze and
        ///     its passage).
        /// </summary>
        public IList<WallGene> WallGeneList { get; }

        /// <summary>
        ///     The list of path genes composing the genome (each gene encodes a "waypoint" in the path and the orientation of its
        ///     intersection).
        /// </summary>
        public IList<PathGene> PathGeneList { get; }

        /// <summary>
        ///     Height of the evolved maze genome (before being scaled to phenotype).
        /// </summary>
        public int MazeBoundaryHeight { get; private set; }

        /// <summary>
        ///     Width of evolved maze genome (before being scaled to phenotype).
        /// </summary>
        public int MazeBoundaryWidth { get; private set; }

        /// <summary>
        ///     Relative height of each maze cell in the range 0 to 1.
        /// </summary>
        public double RelativeCellHeight { get; private set; }

        /// <summary>
        ///     Relative width of each maze cell in the range 0 to 1.
        /// </summary>
        public double RelativeCellWidth { get; private set; }

        /// <summary>
        ///     The maximum complexity of the maze (at the evolved resolution).  Note that this is set when the genome is birthed,
        ///     but can also change as a result of a mutation; however, it's stored on the genome instead of being calculated via
        ///     the "get" call because of the computational cost involved in calculating it.
        /// </summary>
        public int MaxComplexity { get; private set; }

        #endregion

        #region Maze Genome Constructors

        /// <summary>
        ///     Constructor which constructs a new maze genome with the given unique identifier and birth generation.
        /// </summary>
        /// <param name="id">The unique identifier of the new maze genome.</param>
        /// <param name="birthGeneration">The birth generation.</param>
        protected MazeGenome(uint id, uint birthGeneration)
        {
            // Set the unique genome ID and the birth generation
            Id = id;
            BirthGeneration = birthGeneration;

            // Create new evaluation info with no fitness history
            EvaluationInfo = new EvaluationInfo(0);

            // Instantiate new wall gene list
            WallGeneList = new List<WallGene>();

            // Instantiate new path gene list
            PathGeneList = new List<PathGene>();
        }

        /// <summary>
        ///     Constructor which constructs a new maze genome with the given genome factory, unique identifier and birth
        ///     generation.
        /// </summary>
        /// <param name="genomeFactory">Reference to the genome factory.</param>
        /// <param name="id">The unique identifier of the new maze genome.</param>
        /// <param name="birthGeneration">The birth generation.</param>
        public MazeGenome(MazeGenomeFactory genomeFactory, uint id, uint birthGeneration) : this(id, birthGeneration)
        {
            // Ensure that genome factory is non-null
            if (genomeFactory == null)
            {
                throw new SharpNeatException(
                    string.Format(
                        "Null genome factory passed in during construction of maze genome with id [{0}] and birth generation [{1}].  If the maze height/width are not explicitly specified, the genome factory is required for instantiating genome with boundary length defaults.",
                        id, birthGeneration));
            }

            // Set the initial maze height and width
            MazeBoundaryHeight = genomeFactory.BaseMazeHeight;
            MazeBoundaryWidth = genomeFactory.BaseMazeWidth;

            // Set relative cell height and width
            RelativeCellHeight = (double) 1/MazeBoundaryHeight;
            RelativeCellWidth = (double) 1/MazeBoundaryWidth;

            // Set the genome factory
            GenomeFactory = genomeFactory;
        }

        /// <summary>
        ///     Constructor which constructs a new maze genome with the given unique identifier, birth generation, and initial maze
        ///     height/width.
        /// </summary>
        /// <param name="genomeFactory">Reference to the genome factory.</param>
        /// <param name="id">The unique identifier of the new maze genome.</param>
        /// <param name="birthGeneration">The birth generation.</param>
        /// <param name="height">The base/initial height of the maze genome.</param>
        /// <param name="width">The base/initial width of the maze genome.</param>
        public MazeGenome(MazeGenomeFactory genomeFactory, uint id, uint birthGeneration, int height, int width)
            : this(id, birthGeneration)
        {
            // Set the initial maze height and width
            MazeBoundaryHeight = height;
            MazeBoundaryWidth = width;

            // Set relative cell height and width
            RelativeCellHeight = (double) 1/MazeBoundaryHeight;
            RelativeCellWidth = (double) 1/MazeBoundaryWidth;

            // Set the genome factory
            GenomeFactory = genomeFactory;

            // Compute max complexity based on existing genome complexity and maze dimensions
            MaxComplexity = MazeUtils.DetermineMaxPartitions(this);
        }

        /// <summary>
        ///     Constructor which constructs a new maze genome using the given base genome (this is often used in asexual
        ///     reproduction).  The new genome still has a unique identifier and a separately specified birth generation.
        /// </summary>
        /// <param name="copyFrom">The template genome on which the new maze genome is based.</param>
        /// <param name="id">The unique identifier of the new maze genome.</param>
        /// <param name="birthGeneration">The birth generation.</param>
        public MazeGenome(MazeGenome copyFrom, uint id, uint birthGeneration)
        {
            // Set the unique genome ID and the birth generation
            Id = id;
            BirthGeneration = birthGeneration;

            // Copy the other parameters off of the given genome
            GenomeFactory = copyFrom.GenomeFactory;
            MazeBoundaryHeight = copyFrom.MazeBoundaryHeight;
            MazeBoundaryWidth = copyFrom.MazeBoundaryWidth;
            RelativeCellHeight = copyFrom.RelativeCellHeight;
            RelativeCellWidth = copyFrom.RelativeCellWidth;
            WallGeneList = new List<WallGene>(DeepCopyWallGeneList(copyFrom.WallGeneList));
            PathGeneList = new List<PathGene>(DeepCopyPathGeneList(copyFrom.PathGeneList));
            EvaluationInfo = new EvaluationInfo(copyFrom.EvaluationInfo.FitnessHistoryLength);

            // Compute max complexity based on existing genome complexity and maze dimensions
            MaxComplexity = MazeUtils.DetermineMaxPartitions(this);
        }

        /// <summary>
        ///     Constructor which constructs a new maze genome with the given unique identifier, birth generation, and list of wall
        ///     genes.
        /// </summary>
        /// <param name="genomeFactory">Reference to the genome factory.</param>
        /// <param name="id">The unique identifier of the new maze genome.</param>
        /// <param name="birthGeneration">The birth generation.</param>
        /// <param name="height">The base/initial height of the maze genome.</param>
        /// <param name="width">The base/initial width of the maze genome.</param>
        /// <param name="wallGeneList">The list of wall genes.</param>
        /// <param name="pathGeneList">The list of path genes.</param>
        public MazeGenome(MazeGenomeFactory genomeFactory, uint id, uint birthGeneration, int height, int width,
            IList<WallGene> wallGeneList, IList<PathGene> pathGeneList)
            : this(genomeFactory, id, birthGeneration, height, width)
        {
            WallGeneList = wallGeneList;
            PathGeneList = pathGeneList;
        }

        #endregion

        #region Interface Properties

        /// <summary>
        ///     The unique identifier of the maze genome.
        /// </summary>
        public uint Id { get; }

        /// <summary>
        ///     NOT USED (required by interface).
        /// </summary>
        public int SpecieIdx { get; set; }

        /// <summary>
        ///     The birth generation of the maze genome.
        /// </summary>
        public uint BirthGeneration { get; }

        /// <summary>
        ///     Evaluation statistics for the maze genome (i.e. fitness, etc.).
        /// </summary>
        public EvaluationInfo EvaluationInfo { get; }

        /// <summary>
        ///     Computes the complexity of the maze genome.
        /// </summary>
        public double Complexity => WallGeneList.Count;

        /// <summary>
        ///     Gets a coordinate that represents the genome's position in the search space (also known
        ///     as the genetic encoding space). This allows speciation/clustering algorithms to operate on
        ///     an abstract cordinate data type rather than being coded against specific IGenome types.
        /// </summary>
        public CoordinateVector Position
        {
            get
            {
                if (null == _position)
                {
                    int interiorWallCount = WallGeneList.Count;

                    // Create array of key/value pairs to hold innovation IDs and their corresponding 
                    // "position" in the genetic encoding space                    
                    KeyValuePair<ulong, double>[] coordElemArray = new KeyValuePair<ulong, double>[interiorWallCount];

                    for (int i = 0; i < interiorWallCount; i++)
                    {
                        double wallLocation = WallGeneList[i].WallLocation;
                        double passageLocation = WallGeneList[i].PassageLocation;

                        // Calculate cantor pairing of relative wall and passage positions
                        double compositeGeneCoordinate = ((wallLocation + passageLocation)*
                                                          (wallLocation + passageLocation + 1))/2 + passageLocation;

                        // Add gene coordinate to array
                        coordElemArray[i] = new KeyValuePair<ulong, double>(WallGeneList[i].InnovationId,
                            compositeGeneCoordinate);
                    }

                    // Construct the genome coordinate vector
                    _position = new CoordinateVector(coordElemArray);
                }

                return _position;
            }
        }

        /// <summary>
        ///     The decoded phenotype produced from this maze genome (i.e. the physical maze itself).  This is used to speed up the
        ///     decoding process.
        /// </summary>
        public object CachedPhenome { get; set; }

        #endregion

        #region Public Interface Methods

        /// <summary>
        ///     Asexually reproduces a new maze genome based on copying this genome and assigning the given birth generation.
        /// </summary>
        /// <param name="birthGeneration">The birth generation of the new maze genome.</param>
        /// <returns>The new maze genome.</returns>
        public MazeGenome CreateOffspring(uint birthGeneration)
        {
            // Make a new genome that is a copy of this one but with a new genome ID
            MazeGenome offspring = GenomeFactory.CreateGenomeCopy(this, GenomeFactory.GenomeIdGenerator.NextId,
                birthGeneration);

            // Mutate the new genome
            offspring.Mutate();

            return offspring;
        }

        /// <summary>
        ///     NOT USED (required by interface) - this would perform sexual reproduction (i.e. crossover), but that's not
        ///     currently supported.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="birthGeneration"></param>
        /// <returns></returns>
        public MazeGenome CreateOffspring(MazeGenome parent, uint birthGeneration)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Maze Genome Mutation Methods

        /// <summary>
        ///     Performs a mutation operation, which can be path-altering or non-path altering. Path altering mutations add
        ///     waypoints or otherwise shift the path/trajectory through the maze. Non-path altering mutations move/add walls.
        /// </summary>
        private void Mutate()
        {
            int outcome;

            // If there are not yet any waypoints defined, the mutation must be to add a waypoint
            // (this is really not feasible at all because without any waypoints, the maze would not
            // be navigable)
            if (PathGeneList.Count <= 0)
            {
                MutateAddPathWaypoint();
                return;
            }

            // If there are not yet any walls to mutate, the mutation will be to add a wall
            // (otherwise, the resulting maze will be exactly the same structure)
            if (WallGeneList.Count <= 0)
            {
                MutateAddWall();
                return;
            }

            do
            {
                // Get random mutation to perform
                outcome = RouletteWheel.SingleThrow(GenomeFactory.MazeGenomeParameters.RouletteWheelLayout,
                    GenomeFactory.Rng);
            } while (WallGeneList.Count >= MaxComplexity && outcome >= 2);

            switch (outcome)
            {
                case 0:
                    MutateWallStartLocations();
                    break;
                case 1:
                    MutatePassageStartLocations();
                    break;
                case 2:
                    MutateAddWall();
                    break;
                case 3:
                    MutateDeleteWall();
                    break;
                case 4:
                    MutateExpandMaze();
                    break;
                case 5:
                    MutatePathWaypointLocation();
                    break;
                case 6:
                    MutateAddPathWaypoint();
                    break;
            }

            // If the mutation caused a reduction in max complexity, remove non-coding genes
            RemoveNonCodingGenes();
        }

        /// <summary>
        ///     Mutates the location of the wall based on both the wall start mutation probability and the perturbance magnitude.
        /// </summary>
        private void MutateWallStartLocations()
        {
            bool mutationOccurred = false;
            int mazeTreeDepth = (int) Math.Log(WallGeneList.Count, 2) + 1;

            // Don't try to mutate if the gene list is empty
            if (WallGeneList.Count <= 0)
                return;

            // Iterate through each gene (wall) and probabilistically shift its location (scaling perturbance magnitude by wall effect size)
            for (int geneIdx = 0; geneIdx < WallGeneList.Count; geneIdx++)
            {
                if (GenomeFactory.Rng.NextDouble() <
                    GenomeFactory.MazeGenomeParameters.MutateWallStartLocationProbability)
                {
                    WallGeneList[geneIdx].WallLocation = BoundStartLocation(WallGeneList[geneIdx].WallLocation +
                                                                            (((GenomeFactory.Rng.NextDouble()*2) - 1)*
                                                                             GenomeFactory.MazeGenomeParameters
                                                                                 .PerturbanceMagnitude*
                                                                             ((double)
                                                                                 ((int) (Math.Log(geneIdx + 1, 2)) + 1)/
                                                                              mazeTreeDepth)));
                    mutationOccurred = true;
                }
            }

            // Ensure that a mutation actually occurs
            if (mutationOccurred == false)
            {
                // Select a random gene to mutate
                int mazeGeneIdx = GenomeFactory.Rng.Next(WallGeneList.Count);

                // Perform mutation
                WallGeneList[mazeGeneIdx].WallLocation = BoundStartLocation(WallGeneList[mazeGeneIdx].WallLocation +
                                                                            (((GenomeFactory.Rng.NextDouble()*2) - 1)*
                                                                             GenomeFactory.MazeGenomeParameters
                                                                                 .PerturbanceMagnitude*
                                                                             ((double)
                                                                                 ((int) (Math.Log(mazeGeneIdx + 1, 2)) +
                                                                                  1)/
                                                                              mazeTreeDepth)));
            }
        }

        /// <summary>
        ///     Mutates the location of the wall passage based on both the passage start mutation probability and the perturbance
        ///     magnitude.
        /// </summary>
        private void MutatePassageStartLocations()
        {
            bool mutationOccurred = false;
            int mazeTreeDepth = (int) Math.Log(WallGeneList.Count, 2) + 1;

            // Don't try to mutate if the gene list is empty
            if (WallGeneList.Count <= 0)
                return;

            // Iterate through each gene (wall) and probabilistically shift its passage location (scaling perturbance magnitude by wall effect size)
            for (int geneIdx = 0; geneIdx < WallGeneList.Count; geneIdx++)
            {
                if (GenomeFactory.Rng.NextDouble() <
                    GenomeFactory.MazeGenomeParameters.MutatePassageStartLocationProbability)
                {
                    WallGeneList[geneIdx].PassageLocation = BoundStartLocation(WallGeneList[geneIdx].PassageLocation +
                                                                               (((GenomeFactory.Rng.NextDouble()*2) - 1)*
                                                                                GenomeFactory.MazeGenomeParameters
                                                                                    .PerturbanceMagnitude*
                                                                                ((double)
                                                                                    ((int) (Math.Log(geneIdx + 1, 2)) +
                                                                                     1)/
                                                                                 mazeTreeDepth)));

                    mutationOccurred = true;
                }
            }

            // Ensure that a mutation actually occurs
            if (mutationOccurred == false)
            {
                // Select a random gene to mutate
                int mazeGeneIdx = GenomeFactory.Rng.Next(WallGeneList.Count);

                WallGeneList[mazeGeneIdx].PassageLocation = BoundStartLocation(WallGeneList[mazeGeneIdx].WallLocation +
                                                                               (((GenomeFactory.Rng.NextDouble()*2) - 1)*
                                                                                GenomeFactory.MazeGenomeParameters
                                                                                    .PerturbanceMagnitude*
                                                                                ((double)
                                                                                    ((int)
                                                                                        (Math.Log(mazeGeneIdx + 1, 2)) +
                                                                                     1)/
                                                                                 mazeTreeDepth)));
            }
        }

        /// <summary>
        ///     Probabalistically adds a new wall.  This is equivalent to adding a new gene to the genome
        /// </summary>
        private void MutateAddWall()
        {
            // Generate new wall and passage start locations
            double newWallStartLocation = GenomeFactory.Rng.NextDoubleNonZero();
            double newPassageStartLocation = GenomeFactory.Rng.NextDoubleNonZero();

            // Add new gene to the genome
//            WallGeneList.Add(new WallGene(GenomeFactory.InnovationIdGenerator.NextId, newWallStartLocation,
//                newPassageStartLocation, GenomeFactory.Rng.NextBool()));
            // TODO: Evaluating effect of biasing toward vertical walls
            WallGeneList.Add(new WallGene(GenomeFactory.InnovationIdGenerator.NextId, newWallStartLocation,
                newPassageStartLocation, (GenomeFactory.Rng.NextDoubleNonZero() > 0.72)));
        }

        /// <summary>
        ///     Probabalistically deletes a random wall.  This is equivalent to deleting a gene from the genome.
        /// </summary>
        private void MutateDeleteWall()
        {
            // Don't attempt to delete a wall if only one exists
            if (WallGeneList.Count < 2)
            {
                return;
            }

            // Select a random wall to be deleted
            // TODO: Probably need to scale deletion mutation here based on effect size
            int wallIdx = GenomeFactory.Rng.Next(WallGeneList.Count);

            // Delete the wall
            WallGeneList.RemoveAt(wallIdx);
        }

        /// <summary>
        ///     Probabalistically expands the maze area by one unit.
        /// </summary>
        private void MutateExpandMaze()
        {
            // TODO: This could also support incrementing in only one dimension
            // Increment maze height and width by 1
            MazeBoundaryHeight += 1;
            MazeBoundaryWidth += 1;

            // Update relative cell width/height
            RelativeCellHeight = (double) 1/MazeBoundaryHeight;
            RelativeCellWidth = (double) 1/MazeBoundaryWidth;
        }

        /// <summary>
        ///     Probabalistically shifts one of the waypoints by one unit in the horizontal or vertical direction.
        /// </summary>
        private void MutatePathWaypointLocation()
        {
            Point2DInt mutatedPoint = new Point2DInt();
            int geneIdx;

            // Don't try to mutate if the gene list is empty
            if (PathGeneList.Count <= 0)
                return;

            // Attempt to mutate a waypoint on the path until we get a valid point
            // (only one waypoint point at a time is mutated to avoid drastically changing the path)
            do
            {
                // Select a random gene to mutate
                geneIdx = GenomeFactory.Rng.Next(PathGeneList.Count);

                // Apply the appropriate transformation based on the point shift direction
                switch ((PointShift) (GenomeFactory.Rng.Next(1, 5)))
                {
                    case PointShift.Down:
                    {
                        mutatedPoint = new Point2DInt(PathGeneList[geneIdx].Waypoint.X,
                            PathGeneList[geneIdx].Waypoint.Y + 1);
                        break;
                    }
                    case PointShift.Up:
                    {
                        mutatedPoint = new Point2DInt(PathGeneList[geneIdx].Waypoint.X,
                            PathGeneList[geneIdx].Waypoint.Y - 1);
                        break;
                    }
                    case PointShift.Left:
                    {
                        mutatedPoint = new Point2DInt(PathGeneList[geneIdx].Waypoint.X - 1,
                            PathGeneList[geneIdx].Waypoint.Y);
                        break;
                    }
                    case PointShift.Right:
                    {
                        mutatedPoint = new Point2DInt(PathGeneList[geneIdx].Waypoint.X + 1,
                            PathGeneList[geneIdx].Waypoint.Y);
                        break;
                    }
                }
            } while (IsValidLocation(mutatedPoint) == false);

            // Set the new, validated waypoint
            PathGeneList[geneIdx].Waypoint = mutatedPoint;
        }

        /// <summary>
        ///     Probabalistically adds a single new waypoint in the maze solution path.
        /// </summary>
        private void MutateAddPathWaypoint()
        {
            Point2DInt newPoint;

            // Generate new points until we reach one that is valid and is in a sparse region of the maze
            do
            {
                newPoint = GetSparseGridCell();
            } while (IsValidLocation(newPoint) == false);

            // Add the new path gene to the genome
            PathGeneList.Add(new PathGene(GenomeFactory.InnovationIdGenerator.NextId, newPoint,
                GenomeFactory.Rng.NextBool() ? IntersectionOrientation.Horizontal : IntersectionOrientation.Vertical));
        }

        #endregion

        #region Maze Genome utility methods

        /// <summary>
        ///     Bounds the starting location for a wall or passage such that its non-negative and doesn't exceed 1.
        /// </summary>
        /// <param name="proposedLocation">The location proposed by the mutation.</param>
        /// <returns>The bounded location.</returns>
        private double BoundStartLocation(double proposedLocation)
        {
            if (proposedLocation < 0)
                return 0;
            if (proposedLocation > 1)
                return 1;
            return proposedLocation;
        }

        /// <summary>
        ///     Ensures that the waypoint location (resulting from a mutation) is within the maze boundaries and does not overlap
        ///     with other waypoints or with the start/end location (which are in the upper-left and lower right cells of the maze
        ///     respectively). Specifically, the following validity checks are performed:
        ///     1. Checks X and Y minimum and maximum points are in the horizontal and vertical boundaries of the maze
        ///     respectively.
        ///     2. Checks that proposed location does not overlap existing waypoints.
        ///     3. Checks that proposed location does not overlap start location or target location.
        ///     4. Checks that proposed location is higher than the next to the last row, or that the proposed location is on the
        ///     next to the last row but its x-position is less than or equal to existing points on the last row, or that the
        ///     proposed location is on the last row but its x-position is greater than or equal to existing points on the next to
        ///     the last row. This is to prevent trajectory overlaps.
        /// </summary>
        /// <param name="waypointLocation">The proposed waypoint.</param>
        /// <returns>Boolean indicating whether the given point is valid per the maze boundary constraints.</returns>
        private bool IsValidLocation(Point2DInt waypointLocation)
        {
            return
                // Check that x-coordinate is at-or-above minimum maze width
                waypointLocation.X >= 0 &&

                // Check that x-coordinate is within maze width boundary
                waypointLocation.X < MazeBoundaryWidth &&

                // Check that y-coordinate is at-or-above minimum maze height
                waypointLocation.Y >= 0 &&

                // Check that y-coordinate is within maze height boundary
                waypointLocation.Y < MazeBoundaryHeight &&

                // Check that no existing waypoint overlaps proposed waypoint
                PathGeneList.Any(g => waypointLocation.Equals(g.Waypoint)) == false &&

                // Check that proposed waypoint does not overlap start position
                waypointLocation.Equals(new Point2DInt(0, 0)) == false &&

                // Check that proposed waypoint does not overlap target position
                waypointLocation.Equals(new Point2DInt(MazeBoundaryWidth - 1, MazeBoundaryHeight - 1)) == false &&

                // Check that proposed waypoint is higher than next-to-last row
                (waypointLocation.Y < MazeBoundaryHeight - 2 ||

                 // If proposed waypoint is on next-to-last row, check that it's further to the left than waypoints on last row 
                 (waypointLocation.Y == MazeBoundaryHeight - 2 &&
                  (PathGeneList.Count(g => g.Waypoint.Y == MazeBoundaryHeight - 1) == 0 ||
                   PathGeneList.Where(g => g.Waypoint.Y == MazeBoundaryHeight - 1).Min(g => g.Waypoint.X) >=
                   waypointLocation.X) ||

                  // If proposed waypoint is on the last row, check that it's further to the right than waypoints on next-to-last row
                  (waypointLocation.Y == MazeBoundaryHeight - 1 &&
                   (PathGeneList.Count(g => g.Waypoint.Y == MazeBoundaryHeight - 2) == 0 ||
                    PathGeneList.Where(g => g.Waypoint.Y == MazeBoundaryHeight - 2).Max(g => g.Waypoint.X) <=
                    waypointLocation.X))));
        }

        /// <summary>
        ///     Extracts a random most sparse cell from among the top N most sparse regions in the maze space.
        /// </summary>
        /// <returns>A randomly selected cell from a list of sparse regions within the maze space.</returns>
        private Point2DInt GetSparseGridCell()
        {
            // Get count of neighboring cells within neighborhood radius
            var cellNeighborCounts = MazeUtils.ComputeCellNeighborCounts(PathGeneList, MazeBoundaryHeight,
                MazeBoundaryWidth, 2);

            // Compute crowd factor scores for each cell by dividing neighbor count by total number of waypoints
            Dictionary<Point2DInt, double> cellCrowdFactor = cellNeighborCounts.ToDictionary(cell => cell.Key,
                cell => (double) cell.Value/PathGeneList.Count);

            // Extract the specified proportion of most sparse cells and return random cell in set
            return cellCrowdFactor.OrderBy(x => x.Value)
                .Take((int) Math.Ceiling(0.2*cellCrowdFactor.Count))
                .OrderBy(x => GenomeFactory.Rng.Next())
                .First().Key;
        }

        /// <summary>
        ///     Performs a deep copy on the wall genes.
        /// </summary>
        /// <param name="copyFrom">The source wall gene list to duplicate.</param>
        /// <returns>A newly constructed wall gene list.</returns>
        private IList<WallGene> DeepCopyWallGeneList(IList<WallGene> copyFrom)
        {
            List<WallGene> copiedGeneList = new List<WallGene>(copyFrom.Count);

            // Duplicate all maze genes
            copiedGeneList.AddRange(copyFrom.Select(mazeGene => mazeGene.CreateCopy()));

            return copiedGeneList;
        }

        /// <summary>
        ///     Performs a deep copy on the path genes.
        /// </summary>
        /// <param name="copyFrom">The source path gene list to duplicate.</param>
        /// <returns>A newly constructed path gene list.</returns>
        private IList<PathGene> DeepCopyPathGeneList(IList<PathGene> copyFrom)
        {
            List<PathGene> copiedGeneList = new List<PathGene>(copyFrom.Count);

            // Duplicate all path genes
            copiedGeneList.AddRange(copyFrom.Select(pathGene => pathGene.CreateCopy()));

            return copiedGeneList;
        }

        /// <summary>
        ///     Recomputes the maximum complexity supported by the maze genome following a mutation and removes non-coding genes in
        ///     the event that the mutation reduced the maximum complexity supported by the maze.
        /// </summary>
        private void RemoveNonCodingGenes()
        {
            // Recompute max complexity in the event that mutation changed wall/passage placement in a way that reduces the complexity cap
            MaxComplexity = MazeUtils.DetermineMaxPartitions(this);

            // If the max complexity is now lower, remove the non-coding genes
            if (MaxComplexity < WallGeneList.Count)
            {
                ((List<WallGene>) WallGeneList).RemoveRange(WallGeneList.Count - (MaxComplexity - WallGeneList.Count),
                    MaxComplexity - WallGeneList.Count);
            }
        }

        #endregion
    }
}