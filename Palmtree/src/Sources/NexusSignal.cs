/**
 * The NexusSignal class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * Adapted from:        Erik Aarnoutse              (E.J.Aarnoutse@umcutrecht.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

//#define DEBUG_TESTDATA                                             // Output test data instead of data from the Nexus, even when connection with Nexus is timed-out

using Microsoft.Win32.SafeHandles;
using NLog;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;
#if (DEBUG_TESTDATA)
    using static Palmtree.Core.Helpers.DebugHelper;
#endif

namespace Palmtree.Sources {

    /// <summary>
    /// The <c>NexusSignal</c> class.
    /// 
    /// ...
    /// </summary>
    public class NexusSignal : ISource {

        // fundamentals
        private const string CLASS_NAME = "NexusSignal";
        private const int CLASS_VERSION = 4;

        private const double OUTPUT_SAMPLE_RATE = 5;                                      // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)
        private const int NEXUS_POWERMODE_SAMPLES_PER_PACKAGE = 1;
        private const int NEXUS_POWERMODE_CHANNELS_PER_PACKAGE = 5;
        private const int NEXUS_POWERMODE_SIGNALFREQUENCY = 5;
        private const int NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE = 40;
        private const int NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE = 3;
        private const int NEXUS_TIMEMODE_SIGNALFREQUENCY = 200;      // frequency of the time domain signal provided by the Nexus

        private const int DEFAULT_BAUD = SerialPortNet.CBR_115200;   // default Baud-Rate is 112k
        private const int COM_TIMEOUT = 3000;                        // 3000 ms trasmission timeout
        private const int PACKET_BUFFER_SIZE = 128;                  // packet buffer size which is large enough for P6 // (i.e. NEXUS-1 STS time-domain) packages of 120 values

        private const int PACKET_START = 0;
        private const int PACKET_FINISHED = 8;                       // packet-state for completed packet

        // debug variables,used in debug mode to create test data 
        #if (DEBUG_TESTDATA)
            private DebugSignalType debSig = DebugSignalType.Rand;      // type of debug data: random of sinusoid-based
            private double[] testData = null;
            ushort[] testDataShort = null;
            private double samplingFrequency = 200;                     // sampling frequency of test signal, in case debugSignalType is Sinus. 
            private double[][] amps = new double[2][] {                 // amplitudes of sinusoids in test signal, in case debugSignalType is Sinus. Every amplitude is (re)used as one channel, until all input channels are filled.
                new double[] { 10, 20, 30 },                                      
                new double[] { 40, 10, 20 }
            };
            private double[][] frequencies = new double[2][] {          // frequencies of sinusoids in test signal, in case debugSignalType is Sinus. Every frequency is (re)used as one channel, until all input channels are filled.
                new double[] { 20, 60, 38 } ,
                new double[] { 40, 32, 5 }                                                                           
            };
        #endif

        // 
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);
        private static Parameters parameters = ParameterManager.GetParameters(CLASS_NAME, Parameters.ParamSetTypes.Source);
        
        private Thread signalThread = null;                                             // the source thread
        private bool running = true;					                                // flag to define if the source thread should be running (setting to false will stop the source thread)

        private bool configured = false;
        private bool initialized = false;
        private bool started = false;				                                    // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        // 
        private string comPort = "";
        private int numOutputChannels = 0;
        private int[] inputChannels = null;
        int[] inputChUniq = null;                                           // unique id's of input channels
        private int[] outputChannels = null;
        private int deviceProtocol = 0;
        private int numberOfInputValues = 0;
        private double inputSampleRate = 0;                                 // input sample rate of source

        // active variables
        private SerialPort serialPort = null;
        private IntPtr handle = IntPtr.Zero;
        
        private bool readCom = false;
        private Object lockSerialPort = new Object();
        private bool reachEmptyCom = false;              // flag to hold whether an empty com was reached (this prevents any packet in the com buffer from being processed)

        private Stopwatch swNexusPacketTimeout = new Stopwatch();                           // stopwatch object to give an exact amount to time passed inbetween loops
        private bool nexusPacketTimedOut = false;

        // time-power domain transform variables
        private int modelOrder = 0;

        private double[][] inputOutput = null;

        private ARFilter[] arFilters = null;

        // the data structure for tranmission decoding
        private class NexusPacket {
            public byte readstate = PACKET_START;
            public uint extract_pos = 0;
            public byte packetcount = 0;
            public ushort[] buffer_ushort = new ushort[PACKET_BUFFER_SIZE];
            public short[] buffer_short = new short[PACKET_BUFFER_SIZE];
            public byte switches = 0;
            public byte aux = 0;
            public long arrival_time = 0;
            public long prev_arrival_time = 0;
            public bool check_crc = false;                                   // flag whether the protocol supports CRC and sent data has to be checked (false = no crc check, true = do crc check)
            public ushort crc = 0;
            public byte[] data_payload = new byte[2 * PACKET_BUFFER_SIZE];
            public uint payload_pos = 0;
        }
        private static NexusPacket packet = new NexusPacket();
        private int packet_HiLo_value = 0;


        public NexusSignal() {
            
            parameters.addParameter<int>(
                "ComPort",
                "Com-port to use for communication",
                "0", "15", "3", new string[] { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM11", "COM12", "COM13", "COM14", "COM15", "COM16" });

            parameters.addParameter<int>(
                "DeviceProtocol",
                "Communication protocol to use",
                "0", "5", "4", new string[] { "Nexus protocol 1 (legacy)", "Nexus protocol 2 (legacy)", "Nexus protocol 3 (legacy)", "Nexus protocol 4 (legacy)", "Nexus protocol 5 power", "Nexus protocol 6 time" });

            parameters.addParameter<int>(
                "modelOrder",
                "Order of prediction model used for time-frequency domain transform. Only relevant in case of input in time domain (device protocol 6).",
                "1", "", "5");

            parameters.addParameter<double[][]>(
               "InputOutput",
               "Frequency bins for which the power in the input signal will be determined. Each bin will be outputted as a seperate output channel.\nEach row describes one bin by specifiying the id of the corresponding output channel, the lower and upper frequncy limit of the bin, and the amount of evaluations performed in this bin.\nOnly relevant in case of input in time domain (device protocol 6).",
               "", "", "1;1;15;25;10", new string[] { "Input", "Output", "Lower limit", "Upper limit", "Evaluations"  });

            // message
            logger.Info("Source created (version " + CLASS_VERSION + ")");

            // start a new thread
            signalThread = new Thread(this.run);
            signalThread.Name = "NexusSignal Run Thread";
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

            // check if the source is not configured yet
            if (!configured) {

                // message
                logger.Error("Trying to retrieve the input samples per second before the source was configured, first configure the source, returning 0");

                // return 0
                return 0;

            }

            // return the samples per second
            return inputSampleRate;

        }

        /**
        
         **/
        public double getOutputSamplesPerSecond() {

            // check if the source is not configured yet
            if (!configured) {

                // message
                logger.Error("Trying to retrieve the output samples per second before the source was configured, first configure the source, returning 0");

                // return 0
                return 0;

            }

            // return the samples per second
            return OUTPUT_SAMPLE_RATE;

        }
		
		

        public bool configure(out PackageFormat output) {

            // reset numOutputChannels
            numOutputChannels = 0;

            // transfer inputOutput information
            inputOutput = parameters.getValue<double[][]>("InputOutput");

            // if at least one output is defined, retrieve needed information on input and output channels and get maximal value of defined output channels
            if (inputOutput[0].Length >= 1) {

                // init vars
                inputChannels = new int[inputOutput[0].Length];
                outputChannels = new int[inputOutput[0].Length];

                // cycle through rows and retrieve information
                for (int i = 0; i < inputOutput[0].Length; i++) {
                    inputChannels[i] = (int)inputOutput[0][i];
                    outputChannels[i] = (int)inputOutput[1][i];
                    numOutputChannels = Math.Max(numOutputChannels, (int)inputOutput[1][i]);  
                }
            } else {
                logger.Error("No output channels defined.");
                output = null;
                return false;
            }
			
            // create the sampleformat (the nexusfilter - regardless if set to power or time domain - will always give one sample per package, thus 1)
            // therefore, the given samplerate is actually the packagerate here
            output = new PackageFormat(numOutputChannels, 1, OUTPUT_SAMPLE_RATE);
            
            // retrieve and set the comport
            int comPortOption = parameters.getValue<int>("ComPort");
            comPort = "COM" + (comPortOption + 1);

            // check the device protocol setting
            deviceProtocol = parameters.getValue<int>("DeviceProtocol");
            deviceProtocol++;       // legacy code assumes protocol 1 to 6 (instead of 0 to 5)

            // message on the protocol
            if (deviceProtocol < 5) {
                logger.Warn("Device protocol is set to one of the Nexus legacy protocols");
            } else if (deviceProtocol == 5) {
                logger.Info("Using Nexus power-mode protocol");
            } else if (deviceProtocol == 6) {
                logger.Info("Using Nexus time-mode protocol");
            }

           
            // calculate and register the source input streams for power mode
            if (deviceProtocol == 5) {

                // set input sample rate
                inputSampleRate = NEXUS_POWERMODE_SIGNALFREQUENCY;

                numberOfInputValues = NEXUS_POWERMODE_CHANNELS_PER_PACKAGE * NEXUS_POWERMODE_SAMPLES_PER_PACKAGE;

                // create sampleFormat object. Since we are splitting the nexus channels up to seperate streams, the format defines 1 channel
                PackageFormat nexusPowerSampleFormat = new PackageFormat(1, NEXUS_POWERMODE_SAMPLES_PER_PACKAGE, inputSampleRate);

                // register the channels from the nexus (which in power mode have only 1 sample) as seperate source input streams)
                for (int channel = 0; channel < NEXUS_POWERMODE_CHANNELS_PER_PACKAGE; channel++) {
                    Data.registerSourceInputStream("Nexus_Input_Ch" + (channel + 1), nexusPowerSampleFormat);
                }
            }

            // transfer relevant parameters for time mode, perform sanity checks on parameters and create ARFilter, and register output streams
            if (deviceProtocol == 6) {

                // set input sample rate
                inputSampleRate = NEXUS_TIMEMODE_SIGNALFREQUENCY;

                numberOfInputValues = NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE * NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE;

                // transfer the model order
                modelOrder = parameters.getValue<int>("modelOrder");
                if (modelOrder < 1 || NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE < modelOrder) {
                    logger.Error("Model order must be at least 1 and at most equal to length of input, in this case " + NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE.ToString() + ".");
                    output = null;
                    return false;
                }

                // if at least one bin is fully defined
                if(inputOutput[0].Length >= 1 && inputOutput.Length >= 5) {

                    // get the amount of different input channels defined and sort ascending
                    inputChUniq = inputChannels.unique();
                    Array.Sort(inputChUniq);

                    // create array of ARFilters, equal to maximum id of input channels
                    arFilters = new ARFilter[inputChUniq[inputChUniq.Length - 1]];

                    // for each unique input channel, retrieve necessary information to create ARFilter
                    for (int ch = 0; ch < inputChUniq.Length; ch++) {

                        // find all indices in inputOutput matrix for this input channel
                        int[] indices = Extensions.findIndices(inputChannels, inputChUniq[ch]);

                        // init vars to hold relevant information
                        int[] evalPerbin = new int[indices.Length];
                        double[] lowerLimitBin = new double[indices.Length];
                        double[] upperLimitBin = new double[indices.Length];

                        // transfer relevant information for construction of ARFilter
                        for (int i = 0; i < indices.Length; i++) {
                            lowerLimitBin[i] = inputOutput[2][indices[i]];
                            upperLimitBin[i] = inputOutput[3][indices[i]];
                            evalPerbin[i] = (int)inputOutput[4][indices[i]];
                        }

                        // construct ARFilter and store in correct index (equal to input channel id) in array of ARFilters, minus one after lookup because unput channels are user-given and tehrefore 1-based
                        arFilters[inputChUniq[ch]-1] = new ARFilter(NEXUS_TIMEMODE_SIGNALFREQUENCY, modelOrder, evalPerbin, lowerLimitBin, upperLimitBin);
                    }
                }
                else {
                    logger.Error("At least one bin must be defined in Input Output matrix.");
                    return false;
                }

                // create sampleFormat object. Since we are splitting the nexus channels up to seperate streams, the format defines 1 channel
                PackageFormat nexusTimeSampleFormat = new PackageFormat(1, NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE, inputSampleRate);

                // TODO: change names of stream to bins they represent?
                // register the streams
                for (int channel = 0; channel < NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE; channel++)
                    Data.registerSourceInputStream("Nexus_Input_Ch" + (channel + 1), nexusTimeSampleFormat);
            }

            // lock for thread safety
            lock (lockSerialPort) {

                // if a serial port object is still there, close the port first
                if (serialPort != null) closeSerialPort();

                // try to open the port
                if (!openSerialPort(comPort, deviceProtocol)) {

                    // message is already given by openSerialPort

                    // return failure
                    return false;

                } else
                    closeSerialPort();

            }

            // if in debug mode, prepare test data
            #if (DEBUG_TESTDATA)

                // give feedback 
                logger.Error("DEBUG MODE ON. GENERATING TEST DATA.");


                // prepare test data
                testData = genDebugData();

                // convert testData to ushort so it can be injected in the packet buffer
                testDataShort = Array.ConvertAll(testData, x => (ushort)x);

                
            #endif


            // flag as configured
            configured = true;

            // return success
            return true;

        }

        public void initialize() {
            
            // lock for thread safety
            lock (lockSerialPort) {

                // reset the state of the nexus packet
                packet.readstate = PACKET_START;

                // if a serial port object is still there, close the port first
                if (serialPort != null) closeSerialPort();

                // set the connection lost flag to false
                Globals.setValue<bool>("ConnectionLost", "0");
                
                // open the serial port
                if (!openSerialPort(comPort, deviceProtocol)) {

                    // message is already given by openSerialPort

                    // return
                    return;

                }

                // set the initial packet time to zero
                // - this will give the first packet interval a huge value, causing the readSerial function not to accidentally 
                // sleep on it (as it would happen if by change the interval between the starting and the incoming packet is smaller than 150ms)
                packet.prev_arrival_time = 0;
                packet.arrival_time = 0;

                // flag to start reading from the comport
                readCom = true;

            }

            // flag the initialization as complete
            initialized = true;

        }

	    /**
	     * Start
	     */
        public void start() {

            // check if configured and the source was initialized
            if (!configured || !initialized) {
                return;
            }

            // lock for thread safety
            lock(lockStarted) {

                // set the connection lost flag to false
                Globals.setValue<bool>("ConnectionLost", "0");

                // check if the generator was not already started
                if (started)     return;

                // start generating
                started = true;

            }
		
        }

	    /**
	     * Stop
	     */
	    public void stop() {

            // if not initialized than nothing needs to be stopped
            if (!initialized)   return;

            // lock for thread safety
            lock (lockStarted) {

                // check if the source is generating signals
                if (started) {
                    
                    // stop generating
                    started = false;

                }

            }

	    }

	    /**
	     * Returns whether the signalgenerator is generating signal
	     * 
	     * @return Whether the signal generator is started
	     */
	    public bool isStarted() {

            // lock for thread safety
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

            // close the serial connection
            closeSerialPort();

            // flag the thread to stop running (when it reaches the end of the loop)
            running = false;

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
	     * Note, this is something different than actually generating
	     * 
	     * @return Whether the source thread is running
	     */
	    public bool isRunning() {
		    return running;
	    }
        
	    /**
	     * Source running thread
	     */
        private void  run() {

            // name this thread
            if (Thread.CurrentThread.Name == null) {
                Thread.CurrentThread.Name = "Source Thread";
            }

            // log message
            logger.Debug("Thread started");

		    // loop while running
		    while(running) {

                // lock for thread safety
                lock (lockSerialPort) {
                    
                    // lock for thread safety
                    lock(lockStarted) {

                        // check if we should start reading (after initialization)
                        if (readCom) {

                            // read the channels
                            readChannels(deviceProtocol);

                        }

                        // check if we are started
                        if (started) {  
                            // power mode
                            if (deviceProtocol == 5) {

                                // output array
                                double[] retSample = new double[numOutputChannels];
                        
                                // loop through the samples and channels
                                for (int sample = 0; sample < NEXUS_POWERMODE_SAMPLES_PER_PACKAGE; sample++) {              // should be one sample
                                    for (int channel = 0; channel < NEXUS_POWERMODE_CHANNELS_PER_PACKAGE; channel++) {

                                        // check if this input channel must be distributed and if so, to which output channels (+1 because in GUI channels are 1-based)
                                        int[] inputCh = Extensions.findIndices(inputChannels, (channel + 1));
                                        if (inputCh.Length != 0) {

                                            // transfer the values, minus one after lookup in output channels, because this is user-input, using 1-based channels
                                            for (int ch = 0; ch < inputCh.Length; ch++) retSample[outputChannels[inputCh[ch]] - 1] = packet.buffer_ushort[sample * NEXUS_POWERMODE_CHANNELS_PER_PACKAGE + channel];
                                        }
                                    }
                                }

                                // pass the sample
                                MainThread.eventNewSample(retSample);
                            }

                            // time mode
                            if (deviceProtocol == 6) {

                                // reset output array and cast buffer to doubles to allow ARFilter to work 
                                double[] retSample = new double[numOutputChannels];
                                double[] nexusBuffer = Array.ConvertAll(packet.buffer_short, x => (double)x);

                                // create buffers for different input channels         
                                int buffLength = NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE;
                                double[][] inputChBuffers = new double [NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE][];
                                for(int ch = 0; ch < NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE; ch++)      inputChBuffers[ch] = new double[buffLength];

                                // loop through data coming from Nexus
                                for (int sample = 0; sample < NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE; sample++) {
                                    for (int channel = 0; channel < NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE; channel++) {

                                        // check if this input channel must be distributed (+1 because in GUI channels are 1-based)
                                        if (Array.IndexOf(inputChUniq, channel + 1) != -1) {
                                            
                                            // store in input channel buffer
                                            inputChBuffers[channel][sample] = nexusBuffer[sample * NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE + channel];
                                            
                                        }
                                    }
                                }

                                // process the seperate input channel buffers
                                for (int channel = 0; channel < NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE; channel++) {

                                    // check if this buffer contains data (+1 because in GUI channels are 1-based)
                                    if (Array.IndexOf(inputChUniq, channel + 1) != -1) {

                                        // init var
                                        double[] powerSpec = null;

                                        if (arFilters[channel] != null) {

                                            // fill buffer of corresponding ARFilter if ARFilter is allowed to run 
                                            if (arFilters[channel].AllowRun)    arFilters[channel].Data = inputChBuffers[channel];

                                            // determine linearModel on data if ARFilter is allowed to run
                                            if (arFilters[channel].AllowRun)    arFilters[channel].createLinPredModel();

                                            // determine power spectrum if ARFilter is allowed to run
                                            if (arFilters[channel].AllowRun)    powerSpec = arFilters[channel].estimatePowerSpectrum();

                                            // find output channels corresponding to bins in powerSpec
                                            int[] indices = Extensions.findIndices(inputChannels, (channel + 1));

                                            // transfer values from powerSpec to correct indices in retSample (minus 1 because values in outputChannels are 1-based because user-input)
                                            for (int i = 0; i < indices.Length; i++) {
                                                retSample[outputChannels[indices[i]] - 1] = powerSpec[i];
                                            }

                                        } else {

                                            // message
                                            logger.Error("ARFilter for input channel " + channel + " is not initialized. Check code.");

                                        }

                                    }
                                    
                                }

                                //debug
                                //for (int i = 0; i < retSample.Length; i++) logger.Info("powerspec: " + retSample[i]);

                                // pass the sample
                                MainThread.eventNewSample(retSample);
                            }

                        } else {
                            // not started

                            // let the thread sleep, allowing for other processes
                            Thread.Sleep(10);

                        }

                    }   // end lock(lockStarted)

                }

		    }   // end while(running)

            // log message
            logger.Debug("Thread stopped");

        }


        private bool openSerialPort(string portName, int protocol) {

            int baudRate = DEFAULT_BAUD;
	        int dataBits = 8;
            Parity parity = Parity.None;
            StopBits stopBits = StopBits.One;
            
            // the nexus device uses a baudrate of 38.4Kbs
            if (protocol == 5 || protocol == 6)   baudRate = SerialPortNet.CBR_38400;
            
            // Create a new SerialPort object
            serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
            serialPort.ReadBufferSize = 40960;	// Set read buffer == 10K
            serialPort.WriteBufferSize = 4096;	// Set write buffer == 4K
            serialPort.DtrEnable = false;   // no hardware handshake (DSR/DTR handshake)
            serialPort.RtsEnable = false;   // no hardware handshake (RTS/CTS handshake)

            try {

                serialPort.Open();

            } catch (Exception) {

                // message
                logger.Error("Could not open COM-port (" + portName + ")");

                // return failure
                return false;

            }


            //try {

                // get the handle of the serialport object
                handle = ((SafeFileHandle)serialPort.BaseStream.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(serialPort.BaseStream)).DangerousGetHandle();
                
                if (!SerialPortNet.SetCommMask(handle, SerialPortNet.EV_RXCHAR)) {
                    return false;
                }

                // 
                SerialPortNet.DCB dcb = new SerialPortNet.DCB();
                int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(SerialPortNet.DCB));
                dcb.DCBLength = (uint)size;
                SerialPortNet.GetCommState(handle, ref dcb);

                // 
                if (protocol == 5 || protocol == 6)
                    dcb.BaudRate = SerialPortNet.CBR_38400;
                else
                    dcb.BaudRate = DEFAULT_BAUD;
                dcb.ByteSize = 8;
                dcb.Parity = Parity.None;
                dcb.StopBits = StopBits.One;
                dcb.OutxCtsFlow = false;
                dcb.OutxDsrFlow = false;
                dcb.InX = false;
                dcb.OutX = false;
                dcb.DtrControl = SerialPortNet.DtrControl.Disable;
                dcb.RtsControl = SerialPortNet.RtsControl.Disable;
                dcb.Binary = true;
                dcb.Parity = Parity.None;
                if (!SerialPortNet.SetCommState(handle, ref dcb)) {
                    return false;
                }
            
            // check if using Nexus protocol 5 (power) or protocol 6 (time)
            if ((protocol== 5) || (protocol== 6)) {

                // transfer the baudrate to configure the RS232 IR adapter to. (should be 38.4Kbs)
                int m_dwBaudRate = (int)dcb.BaudRate;

                ////
                // routine to configure the RS232 dongle baudrate
                // set dongle baud (Actisys 220L+)
                // 
                // this part below to configre the dongle baudrate comes straight
                // from the Medtronic Model 09084 NEXUS-1 Streaming Telemetry System (STS) programming manual
                ////
                
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRDTR);
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRRTS);
                Thread.Sleep(1);
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETDTR);
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETRTS);
                Thread.Sleep(10);   // 60?
                // default to 9600
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRDTR);
                Thread.Sleep(1);    // 10?
                SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETDTR);
                Thread.Sleep(1);

                switch (m_dwBaudRate) {

                    case SerialPortNet.CBR_38400:
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRRTS);
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETRTS);
                        Thread.Sleep(1);
                        goto case SerialPortNet.CBR_115200; // fall thru

                    case SerialPortNet.CBR_115200:
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRRTS);
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETRTS);
                        Thread.Sleep(1);
                        goto case SerialPortNet.CBR_57600;  // fall thru

                    case SerialPortNet.CBR_57600:
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRRTS);
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETRTS);
                        Thread.Sleep(1);
                        goto case SerialPortNet.CBR_19200;  // fall thru

                    case SerialPortNet.CBR_19200:
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.CLRRTS);
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, (uint)SerialPortNet.ExtendedFunctions.SETRTS);
                        goto case SerialPortNet.CBR_9600;   // fall thru

                    case SerialPortNet.CBR_9600:
                        // fall thru
                    default:
                        // All done
                        //logger.Debug("alldone");
                        break;
                }
                // end dongle control

            }

            // return succes on opening the serial port
            return true;

        }



        private void closeSerialPort() {
            
            handle = IntPtr.Zero;

            if (serialPort != null)
                serialPort.Close();
            
        }
        

        //  reads one packet (all 6 channels) from serial port
        //  this function is called n-times by the process() member-function
        //  to get a sample block of size n.
        private void readChannels(int protocol) {

            // temp read variable
            byte[] buf = new byte[1];

            // zero the packet buffer
            for (int i = 0; i < PACKET_BUFFER_SIZE; i++) {
                packet.buffer_ushort[i] = 0;
                packet.buffer_short[i] = 0;
            }
            
            // determine if the protocol allows for crc check
            packet.check_crc = (protocol == 5 || protocol == 6);

            // set the start for the packet timeout stopwatch
            nexusPacketTimedOut = false;
            swNexusPacketTimeout.Start();

            // loop read from the comport
            do {

                // try to read one byte from the serial port
                if (readSerial(ref buf, 1) == 1) {

                    // byte available: parse the selected protocol
                    switch (protocol) {
                        case 1:
                            parse_byte_P1(buf[0]);
                            break;

                        case 2:
                            parse_byte_P2(buf[0]);
                            break;

                        case 3:
                            parse_byte_P3(buf[0]);
                            break;

                        case 4:
                            parse_byte_P4(buf[0]);
                            break;

                        case 5:
                            parse_byte_P5(buf[0]);
                            break;

                        case 6:
                            parse_byte_P6(buf[0]);
                            break;

                        default:
                            logger.Error("Unknown protocol");
                            break;

                    }

                    // restart the packet timeout timer  (stops the timer, sets elapsed time to zero and starts the timer again)
                    swNexusPacketTimeout.Restart();

                } else {
                    // no byte available

                    // check if an empty packet was ever reached
                    if (!reachEmptyCom) {

                        // flag as empty packet reached
                        reachEmptyCom = true;

                    }

                    // sleep to allow for other processes
                    Thread.Sleep(1);

                }

                // determine whether the timeout was reached
                nexusPacketTimedOut = swNexusPacketTimeout.ElapsedMilliseconds > COM_TIMEOUT;

                // check if a packet has come in before an empty comport was every reached
                if (packet.readstate == PACKET_FINISHED && !reachEmptyCom) {
                    // probably a cached package, discard

                    // message
                    logger.Error("Cached packet found, discarded");

                    // start again
                    packet.readstate = PACKET_START;

                }

            } while ((packet.readstate != PACKET_FINISHED) && !nexusPacketTimedOut);        // read until the packet is finished or a timeout has occured


            // if debug mode on, reset time out flag
            #if (DEBUG_TESTDATA)
                nexusPacketTimedOut = false;    
            #endif

            // check if the packet timed out or the packet is just finished
            if (nexusPacketTimedOut) {
                // timed out
            
                    // message
                    logger.Warn("Source packet timed out");

                    // log timeout event
                    Data.logEvent(1, "SourcePacketTimeout", "");

                    // set the connection lost flag to true
                    Globals.setValue<bool>("ConnectionLost", "1");

                    // reset the timeout timer (stops the timer and sets elapsed time to zero)
                    swNexusPacketTimeout.Reset();

            } else {
                // packet finished

                // if connection was previously lost, log that connection is restored
                if(Globals.getValue<bool>("ConnectionLost")) Data.logEvent(1, "SourcePacketTimeoutRestored", "");

                // set the connection lost flag to false
                Globals.setValue<bool>("ConnectionLost", "0");

                // flag to hold whether the packet is corrupt
                bool packetIsCorrupt = false;

                // check if the protocol packet allows for a CRC check
                if (packet.check_crc) {

                    // calculate the CRC
                    ushort crc_calculated = nexusCalcCRC();

                    // check if the CRCs match
                    if (crc_calculated != packet.crc) {

                        // message
                        logger.Warn("Corrupt data-packet received (CRC wrong: " + packet.crc + " != " + crc_calculated + ")");

                        // log event
                        Data.logEvent(1, "SourcePacketBadCRC", "");

                        // set buffer values to zero
                        for (int i = 0; i < PACKET_BUFFER_SIZE; i++) {
                            packet.buffer_ushort[i] = 0;
                            packet.buffer_short[i] = 0;
                        }

                        // flag as corrupt
                        packetIsCorrupt = true;

                    }

                }

                // check if the packet was not corrupt
                if (!packetIsCorrupt) {
                    
                    // check if we are started
                    if (started) {
                            
                        // check if there are input values to log (depends mainly on the device protocol)
                        if (numberOfInputValues > 0) {

                            // if in debug mode, inject test data in buffer. If test data is set to random, generate new data for packet. If set to sinus, keep using same packet because the generated signals are repetitive in nature
                            #if (DEBUG_TESTDATA)

                                if (debSig == DebugSignalType.Rand) {
                                    testData = genDebugData();
                                    testDataShort = Array.ConvertAll(testData, x => (ushort)x);
                                }
                                Array.Copy(testDataShort, 0, packet.buffer, 0, numberOfInputValues);

                            #endif

                            // log the values as source input before timing correction
                            if (protocol == 6) {
                                
                                // pick the values from the buffer
                                short[] values = new short[numberOfInputValues];
                                Array.Copy(packet.buffer_short, 0, values, 0, numberOfInputValues);

                                Data.logSourceInputValues(values);

                            } else {

                                // pick the values from the buffer
                                ushort[] values = new ushort[numberOfInputValues];
                                Array.Copy(packet.buffer_ushort, 0, values, 0, numberOfInputValues);

                                Data.logSourceInputValues(values);
                            }
                                

                        }
                    }

                    // timing correction
                    //
                    // Although the nexus measures at a frequency of 5hz (power mode) or 200hz (time mode), it only sends data every 400ms.
                    // In power mode the nexus sends 2 packets (where each packet holds 1 sample) close after each other at an interval of 400ms.
                    // In time mode the nexus sends 2 packets (where each packet holds 40 samples) close after each other at an interval of 400ms.
                    // 
                    // Returning from this function will result in a source output sample. The frequency with which we end up here (at the end 
                    // of receiving a packet and about to return from this function) is not the same as we might want the output to come.
                    // 
                    //
                    // Basically (in a timeline where P is a package and a . is idle time) what we do here is prevent this:
                    // 
                    // in:  P.P............P.P...............P.P...............
                    // out: P.P............P.P...............P.P...............
                    // 
                    // But instead make this happen:
                    // 
                    // in:  P.P............P.P...............P.P...............
                    // out: P......P.......P........P........P.........P.......
                    // 
                    // 
                    // For power mode, we ideally want the source to output the samples at (about) the same rate as they are measured (5Hz), as this is intuitive.
                    // For time mode, we could do this as well, but because every packet holds 40 samples instead of 1 sample (like in power mode) it is computationally
                    //                more interesting to process all these 40 samples from one packet at once. So here we divide the number of packages over
                    //                the 400ms interval resulting in a 5Hz sample output interval (400ms / 2 packages = one package every 200ms; every 200ms = 5Hz)
                    // 
                    // To achieve this, we delay the second packet
                    
                    
                    // we have a valid packet, now check if it is the 2nd packet sent every 400 ms (and data comes from NEXUS-1 STS)
                    //double time_diff = (double)(packet.arrival_time - packet.prev_arrival_time) / (double)Stopwatch.Frequency * 1000;
                    // check the difference between this packet and the last
                    double time_diff = (double)(packet.arrival_time - packet.prev_arrival_time) / (double)Stopwatch.Frequency * 1000;

                    // check by the time difference if this is the second package
                    if ((time_diff > 0) && (time_diff < 150) && (protocol >= 5)) {
                        // second package

                        // wait some time to have the packets equally distributed in time
                        Thread.Sleep(200 - (int)time_diff);

                    }

                }

            }

            // packet finished, set to start-state
            packet.readstate = PACKET_START;
            
        }

        /* reads max. <size> bytes from the serial port,
            returns number of bytes read, or -1 if error */
        //private int readSerial(char *buf, int size) {
        private int readSerial(ref byte[] buf, int size) {

            SerialPortNet.COMSTAT comStat = new SerialPortNet.COMSTAT();
            uint errorFlags = 0;
            uint len;

            SerialPortNet.ClearCommError(handle, out errorFlags, out comStat);
            len = comStat.cbInQue;

	        //logger.Debug("Queue length " + len + ", ");
	        if (len > size)     len = (uint)size;
	        if (len > 0) {

                try {
                    int rv = serialPort.Read(buf, 0, size);         // returns The number of bytes read.

                } catch (Exception e) {
                    logger.Error("exception");
                    logger.Error(e.Message);

				    //fprintf(hNotes,"zero bytes read: Buffer %d, ",(unsigned char)buf[0]);
			        //buf[0]=1;

			        //fprintf(hNotes,"Error reading serial port %d, ",rv);
			        len = 0;

                    SerialPortNet.ClearCommError(handle, out errorFlags, out comStat);

                    return -1;
		        }


		        if (errorFlags > 0) {
                    logger.Debug("Error flags serial port");
                    SerialPortNet.ClearCommError(handle, out errorFlags, out comStat);
			        return -1;
		        }
	        }
	        return (int)len;
       
            
        }



        // parse a packet in P1 format
        private void parse_byte_P1(byte actbyte) {
            //	char s[33];

            switch (packet.readstate) {

                case 0:
 
                    if (actbyte == 192) {

                        packet.readstate++;  // one sync byte
                        packet.packetcount = 5;
                        packet.extract_pos = 0;

                    } else {

                        packet.readstate = PACKET_START;

                    }

                    break;

                case 1:
                    
                    if (packet.extract_pos < 4) {

                        if ((packet.extract_pos & 1) == 0)
                            packet.buffer_ushort[packet.extract_pos >> 1] = (ushort)(actbyte * 256);
                        else 
                            packet.buffer_ushort[packet.extract_pos >> 1] += actbyte;

                        packet.extract_pos++;

                    }

                    if (packet.extract_pos == 4) {

                        packet.switches = 0;
                        packet.readstate = PACKET_FINISHED;
                        //  *** PACKET ARRIVED ***

                    }

                    break;
                    
                case PACKET_FINISHED: 
                    break;

                default: 
                    packet.readstate = PACKET_START;
                    break;

		    }   // end of switch

        }   // parse_byte_P1()


        private void parse_byte_P2(byte actbyte) {         // parse a packet in P2 format

            switch (packet.readstate) {

                case 0:
                      
                    if (actbyte==192)
                        packet.readstate++;   // first sync byte

                    break;

                case 1:
                      
                    if (actbyte==0xC0)
                        packet.readstate++;   // second sync byte
                    else
                        packet.readstate=0;

                    break;

                case 2: packet.readstate++;    // Version Number  
                    break;

                case 3: 

                    packet.packetcount = actbyte;
                    packet.extract_pos = 0;
                    packet.readstate++;

                    break;

                case 4:

                    if (packet.extract_pos < 12) {

                        if ((packet.extract_pos & 1) == 0)
                            packet.buffer_ushort[packet.extract_pos >> 1] = (ushort)(actbyte*256);
                        else
                            packet.buffer_ushort[packet.extract_pos >> 1] += actbyte;

                        packet.extract_pos++;

                    } else {
                        
                        packet.switches = actbyte;
                        packet.readstate = PACKET_FINISHED;
                        //  *** PACKET ARRIVED ***

                    }

                break;

                case PACKET_FINISHED: 
                    break;
                
                default: 
                    packet.readstate = 0;
                    break;

            }

        }   // parse_byte_P2()


        private void parse_byte_P3(byte actbyte) {   // parse a packet in P3 format

            //    char s[33];
            //#ifdef DEBUG
            //logger.Debug("readstate: " + packet.readstate);
            //#endif

            switch (packet.readstate) {
                case 0:

                    if (actbyte==192) {
                        packet.readstate++;  // first sync byte
                        
                        //#ifdef DEBUG
                        //logger.Debug("readstate=" + packet.readstate);
                        //#endif

                        packet.packetcount = 5;
                        packet.extract_pos = 0;

                        //#ifdef DEBUG
                        //if (hNotes != NULL) fprintf(hNotes,"%d sync, ",actbyte);
                        //#endif
                    } else {
                        
                        packet.readstate=0;
            
                        //#ifdef DEBUG
                        //logger.Debug("readstate=0 (3)\n");
                        //if (actbyte==0)
                        //  logger.Debug(actbyte + " zerosync");
                        //else {
                        //  logger.Debug(actbyte + " nosync");
                        //}
                        //#endif
                    }

                    break;

            case 1:

                //#ifdef DEBUG
                //logger.Debug(packet.extract_pos + " "  + actbyte);
                //#endif

                if (packet.extract_pos < 4) {

                    if ((packet.extract_pos & 1) == 0)
                        packet.buffer_ushort[packet.extract_pos >> 1] = (ushort)(actbyte*256);
                    else 
                        packet.buffer_ushort[packet.extract_pos >> 1] += actbyte;

                    packet.extract_pos++;

                }

                if (packet.extract_pos == 4) {
                    
                    packet.switches = 0;
                    packet.readstate = PACKET_FINISHED;
                
                    //#ifdef DEBUG
                    //logger.Debug("readstate=" + PACKET_FINISHED + " (4)");
                    //#endif

                    //  *** PACKET ARRIVED ***
                }

                break;

            case PACKET_FINISHED:
                
                //#ifdef DEBUG
                //logger.Debug("packet finished");
                //#endif

                break;

            default:
                
                packet.readstate=0;
                
                //#ifdef DEBUG
                //logger.Debug("readstate=0 (5)");
                //#endif

                break;

            }

        }   // parse_byte_P3()


        // parse packet in P4 format
        private void parse_byte_P4(byte actbyte) {
            //char temp[200];
            //sprintf(temp, "P4: byte=%02x  state=%02d  pos=%02d", actbyte, packet.readstate, packet.extract_pos);
            //bciout << temp << endl;

            switch (packet.readstate) {
                case 0:

                    if (actbyte == 192) {  // == 0xC0

                        packet.readstate++;  // one sync byte
                        packet.packetcount = 9;
                        packet.extract_pos = 0;

                    }

                    break;

                case 1:
                    
                    if (packet.extract_pos < 8) {

                        // Power channels 1 to 4
                        if ((packet.extract_pos & 1) == 0)
                            packet.buffer_ushort[packet.extract_pos >> 1] = (ushort)(actbyte * 256);
                        else
                            packet.buffer_ushort[packet.extract_pos >> 1] += actbyte;

                    }

                    if (packet.extract_pos == 8) {

                        // Detection Status
                        packet.buffer_ushort[4] = actbyte;

                        //  *** PACKET ARRIVED ***
                        packet.switches = 0;
                        packet.readstate = PACKET_FINISHED;

                    }

                    packet.extract_pos++;

                    break;

                case PACKET_FINISHED:
                    break;

                default:
                    
                    packet.readstate = 0;

                    break;

            }

        }  // parse_byte_P4()
        
        // parse packet in P5 format (i.e. NEXUS-1 STS Power-Domain)
        private void parse_byte_P5(byte actbyte) {

            switch (packet.readstate) {
            
                case 0:
                    
                    if ((actbyte >= 192) && (actbyte <= 196)) {  // == 0xC0,0xC1,0xC2,0xC3,0xC4

                        // (stim-prog information only available in NEXUS-1 STS)
				        packet.readstate++;  // one sync byte
				        packet.packetcount = 9;
				        packet.extract_pos = 0;

                        packet.prev_arrival_time = packet.arrival_time;
                        packet.arrival_time = Stopwatch.GetTimestamp();

				        packet.data_payload[0] = actbyte;
				        packet.payload_pos = 1;
			        }
			        
                    break;

		        case 1:
			        
                    if (packet.extract_pos < 8) {

				        // Power channels 1 to 4
				        if ((packet.extract_pos & 1) == 0)
					        packet.buffer_ushort[packet.extract_pos >> 1] = (ushort)(actbyte * 256);
				        else
					        packet.buffer_ushort[packet.extract_pos >> 1] += actbyte;

				        // store data for CRC
				        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;

			        }

			        if (packet.extract_pos == 8) {

				        // Detection Status
				        packet.buffer_ushort[4] = actbyte;

				        // store data for CRC
				        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;

			        }

			        if (packet.extract_pos == 9) {
				        packet.crc = (ushort)(actbyte * 256);
			        }

			        if (packet.extract_pos == 10) {

				        packet.crc += actbyte;
				        
                        //  *** PACKET ARRIVED ***
				        packet.switches = 0;
				        packet.readstate = PACKET_FINISHED;

			        }
			        packet.extract_pos++;

			        break;
		        
                case PACKET_FINISHED:
			        break;

		        default:
			        packet.readstate = 0;
			        break;

	        }

        }  // parse_byte_P5()

        // parse packet in P6 format (i.e. NEXUS-1 STS Time-Domain)
        private void parse_byte_P6(byte actbyte) {

            switch (packet.readstate) {

                case 0:

			        if ((actbyte >= 192) && (actbyte <= 196)) {  // == 0xC0,0xC1,0xC2,0xC3,0xC4
			        
				        packet.readstate++;  // one sync byte
				        packet.packetcount = 163;  // (2ch * 40 samples * 2 bytes) + detect + 2 bytes-CRC
				        packet.extract_pos = 0;

                        packet.prev_arrival_time = packet.arrival_time;
                        packet.arrival_time = Stopwatch.GetTimestamp();

				        packet.data_payload[0] = actbyte;
				        packet.payload_pos = 1;

                        packet_HiLo_value = 0;


                    }

			        break;

		        case 1:

			        if (packet.extract_pos < (uint)(packet.packetcount - 3)) {

                        int sample_offset = (int)packet.extract_pos / 16;  // blocks of 4 samples
				        int curr_ch = (int)(packet.extract_pos / 8) % 2;  // 0 or 1
                        int curr_sample = (int)((packet.extract_pos % 16) - (8 * curr_ch)) / 2; // 0, 1, 2, 3
                        bool low_byte = packet.extract_pos % 2 == 1;

				        // save data in sequences of ch1ch2ch3ch1ch2ch3... as process() expects it in this order
				        int buf_pos = (sample_offset * 12) + (curr_sample * 3) + curr_ch;

                        // each sample (per channel) starts with a high-byte followed by a low-byte, creating a 16-bit number
                        // the 1th bit (and also the next 5 bits) of the hi-byte indicate whether it is a positve or negative number (binary signing)
                        // the 7th and the 8th bit of the hi-byte and the low-byte then form a 10-bit number to represent the value
                        // Because 1 bit is used for signing (positive or negative) and 10 bits for the value, so the value can be between -1023 and 1023
                        
                        // check if the low or high byte is coming in
                        if (!low_byte) {
                            
                            // shift the high bytes 8 bits and store in integer
                            packet_HiLo_value = actbyte << 8;

                        } else {
                            
                            // low-byte arrived, combine with high-byte
                            packet_HiLo_value += actbyte;

                            // old method, first 512 is added to shift all values up and in order to store as a ushort.
                            // Not necessary, direct conversion to short is applied to the whole number
                            //packet_HiLo_value += 512;
                            //Console.WriteLine("- " + Convert.ToString(packet_HiLo_value, 2) + "  " + (short)packet_HiLo_value);
                            
                            // direct conversion to short can applied to the whole number
                            packet.buffer_short[buf_pos] = (short)packet_HiLo_value;

                            //Console.WriteLine("- " + Convert.ToString(packet_HiLo_value, 2) + "  " + packet.buffer_short[buf_pos]);

                        }

                        // store data for CRC
                        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;
			        }

			        if (packet.extract_pos == (packet.packetcount - 3)) {
				        
                        // Detection Status
				        for (int i = 0; i < 40; i++)
					        packet.buffer_short[i*3+2] = actbyte;

				        // store data for CRC
				        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;

			        }

			        if (packet.extract_pos == (packet.packetcount-2)) {
				        packet.crc = (ushort)(actbyte * 256);
			        }

			        if (packet.extract_pos == (packet.packetcount-1)) {
				        packet.crc += actbyte;

				        //  *** PACKET ARRIVED ***
				        packet.switches = 0;
				        packet.readstate = PACKET_FINISHED;
			        }
			        packet.extract_pos++;

			        break;

		        case PACKET_FINISHED:
			        break;

		        default:
                    packet.readstate = PACKET_START;
			        break;
	        }
        }  // parse_byte_P6()




        private ushort nexusCalcCRC() {
            uint x;
            uint i;
            byte crc_data;
            ushort ee_crc_value;

            ee_crc_value = 0xffff;

            for (x = 0; x < packet.payload_pos; x++) {

                crc_data = packet.data_payload[x];
                
                // perform CRC calculation one bit at a time
                for (i = 0; i < 8; i++) {

                    if (((ee_crc_value & 0x0001) ^ (crc_data & 0x01)) == 1)
                        ee_crc_value = (ushort)((ee_crc_value >> 1) ^ 0x8408);
                    else
                        ee_crc_value = (ushort)(ee_crc_value >> 1);
                 
                    crc_data >>= 1;

                }

            }
            
            ee_crc_value = (ushort)(ee_crc_value ^ 0xFFFF);

            return ee_crc_value;

        }   // calc_crc


        #if (DEBUG_TESTDATA)
        private double[] genDebugData() {

            // init vars
            int channels = 0;
            int samples = 0;

            // transfer amount of samples and channels
            if (deviceProtocol == 5) {
                channels = NEXUS_POWERMODE_CHANNELS_PER_PACKAGE;
                samples = NEXUS_POWERMODE_SAMPLES_PER_PACKAGE;
            } else if (deviceProtocol == 6) {
                channels = NEXUS_TIMEMODE_CHANNELS_PER_PACKAGE;
                samples = NEXUS_TIMEMODE_SAMPLES_PER_PACKAGE;
            } else {
                return new double[0];
            }

            // create output array for test data
            double[] tData = new double[channels * samples];
            int counter = 0;

            // for eac channel, create test data
            for (int ch = 0; ch < channels; ch++) {

                double[] tempData = DebugHelper.generateTestData(amps[counter], frequencies[counter], samplingFrequency, samples, debSig);
                counter++;

                // if there are not enough amplitudes and frequencies defined, recycle by resetting counter
                if (counter >= amps.Length || counter >= frequencies.Length) { counter = 0; }

                // interleave samples over test data array
                for (int sample = 0; sample < samples; sample++) { tData[(sample * channels) + ch] = tempData[sample]; }
            }

            // convert testData to ushort
            return tData;
        }
        #endif
    }
}
