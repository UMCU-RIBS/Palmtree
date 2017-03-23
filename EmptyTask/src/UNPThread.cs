using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UNP.filters;
using UNP.helpers;
using UNP.sources;
using UNP.views;

namespace UNP {
    


    public class UNPThread {

        private static Logger logger = LogManager.GetLogger("UNP");

	    public const int threadLoopDelay = -1;		                        // thread loop delay (1000ms / 5 run times per second = rest 200ms)
        private bool running = true;				                        // flag to define if the experiment thread is still running (setting to false will stop the experiment thread) 

        private Source source = null;                                       //
        private List<Filter> filters = new List<Filter>();                  //

        const int sampleBufferSize = 10000;                                 // the size (and maximum samples) of the sample buffer/que 
        private double[][] sampleBuffer = new double[sampleBufferSize][];   // the sample buffer in which samples are queud
        private int sampleBufferAddIndex = 0;                               // the index where in the (ring) sample buffer the next sample will be added
        private int sampleBufferReadIndex = 0;                              // the index where in the (ring) sample buffer of the next sample that should be read
        private int numberOfSamples = 0;                                    // the number of added but unread samples in the (ring) sample buffer

        //private IView view = null;                                      // reference to the view, used to pull information from and push commands to

        /**
         * Pipeline constructor
         */
        public UNPThread() {


	    }


        public void run() {

            // log message
            logger.Debug("Thread started");


            // create a source
            source = new GenerateSignal(this);

            // create filters
            filters.Add(new TimeSmoothingFilter());
            filters.Add(new AdaptationFilter());
            filters.Add(new ThresholdClassifierFilter());
            filters.Add(new ClickTranslatorFilter());

            // create the views




            // (optional/debug) load the parameters
            // (the parameter list has already been filled by the constructors of the source, filters and views)
            Parameters.setParameterValue("SourceChannels", "2");
            Parameters.setParameterValue("SF_EnableFilter", "1");
            Parameters.setParameterValue("SF_WriteIntermediateFile", "0");
            Parameters.setParameterValue("AF_EnableFilter", "1");
            Parameters.setParameterValue("AF_WriteIntermediateFile", "0");


            // configure source (this will also give the output format information)
            SampleFormat tempFormat = null;
            source.configure(out tempFormat);

            // configure the filters
            for (int i = 0; i < filters.Count(); i++) {

                // create a local variable to temporarily store the output format of the filter in
                // (will be given in the configure step)
                SampleFormat outputFormat = null;

                // configure the filter
                filters[i].configure(ref tempFormat, out outputFormat);

                // store the output filter as the input filter for the next loop (filter)
                tempFormat = outputFormat;

            }

            // configure the view


            // initialize source, filter and view
            source.initialize();
            for (int i = 0; i < filters.Count(); i++) filters[i].initialize();

            // start source, filter and view
            source.start();
            for (int i = 0; i < filters.Count(); i++) filters[i].start();

            long time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            long counter = 0;


            double[] sample = null;

            // loop while running
            while (running) {

                if (Stopwatch.GetTimestamp() > time) {

                    logger.Info("numberOfSamples: " + numberOfSamples);
                    logger.Info("tick counter: " + counter);
                    //logger.Info("sample[0]: " + sample[0]);

                    
                    counter = 0;
                    time = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                }
                counter++;




                // see if there samples in the que, pick the sample for processing
                sample = null;
                lock(sampleBuffer.SyncRoot) {

                    if (numberOfSamples > 0) {

                        // retrieve the sample to process (pointer to from array)
                        sample = sampleBuffer[sampleBufferReadIndex];

                        // set the read index to the next item
                        sampleBufferReadIndex++;
                        if (sampleBufferReadIndex == sampleBufferSize) sampleBufferReadIndex = 0;

                        // decrease the itemcounter as it will be processed (to make space for another item in the samples buffer)
                        numberOfSamples--;

                    }

                }

                // check if there is a sample to process
                if (sample != null) {

                    double[] output = null;

                    // process the sample
                    for (int i = 0; i < filters.Count(); i++) {

                        filters[i].process(sample, out output);
                    }

                    sample = output;
                    output = null;

                }


			    // if still running then sleep to allow other processes
			    if (running && threadLoopDelay != -1 && numberOfSamples == 0) {
                    Thread.Sleep(threadLoopDelay);
			    }

            }

		    // stop and destroy the source
		    if (source != null) {
			    source.destroy();
                source = null;
		    }

            for (int i = 0; i < filters.Count(); i++) filters[i].destroy();


            // log message
            logger.Debug("Thread stopped");

        }


        public void eventNewSample(double[] sample) {
            
            lock(sampleBuffer.SyncRoot) {

                if (numberOfSamples == sampleBufferSize) {

                    // discard sample
                    return;

                }

                // set a temp value
                sample[0] = sampleBufferAddIndex;


                // add the sample at the pointer location, and increase the pointer (or loop the pointer around)
                sampleBuffer[sampleBufferAddIndex] = sample;
                sampleBufferAddIndex++;
                if (sampleBufferAddIndex == sampleBufferSize) sampleBufferAddIndex = 0;

                // increase the counter that holds the number of elements of the array
                numberOfSamples++;

            }

        }


        /**
         * event called when the GUI is dispatched and closed
         */
        public void eventGUIClosed() {

            // stop the program from running
            running = false;

        }


    }

}
