using System;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using MCC_Domains.BodyBrain.Bootstrappers;
using MCC_Domains.BodyBrain.MCCExperiment;
using MCC_Domains.Utils;
using SharpNeat;
using SharpNeat.Core;
using SharpNeat.Genomes.Substrate;
using SharpNeat.Phenomes.Voxels;

namespace MCC_Domains.BodyBrain
{
    public static class BodyBrainExperimentUtils
    {
        /// <summary>
        ///     Determines which MCC body/brain initializer to instantiate and return based on the initialization algorithm search
        ///     type.
        /// </summary>
        /// <param name="xmlConfig">XML initialization configuration.</param>
        /// <param name="brainType">The brain type with which to configure the initializer.</param>
        /// <returns>The instantiated initializer.</returns>
        public static BodyBrainInitializer DetermineMCCBodyBrainInitializer(XmlElement xmlConfig, BrainType brainType)
        {
            // Make sure that the XML configuration exists
            if (xmlConfig == null)
            {
                throw new ArgumentException("Missing or invalid MCC initialization configuration.");
            }

            // Extract the corresponding search and selection algorithm domain types
            var searchType =
                AlgorithmTypeUtil.ConvertStringToSearchType(XmlUtils.TryGetValueAsString(xmlConfig, "SearchAlgorithm"));

            // There's currently just two MCC initializers: fitness and novelty search
            switch (searchType)
            {
                // TODO: Implement fitness initializer
                case SearchType.Fitness:
                    return new BodyBrainFitnessInitializer(brainType);
                default:
                    return new BodyBrainNoveltySearchInitializer(brainType);
            }
        }

        /// <summary>
        ///     Reads body genome configuration parameters, including substrate mutation probability.
        /// </summary>
        /// <param name="xmlElem">The top-level XML element containing the body genome configuration parameters.</param>
        /// <returns>
        ///     A NeatSubstrateGenomeParameters object containing substrate-specific mutation probabilities (which encodes
        ///     body size/dimensions).
        /// </returns>
        public static NeatSubstrateGenomeParameters ReadBodyGenomeParameters(XmlElement xmlElem)
        {
            // Get root of the body genome configuration section
            var nodeList = xmlElem.GetElementsByTagName("BodyGenomeConfig", "");

            // Convert to an XML element
            var xmlBodyConfig = nodeList[0] as XmlElement;

            // Read body genome parameters and create substrate genome parameters
            return new NeatSubstrateGenomeParameters(XmlUtils.GetValueAsDouble(xmlBodyConfig, "ModifyBodyProbability"),
                XmlUtils.GetValueAsDouble(xmlBodyConfig, "ExpandBodyProbability"),
                XmlUtils.GetValueAsDouble(xmlBodyConfig, "ShrinkBodyProbability"));
        }

        /// <summary>
        ///     Reads voxelyze-specific simulation parameters from the given XML configuration file, and instantiates a simulation
        ///     properties class with the XML-defined parameters as well as more dynamic, invocation-parameters that were passed
        ///     from the command line.
        /// </summary>
        /// <param name="xmlElem">The top-level XML element containing the simulation configuration parameters.</param>
        /// <param name="simConfigDirectory">
        ///     The directory into which to persist the generated simulation configuration that is fed
        ///     to voxelyze.
        /// </param>
        /// <param name="simResultsDirectory">The directory into which simulation results should be written.</param>
        /// <param name="simExecutableFile">The path to the voxelyze simulation executable.</param>
        /// <returns>A simulation properties object containing all of the extracted simulation parameters.</returns>
        public static SimulationProperties ReadSimulationProperties(XmlElement xmlElem, string simConfigDirectory,
            string simResultsDirectory, string simExecutableFile)
        {
            // Get root of the voxelyze configuration section
            var nodeList = xmlElem.GetElementsByTagName("VoxelyzeConfig", "");

            // Convert to an XML element
            var xmlSimProps = nodeList[0] as XmlElement;

            // Read brain type
            var brainType = XmlUtils.GetValueAsString(xmlSimProps, "BrainType");

            // Read all of the applicable parameters in and create the simulation properties object
            return new SimulationProperties(simConfigDirectory, simResultsDirectory,
                brainType == BrainType.NeuralNet.ToString() ? BrainType.NeuralNet : BrainType.PhaseOffset,
                XmlUtils.GetValueAsString(xmlSimProps, "VxaTemplateFile"), simExecutableFile,
                XmlUtils.GetValueAsDouble(xmlSimProps, "MinPercentMaterial"),
                XmlUtils.GetValueAsDouble(xmlSimProps, "MinPercentActiveMaterial"),
                XmlUtils.GetValueAsInt(xmlSimProps, "InitialXDimension"),
                XmlUtils.GetValueAsInt(xmlSimProps, "InitialYDimension"),
                XmlUtils.GetValueAsInt(xmlSimProps, "InitialZDimension"),
                XmlUtils.GetValueAsInt(xmlSimProps, "BrainNetworkConnections"),
                XmlUtils.GetValueAsDouble(xmlSimProps, "SimulatedSeconds"),
                XmlUtils.GetValueAsDouble(xmlSimProps, "InitializationSeconds"),
                XmlUtils.GetValueAsInt(xmlSimProps, "ActuationsPerSecond"),
                XmlUtils.GetValueAsDouble(xmlSimProps, "FloorSlope"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaSimOutputXPath"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaSimStopConditionXPath"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaEnvThermalXPath"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaEnvGravityXPath"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaStructureXPath"),
                XmlUtils.GetValueAsString(xmlSimProps, "VxaMinimalCriterionXPath"));
        }

        /// <summary>
        ///     Reads simulation results from the given simulation results file.
        /// </summary>
        /// <param name="resultsFilePath">The path to the simulation results file.</param>
        /// <returns>A simulation results object containing extracted simulation results.</returns>
        public static SimulationResults ReadSimulationResults(string resultsFilePath)
        {
            // Load the results file
            var resultsDoc = new XmlDocument();
            resultsDoc.Load(resultsFilePath);

            // Attempt to get the root of the fitness section
            var nodeList = resultsDoc.GetElementsByTagName("Fitness", "");

            if (nodeList.Count <= 0)
            {
                throw new SharpNeatException($"Failed to read simulation results file: [{resultsFilePath}]");
            }

            // Convert to an XML element
            var fitnessXml = nodeList[0] as XmlElement;

            // Extract distance and location
            var xPos = XmlUtils.GetValueAsDouble(fitnessXml, "xPos");
            var yPos = XmlUtils.GetValueAsDouble(fitnessXml, "yPos");
            var distance = XmlUtils.GetValueAsDouble(fitnessXml, "Distance");
            var simTime = XmlUtils.GetValueAsDouble(fitnessXml, "simTime");

            // Construct and return simulation results object
            return new SimulationResults(xPos, yPos, distance, simTime);
        }

        /// <summary>
        ///     Writes the simulation configuration file that dynamically configures the voxelyze simulator.
        /// </summary>
        /// <param name="vxaTemplatePath">
        ///     The path to the simulation configuration template file, containing simulation parameter
        ///     defaults.
        /// </param>
        /// <param name="outputPath">The directory into which the generated Voxelyze simulation configuration file is written.</param>
        /// <param name="simResultsFilePath">The path of the file into which to write simulation results.</param>
        /// <param name="brain">The voxel brain object containing per-voxel parameters.</param>
        /// <param name="body">The voxel body object containing voxel material specifications.</param>
        /// <param name="mcDistance">The distance traveled minimal criterion.</param>
        /// <param name="evalCycles">The number of cycles that should elapse between evaluations (default is 20).</param>
        /// <param name="evalInitTime">
        ///     The amount of time alloted for thermal initialization before actuation is applied (default
        ///     is 0.3 seconds).
        /// </param>
        /// <param name="vxaSimGaXPath">The XPath location containing GA simulation parameters (optional).</param>
        /// <param name="vxaStructureXPath">The XPath location containing voxel structure configuration properties (optional).</param>
        /// <param name="vxaMcXPath">The XPath location containing the minimal criterion configuration (optional).</param>
        /// <param name="vxaSimStopConditionXPath">The XPath location containing the StopCondition properties (optional).</param>
        /// <param name="vxaThermalPath">The XPath location containing the thermal properties (optional).</param>
        public static void WriteVoxelyzeSimulationFile(string vxaTemplatePath, string outputPath,
            string simResultsFilePath, IVoxelBrain brain, VoxelBody body, double mcDistance, int evalCycles = 20,
            double evalInitTime = 0.3,
            string vxaSimGaXPath = "/VXA/Simulator/GA", string vxaStructureXPath = "/VXA/VXC/Structure",
            string vxaMcXPath = "/VXA/Environment/MinimalCriterion",
            string vxaSimStopConditionXPath = "/VXA/Simulator/StopCondition",
            string vxaThermalPath = "/VXA/Environment/Thermal")
        {
            // Instantiate XML reader for VXA template file
            var simDoc = new XmlDocument();
            simDoc.Load(vxaTemplatePath);

            // Enable fitness file logging and set the results output file name and path
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "WriteFitnessFile")).InnerText = "1";
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "FitnessFileName")).InnerText = simResultsFilePath;

            // Disable simulation logging
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "WriteSimLogFile")).InnerText = "0";

            // Set the distance minimal criterion
            simDoc.SelectSingleNode(string.Join("/", vxaMcXPath, "Distance")).InnerText =
                mcDistance.ToString(CultureInfo.InvariantCulture);

            // If this is a phase offset brain, also set period and simulation time
            if (brain is VoxelPhaseOffsetBrain poBrain)
            {
                // Set the stop condition value
                simDoc.SelectSingleNode(string.Join("/", vxaSimStopConditionXPath, "StopConditionValue")).InnerText =
                    CalculateStopTime(evalCycles, poBrain.Frequency, evalInitTime)
                        .ToString(CultureInfo.InvariantCulture);

                // Set the period value 
                simDoc.SelectSingleNode(string.Join("/", vxaThermalPath, "TempPeriod")).InnerText =
                    (1 / poBrain.Frequency).ToString(CultureInfo.InvariantCulture);
            }

            // Set body/brain voxel structure properties
            SetVoxelBodyBrainProperties(simDoc, brain, body, vxaStructureXPath);

            simDoc.Save(outputPath);
        }

        /// <summary>
        ///     Writes the simulation configuration file that dynamically configures the voxelyze simulator. This disables writing
        ///     the fitness file
        /// </summary>
        /// <param name="vxaTemplatePath">
        ///     The path to the simulation configuration template file, containing simulation parameter
        ///     defaults.
        /// </param>
        /// <param name="outputPath">The directory into which the generated Voxelyze simulation configuration file is written.</param>
        /// <param name="simLogFilePath">The path of the file into which to write the simulation log data.</param>
        /// <param name="stopCondition">
        ///     The condition under which the simulation should terminate (typically a time limit for
        ///     post-hoc analysis).
        /// </param>
        /// <param name="stopConditionValue">The numeric value/threshold for the stop condition.</param>
        /// <param name="brain">The voxel brain object containing per-voxel network weights.</param>
        /// <param name="body">The voxel body object containing voxel material specifications.</param>
        /// <param name="vxaSimStopConditionXPath">The XPath location containing the StopCondition properties (optional).</param>
        /// <param name="vxaSimGaXPath">The XPath location containing GA simulation parameters (optional).</param>
        /// <param name="vxaStructureXPath">The XPath location containing voxel structure configuration properties (optional).</param>
        /// <param name="vxaThermalPath">The XPath location containing the thermal properties (optional).</param>
        public static void WriteVoxelyzeSimulationFile(string vxaTemplatePath, string outputPath, string simLogFilePath,
            int stopCondition, double stopConditionValue, IVoxelBrain brain, VoxelBody body,
            string vxaSimStopConditionXPath = "/VXA/Simulator/StopCondition",
            string vxaSimGaXPath = "/VXA/Simulator/GA", string vxaStructureXPath = "/VXA/VXC/Structure",
            string vxaThermalPath = "/VXA/Environment/Thermal")
        {
            // Instantiate XML reader for VXA template file
            var simDoc = new XmlDocument();
            simDoc.Load(vxaTemplatePath);

            // Enable simulation logging and set the simulation log file name and path
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "WriteSimLogFile")).InnerText = "1";
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "SimLogFileName")).InnerText = simLogFilePath;

            // Disable fitness logging
            simDoc.SelectSingleNode(string.Join("/", vxaSimGaXPath, "WriteFitnessFile")).InnerText = "0";

            // Set the stop condition type and value
            simDoc.SelectSingleNode(string.Join("/", vxaSimStopConditionXPath, "StopConditionType")).InnerText =
                stopCondition.ToString();
            simDoc.SelectSingleNode(string.Join("/", vxaSimStopConditionXPath, "StopConditionValue")).InnerText =
                stopConditionValue.ToString(CultureInfo.InvariantCulture);

            // If this is a phase offset brain, also set the period
            if (brain is VoxelPhaseOffsetBrain poBrain)
            {
                simDoc.SelectSingleNode(string.Join("/", vxaThermalPath, "TempPeriod")).InnerText =
                    (1 / poBrain.Frequency).ToString(CultureInfo.InvariantCulture);
            }

            // Set body/brain voxel structure properties
            SetVoxelBodyBrainProperties(simDoc, brain, body, vxaStructureXPath);

            simDoc.Save(outputPath);
        }

        /// <summary>
        ///     Calculates the stop time for phase offset controller experiments.
        /// </summary>
        /// <param name="evalCycles">The number of cycles that should elapse between evaluations.</param>
        /// <param name="frequency">The voxel oscillation frequency.</param>
        /// <param name="evalInitTime">The amount of time alloted for thermal initialization before actuation is applied.</param>
        /// <returns>The stop time.</returns>
        private static double CalculateStopTime(double evalCycles, double frequency, double evalInitTime)
        {
            return evalCycles / frequency + evalInitTime;
        }

        /// <summary>
        ///     Sets the voxel body size and material properties and brain synpase weights.
        /// </summary>
        /// <param name="simDoc">Reference to VXA template file.</param>
        /// <param name="brain">The voxel brain object containing per-voxel parameters.</param>
        /// <param name="body">The voxel body object containing voxel material specifications.</param>
        /// <param name="vxaStructureXPath">The XPath location containing voxel structure configuration properties.</param>
        private static void SetVoxelBodyBrainProperties(XmlDocument simDoc, IVoxelBrain brain, VoxelBody body,
            string vxaStructureXPath)
        {
            // Get reference to structure definition section
            var structureElem = simDoc.SelectSingleNode(vxaStructureXPath);

            // Set voxel structure dimensions
            structureElem.SelectSingleNode("X_Voxels").InnerText = body.LengthX.ToString();
            structureElem.SelectSingleNode("Y_Voxels").InnerText = body.LengthY.ToString();
            structureElem.SelectSingleNode("Z_Voxels").InnerText = body.LengthZ.ToString();

            switch (brain)
            {
                case VoxelAnnBrain annBrain:
                {
                    // Set number of brain connections
                    structureElem.SelectSingleNode("numSynapses").InnerText = annBrain.NumConnections.ToString();

                    // Set layer-wise material and connection weights
                    for (var layerIdx = 0; layerIdx < body.LengthZ; layerIdx++)
                    {
                        // Create a new layer XML element for body materials and connections
                        var bodyLayerElem = simDoc.CreateElement("Layer");
                        var connLayerElem = simDoc.CreateElement("Layer");

                        // Wrap layer material codes and connection weights in a CDATA and add to each layer XML
                        bodyLayerElem.AppendChild(simDoc.CreateCDataSection(body.GetLayerMaterialCodes(layerIdx)));
                        connLayerElem.AppendChild(simDoc.CreateCDataSection(annBrain.GetFlattenedLayerData(layerIdx)));

                        // Append layers to XML document
                        structureElem.SelectSingleNode("Data").AppendChild(bodyLayerElem);
                        structureElem.SelectSingleNode("SynapseWeights").AppendChild(connLayerElem);
                    }

                    break;
                }
                case VoxelPhaseOffsetBrain poBrain:
                {
                    // Set layer-wise material and phase offsets
                    for (var layerIdx = 0; layerIdx < body.LengthZ; layerIdx++)
                    {
                        // Create a new layer XML element for body materials and connections
                        var bodyLayerElem = simDoc.CreateElement("Layer");
                        var poLayerElem = simDoc.CreateElement("Layer");

                        // Wrap layer material codes and phase offset values in a CDATA and add to each layer XML
                        bodyLayerElem.AppendChild(simDoc.CreateCDataSection(body.GetLayerMaterialCodes(layerIdx)));
                        poLayerElem.AppendChild(simDoc.CreateCDataSection(poBrain.GetFlattenedLayerData(layerIdx)));

                        // Append layers to XML document
                        structureElem.SelectSingleNode("Data").AppendChild(bodyLayerElem);
                        structureElem.SelectSingleNode("PhaseOffset").AppendChild(poLayerElem);
                    }

                    break;
                }
            }
        }

        /// <summary>
        ///     Builds the file path for a particular Voxelyze configuration or output file.
        /// </summary>
        /// <param name="fileType">The type of file being written (e.g. configuration, results).</param>
        /// <param name="extension">The file extension (typically vxa).</param>
        /// <param name="outputDirectory">The directory into which the file will be written or is located.</param>
        /// <param name="experimentName">The name of the experiment to which the file corresponds.</param>
        /// <param name="run">The run number of the experiment to which the file corresponds.</param>
        /// <param name="brainGenomeId">The unique ID of the brain genome being simulated.</param>
        /// <param name="bodyGenomeId">The unique ID of the body genome being simulated.</param>
        /// <param name="isBrainEval">Indicates whether the configuration file reference is for a voxel brain evaluation.</param>
        /// <returns></returns>
        public static string ConstructVoxelyzeFilePath(string fileType, string extension, string outputDirectory,
            string experimentName, int run, uint brainGenomeId, uint bodyGenomeId, bool isBrainEval)
        {
            return isBrainEval
                ? string.Join("/", outputDirectory,
                    $"voxelyze_sim_{fileType}_exp_{experimentName.Replace(" ", "_")}_run_{run}_brain_{brainGenomeId}_body_{bodyGenomeId}.{extension}")
                : string.Join("/", outputDirectory,
                    $"voxelyze_sim_{fileType}_exp_{experimentName.Replace(" ", "_")}_run_{run}_body_{bodyGenomeId}_brain_{brainGenomeId}.{extension}");
        }

        /// <summary>
        ///     Configures the command for executing a voxelyze simulation.
        /// </summary>
        /// <param name="simExecutableFile">The path to the Voxelyze executable.</param>
        /// <param name="simConfigFilePath">The path to the simulation configuration file.</param>
        /// <returns>A ProcessStartInfo configured to execute a Voxelyze simulation.</returns>
        public static ProcessStartInfo ConfigureSimulationExecution(string simExecutableFile, string simConfigFilePath)
        {
            return new ProcessStartInfo
            {
                FileName = simExecutableFile,
                Arguments = $"-f \"{simConfigFilePath}\"",
                CreateNoWindow = true
            };
        }
    }
}