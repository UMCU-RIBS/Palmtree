using Microsoft.Win32.SafeHandles;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UNP.Core;
using UNP.Core.Helpers;
using UNP.Core.Params;

namespace UNP.Sources {

    public class SerialPortSignal : ISource {

        [DllImport("kernel32.dll")]
        static extern bool SetCommMask(IntPtr hFile, uint dwEvtMask);

        [DllImport("kernel32.dll")]
        static extern bool GetCommState(IntPtr hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll")]
        static extern bool SetCommState(IntPtr hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll")]
        static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);


        private static Logger logger = LogManager.GetLogger("GenerateSignal");
        private static Parameters parameters = ParameterManager.GetParameters("GenerateSignal", Parameters.ParamSetTypes.Source);



        public const uint EV_RXCHAR = 0x0001;

        public const int CBR_300 = 300;
        public const int CBR_600 = 600;
        public const int CBR_1200 = 1200;
        public const int CBR_2400 = 2400;
        public const int CBR_4800 = 4800;
        public const int CBR_9600 = 9600;
        public const int CBR_14400 = 14400;
        public const int CBR_19200 = 19200;
        public const int CBR_38400 = 38400;
        public const int CBR_57600 = 57600;
        public const int CBR_115200 = 115200;
        public const int CBR_128000 = 128000;
        public const int CBR_256000 = 256000;
        public const int DEFAULT_BAUD = CBR_115200;         // default Baud-Rate is 112k


        [StructLayout(LayoutKind.Sequential)]
        internal struct DCB {
            internal uint DCBLength;
            internal uint BaudRate;
            private BitVector32 Flags;

            private ushort wReserved;        // not currently used 
            internal ushort XonLim;           // transmit XON threshold 
            internal ushort XoffLim;          // transmit XOFF threshold             

            internal byte ByteSize;
            internal Parity Parity;
            internal StopBits StopBits;

            internal sbyte XonChar;          // Tx and Rx XON character 
            internal sbyte XoffChar;         // Tx and Rx XOFF character 
            internal sbyte ErrorChar;        // error replacement character 
            internal sbyte EofChar;          // end of input character 
            internal sbyte EvtChar;          // received event character 
            private ushort wReserved1;       // reserved; do not use     

            private static readonly int fBinary;
            private static readonly int fParity;
            private static readonly int fOutxCtsFlow;
            private static readonly int fOutxDsrFlow;
            private static readonly BitVector32.Section fDtrControl;
            private static readonly int fDsrSensitivity;
            private static readonly int fTXContinueOnXoff;
            private static readonly int fOutX;
            private static readonly int fInX;
            private static readonly int fErrorChar;
            private static readonly int fNull;
            private static readonly BitVector32.Section fRtsControl;
            private static readonly int fAbortOnError;

            static DCB() {
                // Create Boolean Mask
                int previousMask;
                fBinary = BitVector32.CreateMask();
                fParity = BitVector32.CreateMask(fBinary);
                fOutxCtsFlow = BitVector32.CreateMask(fParity);
                fOutxDsrFlow = BitVector32.CreateMask(fOutxCtsFlow);
                previousMask = BitVector32.CreateMask(fOutxDsrFlow);
                previousMask = BitVector32.CreateMask(previousMask);
                fDsrSensitivity = BitVector32.CreateMask(previousMask);
                fTXContinueOnXoff = BitVector32.CreateMask(fDsrSensitivity);
                fOutX = BitVector32.CreateMask(fTXContinueOnXoff);
                fInX = BitVector32.CreateMask(fOutX);
                fErrorChar = BitVector32.CreateMask(fInX);
                fNull = BitVector32.CreateMask(fErrorChar);
                previousMask = BitVector32.CreateMask(fNull);
                previousMask = BitVector32.CreateMask(previousMask);
                fAbortOnError = BitVector32.CreateMask(previousMask);

                // Create section Mask
                BitVector32.Section previousSection;
                previousSection = BitVector32.CreateSection(1);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                fDtrControl = BitVector32.CreateSection(2, previousSection);
                previousSection = BitVector32.CreateSection(1, fDtrControl);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                previousSection = BitVector32.CreateSection(1, previousSection);
                fRtsControl = BitVector32.CreateSection(3, previousSection);
                previousSection = BitVector32.CreateSection(1, fRtsControl);
            }

            public bool Binary {
                get { return Flags[fBinary]; }
                set { Flags[fBinary] = value; }
            }

            public bool CheckParity {
                get { return Flags[fParity]; }
                set { Flags[fParity] = value; }
            }

            public bool OutxCtsFlow {
                get { return Flags[fOutxCtsFlow]; }
                set { Flags[fOutxCtsFlow] = value; }
            }

            public bool OutxDsrFlow {
                get { return Flags[fOutxDsrFlow]; }
                set { Flags[fOutxDsrFlow] = value; }
            }

            public DtrControl DtrControl {
                get { return (DtrControl)Flags[fDtrControl]; }
                set { Flags[fDtrControl] = (int)value; }
            }

            public bool DsrSensitivity {
                get { return Flags[fDsrSensitivity]; }
                set { Flags[fDsrSensitivity] = value; }
            }

            public bool TxContinueOnXoff {
                get { return Flags[fTXContinueOnXoff]; }
                set { Flags[fTXContinueOnXoff] = value; }
            }

            public bool OutX {
                get { return Flags[fOutX]; }
                set { Flags[fOutX] = value; }
            }

            public bool InX {
                get { return Flags[fInX]; }
                set { Flags[fInX] = value; }
            }

            public bool ReplaceErrorChar {
                get { return Flags[fErrorChar]; }
                set { Flags[fErrorChar] = value; }
            }

            public bool Null {
                get { return Flags[fNull]; }
                set { Flags[fNull] = value; }
            }

            public RtsControl RtsControl {
                get { return (RtsControl)Flags[fRtsControl]; }
                set { Flags[fRtsControl] = (int)value; }
            }

            public bool AbortOnError {
                get { return Flags[fAbortOnError]; }
                set { Flags[fAbortOnError] = value; }
            }
        }

        public enum DtrControl : int {
            /// <summary>
            /// Disables the DTR line when the device is opened and leaves it disabled.
            /// </summary>
            Disable = 0,

            /// <summary>
            /// Enables the DTR line when the device is opened and leaves it on.
            /// </summary>
            Enable = 1,

            /// <summary>
            /// Enables DTR handshaking. If handshaking is enabled, it is an error for the application to adjust the line by 
            /// using the EscapeCommFunction function.
            /// </summary>
            Handshake = 2
        }

        public enum RtsControl : int {
            /// <summary>
            /// Disables the RTS line when the device is opened and leaves it disabled.
            /// </summary>
            Disable = 0,

            /// <summary>
            /// Enables the RTS line when the device is opened and leaves it on.
            /// </summary>
            Enable = 1,

            /// <summary>
            /// Enables RTS handshaking. The driver raises the RTS line when the "type-ahead" (input) buffer 
            /// is less than one-half full and lowers the RTS line when the buffer is more than 
            /// three-quarters full. If handshaking is enabled, it is an error for the application to 
            /// adjust the line by using the EscapeCommFunction function.
            /// </summary>
            Handshake = 2,

            /// <summary>
            /// Specifies that the RTS line will be high if bytes are available for transmission. After 
            /// all buffered bytes have been sent, the RTS line will be low.
            /// </summary>
            Toggle = 3
        }

        private MainThread pipeline = null;

        Stopwatch swTimePassed = new Stopwatch();                           // stopwatch object to give an exact amount to time passed inbetween loops
        private int sampleInterval = 200;                                   // interval between the samples in milliseconds
        int threadLoopDelay = 0;

        private bool running = true;					                    // flag to define if the source thread is still running (setting to false will stop the source thread)
        private bool configured = false;
        private bool initialized = false;

        private bool started = false;				                        // flag to define if the source is started or stopped
        private Object lockStarted = new Object();

        Random rand = new Random(Guid.NewGuid().GetHashCode());
        private int outputChannels = 0;
        private double sampleRate = 0;                                      // hold the amount of samples per second that the source outputs (used by the mainthead to convert seconds to number of samples)

        SerialPort serialPort = null;

        public SerialPortSignal(MainThread pipeline) {

            // set the reference to the pipeline
            this.pipeline = pipeline;

            parameters.addParameter<int> (
                "Channels",
                "Number of source channels to generate",
                "1", "", "1");

            parameters.addParameter<double> (
                "SampleRate",
                "Rate with which samples are generated, in samples per second (hz)",
                "0", "", "5");

            // start a new thread
            Thread thread = new Thread(this.run);
            thread.Start();

        }

        public Parameters getParameters() {
            return parameters;
        }

        public bool configure(out SampleFormat output) {
            configured = true;

            // retrieve the number of output channels
            outputChannels = parameters.getValue<int>("Channels");
            
            // create a sampleformat
            output = new SampleFormat((uint)outputChannels);

            // check if the number of output channels is higher than 0
            if (outputChannels <= 0) {
                logger.Error("Number of output channels cannot be 0");
                return false;
            }

            // retrieve the sample rate
            sampleRate = parameters.getValue<double>("SampleRate");
            if (sampleRate <= 0) {
                logger.Error("The sample rate cannot be 0 or lower");
                return false;
            }

            // calculate the sample interval
            sampleInterval = (int)Math.Floor(1000.0 / sampleRate);
            






    /*        
            char comname[5];

            // store the value of the needed parameters
            samplerate = Parameter( "SamplingRate" );
            comport = Parameter( "ComPort" );
            int protocol = Parameter("Protocol");
            mCount=0;

            PACKET.readstate=0;
            strcpy(comname,"COM ");comname[3]=comport+'0';
	*/



            // return success
            return true;

        }

        public void initialize() {
            Console.WriteLine("init dingen");

            // if a serial port object is still there, close the port first
            if (serialPort != null)     closeSerialPort();

            if (!openSerialPort("COM4", 5)) {
                logger.Error("Could not open Comport");
            }
            
            
            
            
            initialized = true;

        }

        /**
         * function to retrieve the number of samples per second
         * 
         * This value could be requested by the main thread and is used to allow parameters
         * to be converted from seconds to samples
         **/
        public double getSamplesPerSecond() {
            
            // check if the source is not configured yet
            if (!configured) {

                // message
                logger.Error("Trying to retrieve the samples per second before the source was configured, first configure the source, returning 0");

                // return 0
                return 0;

            }

            // return the samples per second
            return sampleRate;

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

            // lock for thread safety
            lock(lockStarted) {

                // check if the source is generating signals
                if (started) {

                    // message
                    //logger.Info("Collection stopped for '" + collectionName + "'");

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
		
            // stop generating (stop will check if it was running in the first place)
		    stop();
		
		    // stop the thread from running
		    running = false;
		
		    // allow the source thread to stop
            Thread.Sleep(100);
		
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
        private void run() {

            // name this thread
            if (Thread.CurrentThread.Name == null) {
                Thread.CurrentThread.Name = "Source Thread";
            }

            // log message
            logger.Debug("Thread started");

            // set an initial start for the stopwatche
            swTimePassed.Start();

		    // loop while running
		    while(running) {

                // lock for thread safety
                lock(lockStarted) {

			        // check if we are generating
			        if (started) {

                        read_channels(5);


                        // set values for the generated sample
                        double[] sample = new double[outputChannels];
                        for (int i = 0; i < outputChannels; i++) {
                            //sample[i] = rand.NextDouble();
                            sample[i] = rand.Next(0,10) + 100;
                        }

                        // pass the sample
                        pipeline.eventNewSample(sample);

			        }

                }


                // 
			    // if still running then sleep to allow other processes
			    if (running && sampleInterval != -1) {

                    // calculate the exact time that has passed since the last run
                    swTimePassed.Stop();
                    int timePassed = (int)swTimePassed.ElapsedMilliseconds;

                    // calculate the time to wait to get the exact sample interval
                    threadLoopDelay = sampleInterval - timePassed;

                    // sleep for the remainder of the sample interval to get as close to the sample rate as possible (if there is a remainder)
                    if (threadLoopDelay >= 0) Thread.Sleep(threadLoopDelay);

                    // start the timer to measure the loop time
                    swTimePassed.Reset();
                    swTimePassed.Start();

			    }
			
		    }

            // log message
            logger.Debug("Thread stopped");

        }

        private bool openSerialPort(string portName, int protocol) {

            int baudRate = DEFAULT_BAUD;
	        int dataBits = 8;
            Parity parity = Parity.None;
            StopBits stopBits = StopBits.One;
            
            if ((protocol == 5)||(protocol == 6))   baudRate = CBR_38400;
            
            // Create a new SerialPort object
            serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
            serialPort.ReadBufferSize = 40960;	// Set read buffer == 10K
            serialPort.WriteBufferSize = 4096;	// Set write buffer == 4K
            serialPort.DtrEnable = false;   // no hardware handshake (DSR/DTR handshake)
            serialPort.RtsEnable = false;   // no hardware handshake (RTS/CTS handshake)

            //try {


                
                serialPort.Open();

                // getting handle is tricky
                var handle = ((SafeFileHandle)serialPort.BaseStream.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(serialPort.BaseStream)).DangerousGetHandle();

                //if (!SetCommMask(handle, EV_RXCHAR)) {
                if (!SetCommMask(handle, 0x0001)) {
                    return false;
                }


                DCB dcb = new DCB();
                
                int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DCB));
                dcb.DCBLength = (uint)size;   
 

                GetCommState(handle, ref dcb);
                if ((protocol == 5) || (protocol == 6))
                    dcb.BaudRate = CBR_38400;
                else
                    dcb.BaudRate = DEFAULT_BAUD;
                int m_dwBaudRate = (int)dcb.BaudRate;
                dcb.ByteSize = 8;
                dcb.Parity = Parity.None;
                dcb.StopBits = StopBits.One;
                dcb.OutxCtsFlow = false;
                dcb.OutxDsrFlow = false;
                dcb.InX = false;
                dcb.OutX = false;
                dcb.DtrControl = DtrControl.Disable;
                dcb.RtsControl = RtsControl.Disable;
                dcb.Binary = true;
                dcb.Parity = Parity.None;
                if (!SetCommState(handle, ref dcb)) {
                    return false;
                }

           
                

       


            
            //Thread readThread = new Thread(Read);
            //readThread.Start();
            
            
            
            
          
            if ((protocol== 5) || (protocol== 6)) {
                logger.Debug("protocol5 or 6");

                //static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);

                // set dongle baud (Actisys 220L)
                EscapeCommFunction(handle, 6);  // CLRDTR
                EscapeCommFunction(handle, 4);  // CLRRTS
                Thread.Sleep(1);
                
                EscapeCommFunction(handle, 5);  // SETDTR
                EscapeCommFunction(handle, 3);  // SETRTS
                Thread.Sleep(10);
                
                // default to 9600
                EscapeCommFunction(handle, 6);  // CLRDTR
                Thread.Sleep(1);
                
                EscapeCommFunction(handle, 5);  // SETDTR
                Thread.Sleep(1);


                switch (m_dwBaudRate)   {
                    case CBR_38400:
                        logger.Debug("CBR38400");
                        EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case CBR_115200;
                    case CBR_115200:
                        EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case CBR_57600;
                    case CBR_57600:
                        EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case CBR_19200;
                    case CBR_19200:
                        EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        EscapeCommFunction(handle, 3);  // SETRTS
                        // fall thru
                        goto case CBR_9600;
                    case CBR_9600:
                        // fall thru
                    default:
                        // All done
                        logger.Debug("alldone");
                        break;
                }
                // end dongle control

            }

            return true;

        }
        
        

        private void closeSerialPort() {

        }

        /*
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e) {
            // Show all the incoming data in the ports buffer
            Console.WriteLine(serialPort.ReadExisting());
        }
        */


        /* reads max. <size> bytes from the serial port,
            returns number of bytes read, or -1 if error */
        //private int readSerial(char *buf, int size) {
        private int readSerial(ref byte[] buf, int size) {

            /*
	        COMSTAT comStat;
	        DWORD errorFlags;
	        DWORD len;

	        ClearCommError(s, &errorFlags, &comStat);   // Only try to read number of bytes in queue
	        len = comStat.cbInQue;
        #ifdef DEBUG
	        if (hNotes != NULL)
		        fprintf(hNotes,"\nQueue length %d, ",len);
        #endif
	        if (len > (DWORD) size) len = size;
	        if (len > 0)
	        {
	        BOOL rv = ReadFile(s, (LPSTR)buf, len, &len, NULL);
        #ifdef DEBUG
		        if (rv>0)
		        {
			        if (hNotes != NULL)
				        fprintf(hNotes,"Buffer %d, ",(unsigned char)buf[0]);
		        }
		        else
		        {
			        if (hNotes != NULL)
				        fprintf(hNotes,"zero bytes read: Buffer %d, ",(unsigned char)buf[0]);
			        buf[0]=1;
		        }
        #endif
		        if (!rv)
		        {
        #ifdef DEBUG
			        if (hNotes != NULL)
				        fprintf(hNotes,"Error reading serial port %d, ",rv);
        #endif
			        len= 0;
			        ClearCommError(s, &errorFlags, &comStat);
		        }
		        if (errorFlags > 0)
		        {
        #ifdef DEBUG
			        if (hNotes != NULL)
				        fprintf(hNotes,"Error flags serial port %d, ",rv);
        #endif
			        ClearCommError(s, &errorFlags, &comStat);
			        return -1;
		        }
	        }
	        return len;
             * 
             */

            return 0;
        }



        //  reads one packet (all 6 channels) from serial port
        //  this function is called n-times by the process() member-function
        //  to get a sample block of size n.
        private void read_channels (int protocol) {
            Console.WriteLine("read_channels");

            byte[] buf = new byte[1];


            if (readSerial(ref buf, 1) == 1) {

            }

            /*
	        unsigned char buf[1];
	        //PrecisionTime time1, time2;
	        PrecisionTime::NumType time1; //,time2;

	        time1 = PrecisionTime::Now();   // get timestamp for timeout-test
	        for (int i = 0; i < PACKET_BUFFER_SIZE; i++)      // to avoid AR crashes
	        {
		        PACKET.buffer[i] = 0;
	        }

	        if ((protocol == 5)||(protocol == 6))
		        PACKET.check_crc = 1;
	        else
		        PACKET.check_crc = 0;

	        do
	        {
		        if (readSerial(handle, (char *)buf, 1)==1)  // read one bytes from serial port
		        {
			        // byte available: parse the selected protocol
			        switch (protocol)
			        {
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
						        bcierr << "Unknown protocol" << endl;
						        break;
			        }
		        }
		        else
			        Sleep(1);   // no byte available: free CPU
		        //  sleep(0) caused a CPU-load of more than 50% on a 1,6 GHZ win2000 laptop
		        //  sleep(1) improved this situation

        #ifdef DEBUG
		        time2 = PrecisionTime::SignedDiff(PrecisionTime::Now(),time1);
		        if (hNotes != NULL)
			        fprintf(hNotes,"Timing %d\n",time2);
        #endif
	        } while ((PACKET.readstate != PACKET_FINISHED) && (PrecisionTime::SignedDiff(PrecisionTime::Now(),time1) < COM_TIMEOUT));

	        // read until the packet is finished or a timeout has occured
        //    while (PACKET.readstate!=PACKET_FINISHED);
	        if (PACKET.check_crc)
	        {
		        unsigned short int crc_calculated = calc_crc();
		        if (crc_calculated != PACKET.crc)
		        {
			        // CRC is wrong
			        bciout << "Corrupt data-packet received (CRC wrong: " << PACKET.crc << " != " << crc_calculated << ")" << endl;

			        // set buffer values to zero
			        for (int i = 0; i < PACKET_BUFFER_SIZE; i++)
				        PACKET.buffer[i] = 0;
		        }
	        }

	        if (PACKET.readstate == PACKET_FINISHED)
	        {
		        // we have a valid packet, now check if it is the 2nd packet sent every 400 ms (and data comes from NEXUS-1 STS)
		        int time_diff = PrecisionTime::SignedDiff(PACKET.arrival_time, PACKET.prev_arrival_time);
		        if ((time_diff > 0) && (time_diff < 150) && (protocol >= 5))
		        {
			        // yes, it is the 2nd packet, wait some time to have the packets equally distributed in time
			        Sleep(200 - time_diff);
		        }
	        }

	        // packet finished, set to start-state
	        PACKET.readstate = 0;
             */
        }

    }

}
