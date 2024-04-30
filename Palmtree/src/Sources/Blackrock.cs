/**
 * Blackrock source module class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Diagnostics;
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.Params;
using System.Collections.Generic;
using Palmtree.Sources.Wrappers;
using System.Text;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;

namespace Palmtree.Sources {
    using ValueOrder = SamplePackageFormat.ValueOrder;

    /// <summary>
    /// The <c>Blackrock</c> class.
    /// 
    /// ...
    /// </summary>
    public class Blackrock : ISource {
        
        private const string CLASS_NAME = "Blackrock";
        private const int CLASS_VERSION = 5;

        // 
        private const int NUM_FE_ANALOG_IN_CHANS = 256;                                 // number of (analog) front-end analog-in channels (should be 0-255 on NSP)
        private const int NUM_ANALOG_IN_CHANS = 16;                                     // number of (analog) analog-in channels (should be 256-271 on NSP)

        private const int LIMIT_TO_FIRST_CHANS = 128;                                   // temporary constant to limit the number streamed of channels
        private const double MIN_PERCENTAGE_SAMPLES_EXPECTED = 0.75;                    // the percentage of samples per package from the NSP that needs to be met for a package to be processed (allows us to exclude startup-packages)

        private const int threadLoopDelayNoProc = 200;                                  // thread loop delay when not processing (1000ms / 5 run times per second = rest 200ms)
        

        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);


        private Thread signalThread = null;                                             // the source thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)
        private ManualResetEvent loopManualResetEvent = new ManualResetEvent(false);    // Manual reset event to call the WaitOne event on (this allows - in contrast to the sleep wait - to cancel the wait period at any point when closing the source) 
        private Stopwatch swTimePassed = new Stopwatch();                               // stopwatch object to give an exact amount to time passed inbetween loops
        private int dataRetrievalIntervalMs = 0;                                        // interval between the data-buffer retrievals (from NSP) in milliseconds, determines the loop timing
        private int threadLoopDelay = 0;

        private SamplePackageFormat outputFormat  = null;
        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        private int nspSamplingRateGroup = 0;                                           // (set by config) NSP sampling-rate group index 
        private int nspSamplingRate = 0;                                                // NSP sampling-rate
        private int nspFiltering = 0;                                                   // NSP filter index (set by config)
        private double dataRetrievalRateHz = 0.0;                                       // rate of the data-buffer retrievals (from NSP) in Hz
        private int numSamplesPerRetrieval = 0;                                         // theoretical number of samples per retrieval from NSP (based on NSP sampling freq and data-buffer retrievals)
        private int minSamplesPerRetrieval = 0;                                         // the minimum number of samples at each retrieval to be accepted
        private ValueOrder sampleValueOrder = ValueOrder.SampleMajor;                   // the value order/layout
        private double outputSampleRate = 0;                                            // output sample rate of source
        private int numInputChannels = 0;
        private int numOutputChannels = 0;

        private List<SampleGroupInfo> sampleGroups = new List<SampleGroupInfo>(0);      // the (preliminary) sampling-groups
        private List<FilterDesc> filterDescriptions = new List<FilterDesc>(0);          // the (preliminary) filter descriptions/options
        private List<ChannelInfo> analogChannels = new List<ChannelInfo>(0);            // the analog input channel information


        private bool cerelinkOpen = false;                                              // whether the connection to the NSP is made (whether it is still open we cannot know)
        private Object lockCerelink = new Object();                                     // lock cerelink library calls

        // re-referencing variables
        private int rereferencing = -1;										            // (configuration) re-referencing method (0=off, 1=CAR)
        private int[] rereferencingExcludeChannels = new int[0];				        // (configuration) channels (1-based) to exclude from re-referencing
        private int[] rereferencingIncludedChannels = null;				                // channels (0-based) that are included in re-referencing, helper variable

        // time-freq and power transform variables
        private bool transformToPower = false;
        private int transformNumSubsets = 0;                                            // the number of subsets to split an incoming data package into
        private int transformNumSamplesPerSubset = 0;                                   // the number samples per subset

        private int transformModelOrder = 0;
        private double[][] transformInputOutput = null;
        private ARFilter[] arFilters = null;

        private int[] transInputChannels = null;
        int[] transInputChUniq = null;                                           // unique id's of input channels
        private int[] transOutputChannels = null;


        public Blackrock() {

            // add preliminary continuous sampling-rate groups (0-5)
            // these groups seem to be standard according to the SDK but we will actually retrieve and verify these options when we are connected at initialization
            sampleGroups.Add(new SampleGroupInfo(0, "None", 0));
            sampleGroups.Add(new SampleGroupInfo(1, "500 Hz", 500));
            sampleGroups.Add(new SampleGroupInfo(2, "1 kHz", 1000));
            sampleGroups.Add(new SampleGroupInfo(3, "2 kHz", 2000));
            sampleGroups.Add(new SampleGroupInfo(4, "10 kHz", 10000));
            sampleGroups.Add(new SampleGroupInfo(5, "30 kHz", 30000));
            List<string> lstSamplingRate = new List<string>(sampleGroups.Count - 1);
            for (int i = 1; i < sampleGroups.Count; i++)
                lstSamplingRate.Add(sampleGroups[i].label);

            // add preliminary continuous filters (0-12; not 13 to 16, those are reserved)
            // these filters seem to be standard according to the SDK but we will actually retrieve and verify these options when we are connected at initialization
            filterDescriptions.Add(new FilterDesc(0, "None", 0, 0));
            filterDescriptions.Add(new FilterDesc(1, "750Hz High pass", 750, 0));
            filterDescriptions.Add(new FilterDesc(2, "250Hz High pass", 250, 0));
            filterDescriptions.Add(new FilterDesc(3, "100Hz High pass", 100, 0));
            filterDescriptions.Add(new FilterDesc(4, "50Hz Low pass", 0, 50));
            filterDescriptions.Add(new FilterDesc(5, "125Hz Low pass", 0, 125));
            filterDescriptions.Add(new FilterDesc(6, "250Hz Low pass", 0, 250));
            filterDescriptions.Add(new FilterDesc(7, "500Hz Low pass", 0, 500));
            filterDescriptions.Add(new FilterDesc(8, "150Hz Low pass", 0, 150));
            filterDescriptions.Add(new FilterDesc(9, "10Hz-250Hz Band pass", 10, 250));
            filterDescriptions.Add(new FilterDesc(10, "2.5kHz Low pass", 0, 2500));
            filterDescriptions.Add(new FilterDesc(11, "2kHz Low pass", 0, 2000));
            filterDescriptions.Add(new FilterDesc(12, "250Hz-5kHz Band pass", 250, 5000));
            List<string> lstFilters = new List<string>(filterDescriptions.Count - 1);
            for (int i = 0; i < filterDescriptions.Count; i++)
                lstFilters.Add(filterDescriptions[i].label);

            parameters.addParameter<int>(
                "NSPSamplingRate",
                "The rate (in Hz) at which all input channels will be sampled",
                "0", lstSamplingRate.Count.ToString(), "0", lstSamplingRate.ToArray());

            parameters.addParameter<int>(
                "NSPFiltering",
                "Filtering that will be applied to all input channels",
                "0", lstFilters.Count.ToString(), "0", lstFilters.ToArray());
            
            parameters.addParameter<int> (
                "DataRetrievalInterval",
                "Interval (in ms) at which the data that is buffered on NSP will be retrieved.\nRetrieving the data should happen before the buffer on the NSP overflows, which happens after a bit more than 3 seconds, data is lost otherwise.\nIt is recommended to retrieve more often (set a lower interval) at higher sampling rate to prevent data congestion.",
                "50", "3000", "500");


            //
            //
            //
            parameters.addHeader("Re-referencing");
            
            parameters.addParameter<int>(
                "Rerefencing",
                "The method used to re-reference the incoming signals.",
                "0", "1", "0", new string[] { "Off", "Common Average Re-referencing (CAR)"});

            parameters.addParameter<int[]>(
                "RerefencingExcludeChannels",
                "The channels (1-based indexing: 1 to 128) to exclude from the re-reference calculation.\n\nNote: these channels are still re-referenced and passed through as output",
                "0", "", "");

            //
            //
            //
            parameters.addHeader("Signal Transformation");
            
            parameters.addParameter<bool>(
                "TransformToPower",
                "Transform the signal input from the time-frequency domain to power in one or more frequency bins",
                "0");

            parameters.addParameter<int>(
                "TransformNumSubsets",
                "The number of subsets that an incoming set of samples from the NSP (i.e. from one data retrieval) will be split into.\n" +
                "Each subset (of samples) will be transformed separately from the time-frequency domain into power bins.\n\n" +
                "e.g. When sampling at 1000Hz (NSPSamplingRate) at a 500ms interval (DataRetrievalInterval), each retrieval will have 500 samples.\n" +
                "        With this parameter set to 5, there will be 5 subsets of 100 samples, each transformed separately from time-domain to power bins.",
                "1", "", "10");

            parameters.addParameter<int>(
                "TranformModelOrder",
                "Order of prediction model used for time-frequency domain transform.",
                "1", "", "5");

            parameters.addParameter<double[][]>(
               "TransformInputOutput",
               "Frequency bins for which the power in the input signal will be determined.\nEach bin will be outputted as a seperate output channel.\nEach row describes one bin by specifiying the index (1-based) of the input channel, output channel, the lower and upper frequncy limit of the bin, and the amount of evaluations performed in this bin.",
               "", "", "1;1;15;25;10", new string[] { "Input", "Output", "Lower limit", "Upper limit", "Evaluations"  });



            // message
            logger.Info("Source created (version " + CLASS_VERSION + ")");

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Name = "Blackrock source Thread";
            signalThread.Start();

        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public string getClassName() {
            return CLASS_NAME;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public double getInputSamplesPerSecond() {
            return nspSamplingRate;
        }

        /**
         * function to retrieve the number of samples per second
         * 
         * This value could be requested by the main thread and is used to allow parameters to be converted from seconds to samples
         **/
        public double getOutputSamplesPerSecond() {

            // check if the source is not configured yet
            if (!configured) {

                // error message and return 0 samples
                logger.Error("Trying to retrieve the samples per second before the source was configured, first configure the source, returning 0");
                return 0;

            }

            // return the samples per second
            return outputSampleRate;

        }

        public bool configure(out SamplePackageFormat output) {
            output = null;

            // set the number of input channels to be same number as the front-end analog-in channels
            numInputChannels = LIMIT_TO_FIRST_CHANS;

            // retrieve the NSP sampling rate 
            nspSamplingRateGroup = parameters.getValue<int>("NSPSamplingRate") + 1;
            if (nspSamplingRateGroup < 1 || nspSamplingRateGroup > sampleGroups.Count - 1) {
                logger.Error("Unknown NSP sampling-rate group value: " + nspSamplingRateGroup);
                return false;
            }
            nspSamplingRate = sampleGroups[nspSamplingRateGroup].sampleRate;

            // retrieve the NSP filtering
            nspFiltering = parameters.getValue<int>("NSPFiltering");
            if (nspFiltering < 0 || nspFiltering > filterDescriptions.Count - 1) {
                logger.Error("Unknown NSP filtering index value: " + nspFiltering);
                return false;
            }

            // get the data retrieval interval
            int dataRetrievalInterval = parameters.getValue<int>("DataRetrievalInterval");
            if (dataRetrievalInterval < 50) {
                logger.Error("The data retrieval interval should be 50 ms at minimum");
                return false;
            }
            if (dataRetrievalInterval > 3000) {
                logger.Error("The data retrieval interval should be 3000 ms or less to ensure the buffer on the NSP does not overflow and no data is lost");
                return false;
            }
            // TODO: dataRetrievalInterval preferable lower than 1000ms and a factor of 10 (1000 modulus 10)

            // since the loop uses this variable, only apply it when it is validated (above)
            dataRetrievalIntervalMs = dataRetrievalInterval;
            dataRetrievalRateHz = 1000.0 / dataRetrievalIntervalMs;

            // calculate the (theoretical) samples per package (reality might deviate slightly)
            numSamplesPerRetrieval = (int)(sampleGroups[nspSamplingRateGroup].sampleRate / dataRetrievalRateHz);      // calculated based on sample rate / dataRetrievalRateHz
            if (numSamplesPerRetrieval <= 0) {
                logger.Error("Number of samples per package cannot be 0, adjust the NSPSamplingRate or DataRetrievalInterval");
                return false;
            }
            if (numSamplesPerRetrieval > 65535) {
                logger.Error("Number of samples per package cannot be higher than 65535");
                return false;
            }

            // determine the minimum number of samples that are expected from the NSP in order to be sent through
            minSamplesPerRetrieval = (int)(numSamplesPerRetrieval * MIN_PERCENTAGE_SAMPLES_EXPECTED);

            // retrieve the sample value order
            //sampleValueOrder = (parameters.getValue<int>("ValueOrder") == 0 ? ValueOrder.ChannelMajor : ValueOrder.SampleMajor);
            sampleValueOrder = ValueOrder.SampleMajor;

            // register to include the source-input timestamps
            Data.registerSourceInputTime(true);

            // log input as sourceinput
            for (int i = 0; i < numInputChannels; i++)
                Data.registerSourceInputStream(("Br_Input_Ch" + (i + 1)), numSamplesPerRetrieval, dataRetrievalRateHz);


            //
            //
            //

            // retrieve the re-referencing method
            rereferencing = parameters.getValue<int>("Rerefencing");

            // retrieve the re-referencing excluded channels
            if (rereferencing > 0) {
                
                rereferencingExcludeChannels = parameters.getValue<int[]>("RerefencingExcludeChannels");
                for (int i = 0; i < rereferencingExcludeChannels.Length; i++) {
                    if (rereferencingExcludeChannels[i] < 1 || rereferencingExcludeChannels[i] > LIMIT_TO_FIRST_CHANS) {
                        logger.Error("The RerefencingExcludeChannels parameter contains a channel-index (" + rereferencingExcludeChannels[i] + ") that is not between 1 and " + LIMIT_TO_FIRST_CHANS);
                        return false;
                    }
                    for (int j = 0; j < rereferencingExcludeChannels.Length; j++) {
                        if (i != j && rereferencingExcludeChannels[i] == rereferencingExcludeChannels[j]) {
                            logger.Error("The RerefencingExcludeChannels parameter contains the same channel (" + rereferencingExcludeChannels[i] + ") more than once");
                            return false;
                        }
                    }
                }

                // ensure not all channels are excluded
                if (rereferencingExcludeChannels.Length == numInputChannels) {
                    logger.Error("All channels are excluded from re-referencing, make sure at least one channel remains included");
                    return false;
                }

            }

            //
            // Transformation and output
            //

            // 
            transformToPower = parameters.getValue<bool>("TransformToPower");
            if (!transformToPower) {
                // output time-freq domain

                // output exactly the same as the input
                numOutputChannels = numInputChannels;
                outputSampleRate = nspSamplingRate;

                // create a sampleformat
                output = new SamplePackageFormat(numOutputChannels, numSamplesPerRetrieval, dataRetrievalRateHz, sampleValueOrder);

            } else {
                // output power
                
                // get the number of subsets that the incoming samples from the NSP will be split into
                transformNumSubsets = parameters.getValue<int>("TransformNumSubsets");
                if (transformNumSubsets < 1) {
                    logger.Error("The number of subsets for transformation cannot be lower than 1, adjust the TransformNumSubsets parameter");
                    return false;
                }
                if (transformNumSubsets > numSamplesPerRetrieval) {
                    logger.Error("The number of subsets for transformation cannot be higher than the number of incoming samples per NSP-package (" + numSamplesPerRetrieval + "), adjust the TransformNumSubsets parameter");
                    return false;
                }

                // calculate the number of samples per subset to transform
                transformNumSamplesPerSubset = numSamplesPerRetrieval / transformNumSubsets;
                if (transformNumSamplesPerSubset < 1) {
                    logger.Error("The number of samples per subset (i.e. after splitting the a retrieval/package) cannot be lower than 1, adjust the NSPSamplingRate, DataRetrievalInterval or TransformNumSubsets parameter");
                    return false;
                }

                // transfer the model-order
                transformModelOrder = parameters.getValue<int>("TranformModelOrder");
                if (transformModelOrder < 1 || transformNumSamplesPerSubset < transformModelOrder) {
                    logger.Error("Transform model order must be at least 1 and lower than the number of samples per subset, in this case " + transformNumSamplesPerSubset.ToString() + ".");
                    return false;
                }

                // calculate the output sample rate
                outputSampleRate = dataRetrievalRateHz * transformNumSubsets;

                // transfer input-output information
                transformInputOutput = parameters.getValue<double[][]>("TransformInputOutput");

                // if at least one output is defined, retrieve needed information on input and output channels and get maximal value of defined output channels
                if (transformInputOutput.Length >= 1) {

                    // init vars
                    transInputChannels = new int[transformInputOutput[0].Length];
                    transOutputChannels = new int[transformInputOutput[0].Length];

                    // cycle through rows and retrieve information
                    for (int i = 0; i < transformInputOutput[0].Length; i++) {
                        transInputChannels[i] = (int)transformInputOutput[0][i];
                        transOutputChannels[i] = (int)transformInputOutput[1][i];
                        numOutputChannels = Math.Max(numOutputChannels, (int)transformInputOutput[1][i]);  
                    }
                } else {
                    logger.Error("No output channels were defined according to the TransformInputOutput parameter");
                    return false;
                }
			
                // if at least one bin is fully defined
                if (transformInputOutput[0].Length >= 1 && transformInputOutput.Length >= 5) {

                    // get the amount of different input channels defined and sort ascending
                    transInputChUniq = transInputChannels.unique();
                    Array.Sort(transInputChUniq);

                    // create array of ARFilters, equal to maximum id of input channels
                    arFilters = new ARFilter[transInputChUniq[transInputChUniq.Length - 1]];

                    // for each unique input channel, retrieve necessary information to create ARFilter
                    for (int ch = 0; ch < transInputChUniq.Length; ch++) {

                        // find all indices in inputOutput matrix for this input channel
                        int[] indices = Extensions.findIndices(transInputChannels, transInputChUniq[ch]);

                        // init vars to hold relevant information
                        int[] evalPerbin = new int[indices.Length];
                        double[] lowerLimitBin = new double[indices.Length];
                        double[] upperLimitBin = new double[indices.Length];

                        // transfer relevant information for construction of ARFilter
                        for (int i = 0; i < indices.Length; i++) {
                            lowerLimitBin[i] = transformInputOutput[2][indices[i]];
                            upperLimitBin[i] = transformInputOutput[3][indices[i]];
                            evalPerbin[i] = (int)transformInputOutput[4][indices[i]];
                        }

                        // construct ARFilter and store in correct index (equal to input channel id) in array of ARFilters, minus one after lookup because unput channels are user-given and tehrefore 1-based
                        arFilters[transInputChUniq[ch] - 1] = new ARFilter(nspSamplingRate, transformModelOrder, evalPerbin, lowerLimitBin, upperLimitBin);
                    }
                } else {
                    logger.Error("At least one bin must be defined in Input Output matrix.");
                    return false;
                }

                // create a sampleformat
                output = new SamplePackageFormat(numOutputChannels, transformNumSamplesPerSubset, dataRetrievalRateHz, sampleValueOrder);

            }

            // store a reference to the output format
            outputFormat = output;

            // print configuration
            printLocalConfiguration();

            // flag as configured
            configured = true;

            // return success
            return true;

        }

        private void printLocalConfiguration() {

            // debug output
            logger.Debug("--- Source configuration: " + CLASS_NAME + " ---");
            logger.Debug("NSP Input channels: " + numInputChannels);
            logger.Debug("NSP sampling-rate group index: " + nspSamplingRateGroup);
            logger.Debug("NSP sampling-rate: " + nspSamplingRate + "Hz");
            logger.Debug("NSP filtering: " + filterDescriptions[nspFiltering].label);
            logger.Debug("Data retrieval interval: " + dataRetrievalIntervalMs + "ms    (= " + dataRetrievalRateHz + "Hz)");
            logger.Debug("Expected #samples per retrieval: " + numSamplesPerRetrieval);
            logger.Debug("Minimum #samples per retrieval: " + minSamplesPerRetrieval);
            logger.Debug("Re-referencing: " + ((rereferencing > 0) ? "on": "off"));
            if (rereferencing > 0) {
                logger.Debug("    Excluded channels: " + string.Join(",", rereferencingExcludeChannels));
            }
            logger.Debug("Transform time-freq to power: " + (transformToPower ? "on": "off"));
            if (transformToPower) {
                logger.Debug("    Number of subsets/splits: " + transformNumSubsets);
                logger.Debug("    Estimated #samples per subset/split: " + transformNumSamplesPerSubset);
                logger.Debug("    Model order: " + transformModelOrder);
                // TODO: transform input-output channels + settings
            }
            logger.Debug("Output channels: " + outputFormat.numChannels);
            logger.Debug("Output estimated package-rate: " + outputFormat.packageRate + "Hz");
            logger.Debug("Output estimated #samples-per-package: " + outputFormat.numSamples);

        }

        public bool initialize() {

            lock (lockCerelink) {

                // initialize libary
                try {
                    if (!Cerelink.initialize()) {
                        // message is already given by cerelink
                        initialized = false;
                        return false;
                    }
                } catch (DllNotFoundException ex) {
                    // TODO: DLL not found:
                    // Offer user dialog to search for DLL file, if found:
                    //   - add to environment for permanent fix: Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\hardware-intermediate-v1");
                    //   - temp fix, load this runtime (use addLibraryPath in wrapper)
                    initialized = false;
                    return false;
                }

                // if the library is still open, close it first
                if (cerelinkOpen)   Cerelink.close();

                // open the library
                cerelinkOpen = Cerelink.open();
                if (!cerelinkOpen) {
                    initialized = false;
                    return false;
                }
                

                // 
                // TODO: library version check with Cerelink.getVersion
                //


                // retrieve, verify and replace each of the sampling-groups
                // (0=reserved for no sampling, skip; testing with NSP and matlab doc teaches us the maximum is actually 6)
                for (int i = 1; i < 6; i++) {

                    // retrieve
                    Tuple<char[], UInt32, UInt32> groupInfo = Cerelink.getSampleGroupInfo((UInt32)i);
                    if (groupInfo == null) {
                        logger.Error("Error while retrieving information on sampling-group (" + i + ")");
                        initialized = false;
                        return false;
                    }
                    SampleGroupInfo info = new SampleGroupInfo(i, groupInfo);

                    // verify
                    SampleGroupInfo preInfo = sampleGroups[i];
                    if (preInfo.index != info.index || preInfo.sampleRate != info.sampleRate) {
                        logger.Error("Mismatch between pre-definition and information of sampling-group (" + i + ") from library");
                        initialized = false;
                        return false;
                    }

                    // if sample group from library has no name, then use the one from the pre-definition
                    if (info.label.Length == 0)
                        info.label = preInfo.label;

                    // replace in sampling-group list
                    sampleGroups.RemoveAt(i);
                    sampleGroups.Insert(i, info);

                    //logger.Debug(info.label + " (index: " + info.index + ", rate: " + info.sampleRate + ")");

                }
            
                // retrieve, verify and replace each of the filters
                // (0=reserved for no filtering, skip; testing with NSP and matlab doc teaches us the maximum-index is actually 11) 
                for (int i = 1; i < 12; i++) {

                    // retrieve
                    Cerelink.cbFILTDESC? filtDesc = Cerelink.getFilterDesc((ushort)(i));
                    if (!filtDesc.HasValue) {
                        logger.Error("Error while retrieve the description of filter (" + i + ")");
                        initialized = false;
                        return false;
                    }
                    FilterDesc desc = new FilterDesc(i, filtDesc.Value);

                    // verify
                    FilterDesc preDesc = filterDescriptions[i];
                    if (preDesc.index != desc.index || preDesc.hpfreqHz != desc.hpfreqHz || preDesc.lpfreqHz != desc.lpfreqHz) {
                        logger.Error("Mismatch between pre-definition and description of filter (" + i + ") from library");
                        initialized = false;
                        return false;
                    }

                    // if filter from library has no name, then use the one from the pre-definition
                    if (desc.label.Length == 0)
                        desc.label = preDesc.label;

                    // replace in filter list
                    filterDescriptions.RemoveAt(i);
                    filterDescriptions.Insert(i, desc);

                    //logger.Debug(desc.label + " (index: " + desc.index + 
                    //                          ", HP Filter Type: " + desc.hpTypeStr + ", HP Corner Freq: " + desc.filtDesc.hpfreq + " " + desc.hpfreqHz + "Hz, HP filter Order: " + desc.filtDesc.hporder + 
                    //                          ", LP Filter Type: " + desc.lpTypeStr + ", LP Corner Freq: " + desc.filtDesc.lpfreq + " " + desc.lpfreqHz + "Hz, LP filter Order: " + desc.filtDesc.lporder + ")");

                }


                //
                // set the configuration of all channels (according to the configuration)
                //

                // reset list
                analogChannels.Clear();


                // loop over the analog channels
                // Note: 0-255 = FE analog-in, 256-271 = analog-in; 272-275=analog out; 276-277=audio out; 278=digital-in; 279: serial1; 280-283=digital-out;
                //       We will set and enable the FE analog-in and disable non-FE analog-in. So set the first 272 channels, and leave the rest of the channels as they were
                for (int i = 0; i < (NUM_FE_ANALOG_IN_CHANS + NUM_ANALOG_IN_CHANS); i++) {

                    // retrieve the channel configuration
                    Cerelink.cbPKT_CHANINFO? retChanInfo = Cerelink.getChannelConfig((ushort)(i + 1));
                    if (!retChanInfo.HasValue) {
                        logger.Error("Error while retrieve channel configuration (" + i + ")");
                        initialized = false;
                        return false;
                    }
                    Cerelink.cbPKT_CHANINFO chanInfo = retChanInfo.Value;

                    // disable sorting, spike extraction and raw data streaming
                    Cerelink.setChannelSpikeSorting(ref chanInfo, Cerelink.cbAINPSPK_NOSORT);
                    Cerelink.setChannelSpikeExtraction(ref chanInfo, false);
                    Cerelink.setChannelRawDataStreaming(ref chanInfo, false);
                    chanInfo.spkfilter = 0;
                    chanInfo.spkgroup = 0;

                    // check if within first x channels
                    if (i < LIMIT_TO_FIRST_CHANS) {

                        // check type of analog-in channel
                        if (Cerelink.isChannelFEAnalogIn(ref chanInfo)) {
                            // front-end analog-in channel

                            // stream by setting the sampling group, also set filter
                            chanInfo.smpgroup = (uint)nspSamplingRateGroup;
                            chanInfo.smpfilter = (uint)nspFiltering;
                    
                        } else {
                            // non-front-end analog-in channel

                            // do not stream, by setting the sampling group to 0, also set filter to 0
                            chanInfo.smpgroup = 0;
                            chanInfo.smpfilter = 0;

                        }

                    } else {
                        // do not stream, by setting the sampling group to 0, also set filter to 0
                        chanInfo.smpgroup = 0;
                        chanInfo.smpfilter = 0;
                    }

                    // set the channel configuration
                    if (!Cerelink.setChannelConfig((ushort)(i + 1), chanInfo)) {
                        initialized = false;
                        return false;
                    }


                    // add to list of channels
                    ChannelInfo info = new ChannelInfo(i, chanInfo);
                    analogChannels.Add(info);

                    //logger.Debug(info.label + " (index: " + info.index + ", bank: " + info.bank + ", pin:" + info.pin + 
                    //                          ", samplingGroup:" + info.samplingGroup + ", filterId:" + info.filterId + 
                    //                          ", isAnalogIn:" + info.isAnalogIn + ", isFEAnalogIn:" + info.isFEAnalogIn +
                    //                          ", isDigIn:" + Cerelink.isChannelDigitalIn(ref chanInfo) + ", isSerial:" + Cerelink.isChannelSerial(ref chanInfo) + ")");

                }

            }   // end of lockCerelink

            // if re-referencing, create a array of channels that are included
            if (rereferencing > 0) {
                
                List<int> includedChannels = new List<int>();
                for (int i = 0; i < LIMIT_TO_FIRST_CHANS; i++) {
                    bool isFound = false;
                    for (int j = 0; j < rereferencingExcludeChannels.Length; j++) {
                        if ((i + 1) == rereferencingExcludeChannels[j]) {
                            isFound = true;
                            break;
                        }
                    }
                    if (!isFound)
                        includedChannels.Add(i);    // deliberately 0-based here 
                }
                rereferencingIncludedChannels = includedChannels.ToArray();

            }

            // flag as initialized and return success
            initialized = true;
            return true;

        }

	    /**
	     * Start
	     */
        public void start() {

            // check if configured and the source was initialized
            if (!configured || !initialized) {
                return;
            }

            lock(lockStarted) {

                // check if the acquisition was not already started
                if (started)     return;

                lock (lockCerelink) {
                    if (cerelinkOpen) {

                        // (re)start continuous data buffering (with absolute timing, Uint16)
                        bool configResult = Cerelink.setTrialConfig(true, false, true, true);
                        if (configResult) {
                            // since trialconfig sets some acquisition properties, only set as
                            // started if the library is open and setTrialConfig was successfull

                            // start acquisition
                            started = true;
                            logger.Info("Data buffering enabled");

                        } else {
                            logger.Error("An error occurred while (re)starting data buffering on the NSP");
                        }

                    }
                }   // end of lock

            }

            // interrupt the loop wait, allowing the loop to continue (in case it was waiting the noproc interval)
            // causing an immediate start and switching to the processing waittime
            loopManualResetEvent.Set();

        }

	    /**
	     * Stop
	     */
	    public void stop() {

            // if not initialized than nothing needs to be stopped
            if (!initialized)	return;

            // stop generating
            lock (lockStarted) {

                // always stop acquisition (regardless of whether we can make the NSP stop)
                started = false;

                lock (lockCerelink) {
                    if (cerelinkOpen) {

                        // Stop data buffering
                        if (Cerelink.setTrialConfig(true, false, false, true))
                            logger.Info("Data buffering disabled");
                        else
                            logger.Error("An error occurred while stopping data buffering on the NSP");

                    }
                }

            }

            // interrupt the loop wait, allowing the loop to continue (in case it was waiting the proc interval)
            // switching to the no-processing waittime
            loopManualResetEvent.Set();

        }

	    /**
	     * Returns whether the signalgenerator is generating signal
	     * 
	     * @return Whether the signal generator is started
	     */
	    public bool isStarted() {
            lock(lockStarted) {
                return started;
            }
	    }


	    /**
	     * 
	     */
	    public void destroy() {
        
            // stop source
            // Note: At this point stop will probably have been called from the mainthread before destroy, however there is a slight
            // chance that in the future someone accidentally will put something in the configure/initialize that should have
            // actually been put in the start. If start is not called in the mainthread, then stop will also not be called at the
            // modules. For these accidents we do an extra stop here.
		    stop();

            // if the library is open, close it
            lock (lockCerelink) {
                if (cerelinkOpen)
                    Cerelink.close();
            }

            // flag the thread to stop running (when it reaches the end of the loop)
            running = false;

            // interrupt the wait in the loop
            // (this is done because if the sample-package rate is low, we might have to wait for a long time for the thread to end)
            loopManualResetEvent.Set();

            // wait until the thread stopped
            // try to stop the main loop using running
            int waitCounter = 500;
            while (signalThread.IsAlive && waitCounter > 0) {
                Thread.Sleep(10);
                waitCounter--;
            }

            // clear the thread reference
            signalThread = null;

	    }
	
	    /**
	     * Returns whether the source thread is still running
	     * Note: 'running' just determines whether the source thread is running; start(), stop() and 
         * isStarted() manage whether samples are generated and forwarded into Palmtree
	     * 
	     * @return Whether the source thread is running
	     */
	    public bool isRunning() {
		    return running;
	    }
        
        /**
	     * Source running thread
	     */
        private void run() {

            // log message
            logger.Debug("Thread started");

            // set an initial start for the stopwatch
            swTimePassed.Start();
            
            // loop while running
            while (running) {

                lock (lockStarted) {

			        // check if we are retrieving from NSP
			        if (started) {

                        // acquire data from the NSP
                        acquireData();

                    } // end of started conditional
                }  // end of started-lock
                
			    // if still running then wait to allow other processes
			    if (running) {
                    
                    // check if we are generating
                    // (note: we deliberately do not lock the started variable here, the locking will delay/lock out 'start()' during the wait here
                    //  and if these are not in sync, the worst thing that can happen is that it does waits one loop extra, which is no problem)
                    if (started) {

                        threadLoopDelay = dataRetrievalIntervalMs;     // choose not to correct for elapsed ms. This result in a slightly higher wait time, which at lower Hz comes closer to the expected samplecount per second
                        //threadLoopDelay = dataRetrievalIntervalMs - (int)swTimePassed.ElapsedMilliseconds;

                        // wait for the remainder of the sample-package interval to get as close to the sample-package rate as possible (if there is a remainder)
                        if (threadLoopDelay >= 0) {
                                
                            // reset the manual reset event, so it is sure to block on the next call to WaitOne
                            // 
                            // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                            // using AutoResetEvent this will cause it to skip the next WaitOne call
                            loopManualResetEvent.Reset();

                            // Sleep wait
                            loopManualResetEvent.WaitOne(threadLoopDelay);      // using WaitOne because this wait is interruptable (in contrast to sleep)
                                
                        }

                    } else {
                            
                        // reset the manual reset event, so it is sure to block on the next call to WaitOne
                        // 
                        // Note: not using AutoResetEvent because it could happen that .Set is called while not in WaitOne yet, when
                        // using AutoResetEvent this will cause it to skip the next WaitOne call
                        loopManualResetEvent.Reset();

                        // Sleep wait
                        loopManualResetEvent.WaitOne(threadLoopDelayNoProc);      // using WaitOne because this wait is interruptable (in contrast to sleep)

                    }

                    // restart the timer to measure the loop time
                    swTimePassed.Restart();

                }
            }       // end of 'running' loop

            // log message
            logger.Debug("Thread stopped");

        }

        private void acquireData() {

            // lock and acquire samples
            lock (lockCerelink) {
                if (cerelinkOpen) {

                    UInt32 time;
                    Int16[][] data;

                    // retrieve data from NSP and check
                    if (!Cerelink.fetchTrialContinuousData(true, out time, out data)) {
                        // error message will already be logged in cerelink
                        return;
                    }
                    if (data == null) {
                        logger.Error("An empty data-set was received");
                        return;
                    }
                    if (data.Length != numInputChannels) {
                        // unexpected number of channels
                        logger.Error("Data matrix from NSP has different number of channels than expected, skipping set");
                        return;
                    }

                    // determine the number of channel, and
                    // check if at least 95% of expected amount of samples is there
                    // (prevents a start package of just a few samples, and in time-domain the generation of too much data)
                    int numSamples = data[0].Length;
                    if (numSamples < minSamplesPerRetrieval) {
                        logger.Warn("Discarding package with too few samples (most likely a NSP startup package)");
                        return;
                    }

                    //
                    //Console.WriteLine("#samples: " + numSamples);
                    //Console.WriteLine("time: " + time);

                    // initialize an array
                    double[] samples = new double[numInputChannels * numSamples];

                    // transfer values (cast to doubles)
                    // TODO: more efficitient, multiple improvements possible
                    if (sampleValueOrder == ValueOrder.SampleMajor) {
                        for (int iSmpl = 0; iSmpl < numSamples; iSmpl++) {
                            for (int iCh = 0; iCh < numInputChannels; iCh++)
                                samples[iSmpl * numInputChannels + iCh] = data[iCh][iSmpl];

                        }
                    } else {
                        for (int iSmpl = 0; iSmpl < numSamples; iSmpl++) {
                            for (int iCh = 0; iCh < numInputChannels; iCh++)
                                samples[iCh * numSamples + iSmpl] = data[iCh][iSmpl];

                        }
                    }

                    // release original data matrix
                    data = null;

                    // log incoming data as source streams
                    Data.logSourceInputValues(samples, (double)time);


                    //
                    // re-referencing
                    //

                    if (rereferencing == 1 && rereferencingIncludedChannels.Length > 0) {
                        // CAR rereferencing

                        // loop over each sample
                        for (int iSample = 0; iSample < samples.Length; iSample += numInputChannels) {
                            double rerefValue = 0;

                            // loop over the channels (that are included for re-referencing) and add those to the total
                            for (int iRefChan = 0; iRefChan < rereferencingIncludedChannels.Length; iRefChan++)
                                rerefValue += samples[iSample + rereferencingIncludedChannels[iRefChan]];

                            // calculate the average
                            rerefValue /= rereferencingIncludedChannels.Length;

                            // loop over all the channels in the sample and subtract the average
                            for (int iChan = 0; iChan < numInputChannels; iChan++)
                                samples[iSample + iChan] = samples[iSample + iChan] - rerefValue;

                        }

                    }


                    //
                    if (transformToPower) {
                        // transform from freq-time domain to power

                        double[] pwrSamples = transformDataToPower(samples, numSamples);
                        
                        // pass power data to pipeline
                        MainThread.eventNewSample(pwrSamples);
                        

                    } else {
                        // time-freq domain, just pass through to pipeline

                        MainThread.eventNewSample(samples);

                    }

                } // end of cerelink-open
            } // end of cerelink lock

        } // end of acquireData

        private double[] transformDataToPower(double[] samples, int numSamples) {

            double[] pwrSamples = new double[numOutputChannels * transformNumSubsets];

            // split the incoming data
            // Note: use doubles to raise and round so we don't end up with leftover samples
            double dblNumPerPart = (double)numSamples / transformNumSubsets;
            double dblStartIndex = 0;
            
            // ensure actual number of samples is higher than transformModelOrder
            // Note: this is difficult to achieve (if estimated #samples is higher, the actual #samples is lower, and lower than minSamples)
            if (dblNumPerPart < transformModelOrder) {
                logger.Error("The actual number of samples in a subset would be lower than the modelOrder, passing zero dataset");
                Array.Clear(pwrSamples, 0, pwrSamples.Length);
                return pwrSamples;
            }

            // loop over the parts
            for (int part = 0; part < transformNumSubsets; part++) {

                // 
                int startIndex = (int)Math.Round(dblStartIndex);
                int endIndex = (int)Math.Round(dblStartIndex + dblNumPerPart);
                int partSize = endIndex - startIndex;
                
                // process the seperate input channel buffers
                for (int channel = 0; channel < numInputChannels; channel++) {

                    // check if this buffer contains data (+1 because in GUI channels are 1-based)
                    if (Array.IndexOf(transInputChUniq, channel + 1) != -1) {

                        double[] powerSpec = null;

                        if (arFilters[channel] != null) {
                            double[] partData = new double[partSize];

                            int counter = 0;
                            for (int iSmpl = startIndex; iSmpl < endIndex; iSmpl++) {
                                if (sampleValueOrder == ValueOrder.SampleMajor) {
                                    partData[counter] = samples[iSmpl * numInputChannels + channel];
                                } else {
                                    // TODO: just Array.Copy from input array on ChannelMajor
                                    partData[counter] = samples[channel * numSamplesPerRetrieval + iSmpl];
                                }

                                counter++;
                            }

                            // fill buffer of corresponding ARFilter if ARFilter is allowed to run 
                            if (arFilters[channel].AllowRun) arFilters[channel].Data = partData;

                            // determine linearModel on data if ARFilter is allowed to run
                            if (arFilters[channel].AllowRun) arFilters[channel].createLinPredModel();

                            // determine power spectrum if ARFilter is allowed to run
                            if (arFilters[channel].AllowRun) powerSpec = arFilters[channel].estimatePowerSpectrum();

                            // find output channels corresponding to bins in powerSpec
                            int[] indices = Extensions.findIndices(transInputChannels, (channel + 1));

                            // transfer values from powerSpec to correct indices in retSample (minus 1 because values in outputChannels are 1-based because user-input)
                            if (sampleValueOrder == ValueOrder.SampleMajor) {

                                for (int i = 0; i < indices.Length; i++) {
                                    int iCh = transOutputChannels[indices[i]] - 1;
                                    pwrSamples[part * numOutputChannels + iCh] = powerSpec[i];

                                }

                            } else {
                                for (int i = 0; i < indices.Length; i++) {
                                    int iCh = transOutputChannels[indices[i]] - 1;
                                    pwrSamples[iCh * numSamplesPerRetrieval + part] = powerSpec[i];
                                }

                            }

                        } else {

                            // message
                            logger.Error("ARFilter for input channel " + channel + " is not initialized. Check code.");

                        }

                    }

                } // end of channel loop

                dblStartIndex += dblNumPerPart;

            } // end of part loop

            // return the power values
            return pwrSamples;
            
        } // end of transformDataToPower function

    }

    
    public class ChannelInfo {
        public int index;
        public Cerelink.cbPKT_CHANINFO chanInfo;
        public string label;
        public uint bank;
        public uint pin;
        public int samplingGroup;
        public int filterId;
        public bool isAnalogIn;
        public bool isFEAnalogIn;

        public ChannelInfo(int index, Cerelink.cbPKT_CHANINFO chanInfo) {
            this.index              = index;
            this.chanInfo           = chanInfo;
            this.label              = Encoding.UTF8.GetString(Encoding.Default.GetBytes(chanInfo.label)).TrimEnd('\0');
            this.bank               = chanInfo.bank;
            this.pin                = chanInfo.term;
            this.samplingGroup      = (int)chanInfo.smpgroup;
            this.filterId           = (int)chanInfo.smpfilter;
            this.isAnalogIn         = Cerelink.isChannelAnalogIn(ref chanInfo);
            this.isFEAnalogIn       = Cerelink.isChannelFEAnalogIn(ref chanInfo);
        }    
    }

    public class SampleGroupInfo {
        public int index;
        public string label;
        public uint period;
        public int sampleRate;
        public int numChans;

        public SampleGroupInfo(int index, string label, int sampleRate) {
            this.index              = index;
            this.label              = label;
            this.sampleRate         = sampleRate;
        }
        public SampleGroupInfo(int index, Tuple<char[], UInt32, UInt32> groupInfo) {
            this.index              = index;
            this.label              = Encoding.UTF8.GetString(Encoding.Default.GetBytes(groupInfo.Item1)).TrimEnd('\0');
            this.period             = groupInfo.Item2;
            this.sampleRate         = (int)(30000.0 / (double)this.period);
            this.numChans           = (int)groupInfo.Item3;
        }    
    }

    public class FilterDesc {
        public int index;
        public Cerelink.cbFILTDESC filtDesc;
        public string label;
        public string hpTypeStr;
        public int hpfreqHz;            // high-pass corner frequency in Hertz (digital according to Central, it shows different value for analog)
        public string lpTypeStr;
        public int lpfreqHz;            // low-pass corner frequency in Hertz  (digital according to Central, it shows different value for analog)

        public FilterDesc(int index, string label, int hpfreqHz, int lpfreqHz) {
            this.index              = index;
            this.label              = label;
            this.hpfreqHz           = hpfreqHz;
            this.lpfreqHz           = lpfreqHz;
        }
        public FilterDesc(int index, Cerelink.cbFILTDESC filtDesc) {
            this.index              = index;
            this.filtDesc           = filtDesc;
            this.label              = Encoding.UTF8.GetString(Encoding.Default.GetBytes(filtDesc.label)).TrimEnd('\0');
            this.hpTypeStr          = Cerelink.getFilterTypeString(filtDesc.hptype);
            this.hpfreqHz           = (int)(filtDesc.hpfreq / 1000);
            this.lpTypeStr          = Cerelink.getFilterTypeString(filtDesc.lptype);
            this.lpfreqHz           = (int)(filtDesc.lpfreq / 1000);
        }    
    }
}
