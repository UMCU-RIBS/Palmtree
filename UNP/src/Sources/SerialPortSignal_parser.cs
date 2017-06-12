using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UNP.Core.Helpers;

namespace UNP.Sources {

    public partial class SerialPortSignal {

        
        private SerialPort serialPort = null;
        private IntPtr handle;


        public const int DEFAULT_BAUD = SerialPortNet.CBR_115200;   // default Baud-Rate is 112k
        public const int COM_TIMEOUT = 1000;                        // 1000 ms trasmission timeout
        public const int PACKET_BUFFER_SIZE = 128;                  // packet buffer size which is large enough for P6
                                                                    // (i.e. NEXUS-1 STS time-domain) packages of 120 values

        public const int PACKET_START = 0;
        public const int PACKET_FINISHED = 8;                       // packet-state for completed packet



        // the data structure for tranmission decoding
        private class NexusPacket {
            public byte readstate = PACKET_START;
            public uint extract_pos = 0;
            public byte packetcount = 0;
            public ushort[] buffer = new ushort[PACKET_BUFFER_SIZE];
            public byte switches = 0;
            public byte aux = 0;

            //PrecisionTime::NumType arrival_time;
            //PrecisionTime::NumType prev_arrival_time;

            public bool check_crc = false;                                   // flag whether the protocol supports CRC and sent data has to be checked (false = no crc check, true = do crc check)
            public ushort crc = 0;
            public byte[] data_payload = new byte[2 * PACKET_BUFFER_SIZE];
            public uint payload_pos = 0;
        }
        private static NexusPacket packet = new NexusPacket();










        private bool openSerialPort(string portName, int protocol) {

            int baudRate = DEFAULT_BAUD;
	        int dataBits = 8;
            Parity parity = Parity.None;
            StopBits stopBits = StopBits.One;
            
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
                logger.Error("Could not open port (" + portName + ")");
                return false;
            }


            //try {

                // getting handle is tricky
                //var handle = ((SafeFileHandle)serialPort.BaseStream.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(serialPort.BaseStream)).DangerousGetHandle();
                handle = ((SafeFileHandle)serialPort.BaseStream.GetType().GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(serialPort.BaseStream)).DangerousGetHandle();

                //if (!SetCommMask(handle, EV_RXCHAR)) {
                if (!SerialPortNet.SetCommMask(handle, 0x0001)) {
                    return false;
                }


                SerialPortNet.DCB dcb = new SerialPortNet.DCB();

                int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(SerialPortNet.DCB));
                dcb.DCBLength = (uint)size;


                SerialPortNet.GetCommState(handle, ref dcb);
                if (protocol == 5 || protocol == 6)
                    dcb.BaudRate = SerialPortNet.CBR_38400;
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
                dcb.DtrControl = SerialPortNet.DtrControl.Disable;
                dcb.RtsControl = SerialPortNet.RtsControl.Disable;
                dcb.Binary = true;
                dcb.Parity = Parity.None;
                if (!SerialPortNet.SetCommState(handle, ref dcb)) {
                    return false;
                }
            
          
            if ((protocol== 5) || (protocol== 6)) {
                logger.Debug("protocol5 or 6");

                //static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);

                // set dongle baud (Actisys 220L)
                SerialPortNet.EscapeCommFunction(handle, 6);  // CLRDTR
                SerialPortNet.EscapeCommFunction(handle, 4);  // CLRRTS
                Thread.Sleep(1);

                SerialPortNet.EscapeCommFunction(handle, 5);  // SETDTR
                SerialPortNet.EscapeCommFunction(handle, 3);  // SETRTS
                Thread.Sleep(10);
                
                // default to 9600
                SerialPortNet.EscapeCommFunction(handle, 6);  // CLRDTR
                Thread.Sleep(1);

                SerialPortNet.EscapeCommFunction(handle, 5);  // SETDTR
                Thread.Sleep(1);


                switch (m_dwBaudRate)   {
                    case SerialPortNet.CBR_38400:
                        logger.Debug("CBR38400");
                        SerialPortNet.EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case SerialPortNet.CBR_115200;
                    case SerialPortNet.CBR_115200:
                        SerialPortNet.EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case SerialPortNet.CBR_57600;
                    case SerialPortNet.CBR_57600:
                        SerialPortNet.EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, 3);  // SETRTS
                        Thread.Sleep(1);
                        // fall thru
                        goto case SerialPortNet.CBR_19200;
                    case SerialPortNet.CBR_19200:
                        SerialPortNet.EscapeCommFunction(handle, 4);  // CLRRTS
                        Thread.Sleep(1);
                        SerialPortNet.EscapeCommFunction(handle, 3);  // SETRTS
                        // fall thru
                        goto case SerialPortNet.CBR_9600;
                    case SerialPortNet.CBR_9600:
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

            SerialPortNet.COMSTAT comStat = new SerialPortNet.COMSTAT();
            uint errorFlags = 0;
            uint len;


            SerialPortNet.ClearCommError(handle, out errorFlags, out comStat);
            len = comStat.cbInQue;


            
	        //logger.Debug("Queue length " + len + ", ");
	        if (len > size)     len = (uint)size;
	        if (len > 0) {


                  //_In_        HANDLE       hFile,
                  //_Out_       LPVOID       lpBuffer,
                  //_In_        DWORD        nNumberOfBytesToRead,
                  //_Out_opt_   LPDWORD      lpNumberOfBytesRead,
                  //_Inout_opt_ LPOVERLAPPED lpOverlapped
                //BOOL rv = ReadFile(s, (LPSTR)buf, len, &len, NULL);

                try {
                    int rv = serialPort.Read(buf, 0, size);         // returns The number of bytes read.

                    //logger.Debug("Buffer " + buf[0] + ", ");

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



        //  reads one packet (all 6 channels) from serial port
        //  this function is called n-times by the process() member-function
        //  to get a sample block of size n.
        private void read_channels (int protocol) {
            Console.WriteLine("read_channels");

            byte[] buf = new byte[1];

            // get begin for timeout-test
	        //PrecisionTime::NumType time1; //,time2;
	        //time1 = PrecisionTime::Now();

            //Stopwatch swTimePassed = new Stopwatch();                           // stopwatch object to give an exact amount to time passed inbetween loops

            // zero the packet buffer
            for (int i = 0; i < PACKET_BUFFER_SIZE; i++)    packet.buffer[i] = 0;

            // determine if the protocol allows for crc check
            packet.check_crc = (protocol == 5 || protocol == 6);



            do {

                // try to read one byte from the serial port
                if (readSerial(ref buf, 1) == 1) {  



                    
                    // byte available: parse the selected protocol
                    switch (protocol) {
                        case 1:
                            parse_byte_P1(buf[0]);
                            break;
                            /*
                        case 2:
                            parse_byte_P2(buf[0]);
                            break;
                        case 3:
                            parse_byte_P3(buf[0]);
                            break;
                        case 4:
                            parse_byte_P4(buf[0]);
                            break;
                             */
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
                    
                } else {
                    Thread.Sleep(1);   // no byte available: free CPU
                    //  sleep(0) caused a CPU-load of more than 50% on a 1,6 GHZ win2000 laptop
                    //  sleep(1) improved this situation

                }

                
        //#ifdef DEBUG
		//        time2 = PrecisionTime::SignedDiff(PrecisionTime::Now(),time1);
		//        if (hNotes != NULL)
		//	        fprintf(hNotes,"Timing %d\n",time2);
        //#endif

            } while ((packet.readstate != PACKET_FINISHED));        // read until the packet is finished or a timeout has occured
            //} while ((packet.readstate != PACKET_FINISHED) && (PrecisionTime::SignedDiff(PrecisionTime::Now(),time1) < COM_TIMEOUT));     // read until the packet is finished or a timeout has occured


            

            /*

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


             */

            // packet finished, set to start-state
            packet.readstate = PACKET_START;

        }











        // parse a packet in P1 format
        private void parse_byte_P1(byte actbyte) {
            //	char s[33];

            switch (packet.readstate) {

                case 0:
 
                    if (actbyte==192) {

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
                            packet.buffer[packet.extract_pos >> 1] = (ushort)(actbyte * 256);
                        else 
                            packet.buffer[packet.extract_pos >> 1] += actbyte;

                        packet.extract_pos++;
                    }

                    if (packet.extract_pos == 4) {
                        packet.switches= 0;
                        packet.readstate=PACKET_FINISHED;
                        //  *** PACKET ARRIVED ***
                    }

                    break;
                    
                case PACKET_FINISHED: 
                    break;

                default: 
                    packet.readstate = PACKET_START;
                    break;

		    }   // end of switch

        }
        /*
        void parse_byte_P2(unsigned char actbyte)         // parse a packet in P2 format
        {
            switch (packet.readstate)
	        {
		          case 0: if (actbyte==192) packet.readstate++;  // first sync byte
			          break;
		          case 1: if (actbyte==0xC0)  packet.readstate++; // second sync byte
			          else packet.readstate=0;
			          break;
		          case 2: packet.readstate++;    // Version Number
			          break;
			        case 3: packet.packetcount = actbyte;
			          packet.extract_pos=0;packet.readstate++;
			          break;
		          case 4: if (packet.extract_pos < 12)
				          {   if ((packet.extract_pos & 1) == 0)
					             packet.buffer[packet.extract_pos>>1]=actbyte*256;
			                      else packet.buffer[packet.extract_pos>>1]+=actbyte;
				              packet.extract_pos++;
				          }
				          else
				          {  packet.switches= actbyte;
				             packet.readstate=PACKET_FINISHED;
			 	         //  *** PACKET ARRIVED ***
				          }
		  		        break;
                          case PACKET_FINISHED: break;
		          default: packet.readstate=0;
		        }
        }


        void parse_byte_P3(unsigned char actbyte)   // parse a packet in P3 format
        {
        //    char s[33];
            #ifdef DEBUG
                if (hNotes != NULL) fprintf(hNotes,"readstate: %d, ",packet.readstate);
            #endif
            switch (packet.readstate)
	        {
                case 0: if (actbyte==192)
                    {
                        packet.readstate++;  // first sync byte
                    #ifdef DEBUG
                        if (hNotes != NULL) fprintf(hNotes,"readstate=%d ",packet.readstate);
                    #endif
          		        packet.packetcount = 5;
            	        packet.extract_pos=0;
                    #ifdef DEBUG
                        if (hNotes != NULL) fprintf(hNotes,"%d sync, ",actbyte);
                    #endif
                    }
                    else
                    {
                        packet.readstate=0;
                    #ifdef DEBUG
                        if (hNotes != NULL) fprintf(hNotes,"readstate=0 (3)\n");
                        if (actbyte==0)
                	        if (hNotes != NULL) fprintf(hNotes,"%d zerosync",actbyte);
                        else
                        {
                            if (hNotes != NULL) fprintf(hNotes,"%d nosync",actbyte);
                        }
                    #endif
                    }
			          break;
		          case 1:
                    #ifdef DEBUG
                          if (hNotes != NULL) fprintf(hNotes,"%d: %d; ",packet.extract_pos,actbyte);
                    #endif
                  if (packet.extract_pos < 4) 
					        {
						        if ((packet.extract_pos & 1) == 0)
								        packet.buffer[packet.extract_pos>>1]=actbyte*256;
			              else 
								        packet.buffer[packet.extract_pos>>1]+=actbyte;
						        packet.extract_pos++;
				          }
				          if (packet.extract_pos == 4)
				          {  
						        packet.switches= 0;
				            packet.readstate=PACKET_FINISHED;
                    #ifdef DEBUG
                        if (hNotes != NULL) fprintf(hNotes,"readstate=%d (4)\n",PACKET_FINISHED);
                    #endif
			 	         //  *** PACKET ARRIVED ***
					        }
		  		        break;
                  case PACKET_FINISHED:
                    #ifdef DEBUG
            	        if (hNotes != NULL) fprintf(hNotes,"packet finished\n");
                    #endif
                        break;
		          default: packet.readstate=0;
                    #ifdef DEBUG
                          if (hNotes != NULL) fprintf(hNotes,"readstate=0 (5)\n");
                          break;
                    #endif

		        }
        }


        // parse packet in P4 format
        void parse_byte_P4(unsigned char actbyte)
        {
	        //char temp[200];
	        //sprintf(temp, "P4: byte=%02x  state=%02d  pos=%02d", actbyte, packet.readstate, packet.extract_pos);
	        //bciout << temp << endl;

	        switch (packet.readstate)
	        {
		        case 0:
			        if (actbyte == 192)  // == 0xC0
			        {
				        packet.readstate++;  // one sync byte
				        packet.packetcount = 9;
				        packet.extract_pos = 0;
			        }
			        break;
		        case 1:
			        if (packet.extract_pos < 8)
			        {
				        // Power channels 1 to 4
				        if ((packet.extract_pos & 1) == 0)
					        packet.buffer[packet.extract_pos >> 1] = actbyte * 256;
				        else
					        packet.buffer[packet.extract_pos >> 1] += actbyte;
			        }
			        if (packet.extract_pos == 8)
			        {
				        // Detection Status
				        packet.buffer[4] = actbyte;
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
	        }
        }  // parse_byte_P4()
        */
        
        // parse packet in P5 format (i.e. NEXUS-1 STS Power-Domain)
        private void parse_byte_P5(byte actbyte) {
            switch (packet.readstate) {
            
                case 0:
                    
                    if ((actbyte >= 192) && (actbyte <= 196)) {  // == 0xC0,0xC1,0xC2,0xC3,0xC4

                        // (stim-prog information only available in NEXUS-1 STS)
				        packet.readstate++;  // one sync byte
				        packet.packetcount = 9;
				        packet.extract_pos = 0;
				        //packet.prev_arrival_time = packet.arrival_time;
				        //packet.arrival_time = PrecisionTime::Now();

				        packet.data_payload[0] = actbyte;
				        packet.payload_pos = 1;
			        }
			        
                    break;

		        case 1:
			        
                    if (packet.extract_pos < 8) {

				        // Power channels 1 to 4
				        if ((packet.extract_pos & 1) == 0)
					        packet.buffer[packet.extract_pos >> 1] = (ushort)(actbyte * 256);
				        else
					        packet.buffer[packet.extract_pos >> 1] += actbyte;

				        // store data for CRC
				        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;

			        }

			        if (packet.extract_pos == 8) {

				        // Detection Status
				        packet.buffer[4] = actbyte;

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
            int temp_value = 0;

            switch (packet.readstate) {

                case 0:

			        if ((actbyte >= 192) && (actbyte <= 196)) {  // == 0xC0,0xC1,0xC2,0xC3,0xC4
			        
				        packet.readstate++;  // one sync byte
				        packet.packetcount = 163;  // (2ch * 40 samples * 2 bytes) + detect + 2 bytes-CRC
				        packet.extract_pos = 0;
				        //packet.prev_arrival_time = packet.arrival_time;
				        //packet.arrival_time = PrecisionTime::Now();

				        packet.data_payload[0] = actbyte;
				        packet.payload_pos = 1;

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
                        
                        Debug.Assert(buf_pos < PACKET_BUFFER_SIZE - 1);

                        if (!low_byte)
                            temp_value = actbyte << 8;

                        else {
                            // low-byte arrived, now shift value up by 512 to make an unsigned value
                            temp_value += actbyte;
                            temp_value += 512;
                            packet.buffer[buf_pos] = (ushort)temp_value;
                        }

				        // store data for CRC
				        packet.data_payload[packet.payload_pos] = actbyte;
				        packet.payload_pos++;
			        }

			        if (packet.extract_pos == (packet.packetcount - 3)) {
				        
                        // Detection Status
				        for (int i = 0; i < 40; i++)
					        packet.buffer[i*3+2] = actbyte;

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
        


    }
}
