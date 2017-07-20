using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core;
using UNP.Core.DataIO;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class FilterBase {

        protected int CLASS_VERSION = -1;

        protected string filterName = "";
        protected Logger logger = null;
        protected Parameters parameters = null;

        protected bool mEnableFilter = false;                   // Filter is enabled or disabled
        protected bool mLogDataStreams = false;                 // stores whether the initial configuration has the logging of data streams enabled or disabled
        protected bool mLogDataStreamsRuntime = false;          // stores whether during runtime the data streams should be logged (if it was on, then it can be switched off, resulting in 0's being logged)
        protected bool mEnableDataVisualization = false;         // stores whether data visualization is enabled or disabled. This is a local copy of the setting from Globals (originating from the Data class), set during configuration of the filter

        protected int inputChannels = 0;
        protected int outputChannels = 0;

        public string getName() {
            return filterName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public int getClassVersion() {
            return CLASS_VERSION;
        }

        public void configureOutputLogging(string prefix, SampleFormat output) {

            // retrieve and prepare the logging of streams
            mLogDataStreams = parameters.getValue<bool>("LogDataStreams");
            mLogDataStreamsRuntime = mLogDataStreams;
            if (mLogDataStreams) {

                // register the streams
                for (int channel = 0; channel < outputChannels; channel++)
                    Data.registerDataStream((prefix + "Output_Ch" + (channel + 1)), output);

            }

            // retrieve and prepare the visualization of streams
            mEnableDataVisualization = Globals.getValue<bool>("EnableDataVisualization");
            if (mEnableDataVisualization) {

                // register the streams to visualize
                for (int channel = 0; channel < outputChannels; channel++) {
                    Data.registerVisualizationStream((prefix + "Output_Ch" + (channel + 1)), output);

                    // debug
                    //logger.Warn((prefix + "Output_Ch" + (channel + 1)));

                }
            }

        }

        // standard function to process the output logging
        public void processOutputLogging(double[] output) {

            // check if the streams should be logged to a file (initial setting) or visualization is allowed 
            if (mLogDataStreams || mEnableDataVisualization) {

                for (int channel = 0; channel < outputChannels; ++channel) {

                    // check if logging of the steams was initially allowed
                    if (mLogDataStreams) {

                        // check if the logging of streams is needed/allowed during runtime
                        if (mLogDataStreamsRuntime) {
                            // enabled initially and at runtime

                            // log values
                            Data.logStreamValue(output[channel]);

                        } else {
                            // enabled initially but not at runtime

                            // log zeros
                            Data.logStreamValue(0.0);

                        }

                    }

                    // check if the data should be visualized
                    if (mEnableDataVisualization) {
                        
                        // log values
                        Data.logVisualizationStreamValue(output[channel]);

                        // debug
                        //logger.Warn(("Output_Ch" + (channel + 1)));

                    }

                }   // end for loop

            }

        }   // end processOutputLogging


    }

}
