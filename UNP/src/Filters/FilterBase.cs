using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Filters {

    public class FilterBase {

        protected string filterName = "";
        protected Logger logger = null;
        protected Parameters parameters = null;

        protected bool mEnableFilter = false;                   // Filter is enabled or disabled
        protected bool mLogSampleStreams = false;               // stores whether the initial configuration has the logging of sample streams enabled or disabled
        protected bool mLogSampleStreamsRuntime = false;        // stores whether during runtime the sample streams should be logged    (if it was on, then it can be switched off, resulting in 0's being logged)
        protected bool mAllowDataVisualization = false;         // stores whether data visualization is enabled or disabled. This is a local copy of the setting from Globals (originating from the Data class), set during configuration of the filter

        protected uint inputChannels = 0;
        protected uint outputChannels = 0;

        public string getName() {
            return filterName;
        }

        public Parameters getParameters() {
            return parameters;
        }

        public void configureInputLogging(string prefix, SampleFormat input) {

            // retrieve and prepare the visualization of streams
            mAllowDataVisualization = Globals.getValue<bool>("AllowDataVisualization");
            if (mAllowDataVisualization) {

                // register the streams to visualize
                for (int i = 0; i < outputChannels; i++)
                    Data.RegisterVisualizationStream((prefix + "Input_Ch" + (i + 1)), input);

            }

            // retrieve and prepare the logging of sample streams
            mLogSampleStreams = parameters.getValue<bool>("LogSampleStreams");
            mLogSampleStreamsRuntime = mLogSampleStreams;
            if (mLogSampleStreams) {

                // register the streams
                for (int i = 0; i < outputChannels; i++)
                    Data.RegisterSampleStream((prefix + "Input_Ch" + (i + 1)), input);

            }

        }

        // standard function to process the input loggin
        public void processInputLogging(double[] input) {

            // check if the sample streams should be logged to a file (initial setting) or visualization is allowed 
            if (mLogSampleStreams || mAllowDataVisualization) {

                for (uint channel = 0; channel < inputChannels; ++channel) {

                    // check if the logging of sample streams is needed/allowed during runtime
                    if (mLogSampleStreamsRuntime) {
                        // enabled initially and at runtime

                        // log values
                        Data.LogStreamSample(input[channel]);

                    } else {
                        // enabled initially but not at runtime

                        // log zeros
                        Data.LogStreamSample(0.0);

                    }

                    // check if the data should be visualized
                    if (mAllowDataVisualization) {

                        // log values
                        Data.LogVisualizationSample(input[channel]);

                    }

                }   // end for loop

            }

        }   // end processInputLogging


    }

}
