/**
 * Blackrock source module class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2024:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted parts from:  Cerelink SDK, written by Kirk Korver and Ehsan Azar
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Palmtree.Sources.Wrappers {

    /// <summary>
    /// Class that wraps around the Cerebus Link (CereLink) SDK dynamic link libary.
    /// Wrapper functions and checks around the P/Invokes allow for the robust handling of calls.
    /// </summary>
    public static class Cerelink {

        private const string CLASS_NAME = "Cerelink";
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);


        //
        // default Cerebus networking connection parameters (from cbproto.h)
        //
        public const string cbNET_UDP_ADDR_INST         = "192.168.137.1";          // Cerebus default address
        public const string cbNET_UDP_ADDR_CNT          = "192.168.137.128";        // Gemini NSP default control address
        public const string cbNET_UDP_ADDR_BCAST        = "192.168.137.255";        // NSP default broadcast address
        public const int cbNET_UDP_PORT_BCAST           = 51002;                    // Neuroflow Data Port
        public const int cbNET_UDP_PORT_CNT             = 51001;                    // Neuroflow Control Port
        public const int cbCER_UDP_SIZE_MAX             = 58080;                    // maximum udp datagram size used to transport cerebus packets, taken from MTU size; Note that multiple packets may reside in one udp datagram as aggregate
        public const int cbNET_TCP_PORT_GEMINI          = 51005;                    // Neuroflow Data Port
        public const string cbNET_TCP_ADDR_GEMINI_HUB   = "192.168.137.200";        // NSP default control address
        public const string cbNET_UDP_ADDR_HOST         = "192.168.137.199";        // Cerebus (central) default address
        public const string cbNET_UDP_ADDR_GEMINI_NSP   = "192.168.137.128";        // NSP default control address
        public const string cbNET_UDP_ADDR_GEMINI_HUB   = "192.168.137.200";        // HUB default control address
        public const string cbNET_UDP_ADDR_GEMINI_HUB2  = "192.168.137.201";        // HUB default control address

        //
        // Maximum entity ranges (from cbproto.h)
        // Caution: values of cbMAXPROCS and cbNUM_FE_CHANS depend on pre-processor preprocessor directives (__cplusplus & CBPROTO_311 conditional). The values
        //          of cbMAXPROCS and cbNUM_FE_CHANS here must match those in the cbsdk.dll DLL, or the transfer of data (using cbSdkTrialCont) will not be correct.
        //          - The DLL that came with the SDK (cbsdk.dll) and the DLL resulting from SDK source compilation both seem to assume cbMAXPROCS = 3 and cbNUM_FE_CHANS = 512.
        //          - However cbsdk_cython.pxd and Cerelink.cs show cbMAXPROCS to 1 and cbNUM_FE_CHANS to 256 as constants.
        //          - And central shows 272 analog-in channels, which seems to correspond with cbNUM_ANALOG_CHANS = cbNUM_FE_CHANS (256) + cbNUM_ANAIN_CHANS (16 * cbMAXPROCS; cbMAXPROCS = 1)
        //            but that might use 'cbsdkx64.dll' instead of 'cbsdk.dll' (different preprocessor definitions?)
        //          Currently cbMAXPROCS = 3 and cbNUM_FE_CHANS = 512 seems to work for the cbSdkTrialCont struct
        // TODO: see if it possible to deduce this number (through caps or indirectly by trying) and use two different struct (or dynamically set) to
        //       retrieve data in both scenarios
        public const int cbMAXOPEN                      = 4;                                        // maximum number of open cbhwlib's (nsp's)
        //public const int cbMAXPROCS                     = 1;                                        // number of NSPs for the embedded software
        //public const int cbNUM_FE_CHANS                 = 256;                                      // front-end channels for the NSP
        public const int cbMAXPROCS                     = 3;                                        // number of NSPs for client  (or preprocessor set to 1 for embedded software)
        public const int cbNUM_FE_CHANS                 = 512;                                      // front-end channels for the client (or preprocessor set to 256 for embedded software)
        public const int cbMAXGROUPS                    = 8;                                        // number of sample rate groups
        public const int cbMAXFILTS                     = 32;
        public const int cbMAXVIDEOSOURCE               = 1;                                        // maximum number of video sources
        public const int cbMAXTRACKOBJ                  = 20;                                       // maximum number of trackable objects
        public const int cbMAXHOOPS                     = 4;
        public const int cbMAX_AOUT_TRIGGER             = 5;                                        // maximum number of per-channel (analog output, or digital output) triggers

        public const int cbNUM_ANAIN_CHANS              = 16 * cbMAXPROCS;                          // analog Input channels
        public const int cbNUM_ANALOG_CHANS             = (cbNUM_FE_CHANS + cbNUM_ANAIN_CHANS);     // total Analog Inputs
        public const int cbNUM_ANAOUT_CHANS             = 4 * cbMAXPROCS;                           // analog Output channels
        public const int cbNUM_AUDOUT_CHANS             = 2 * cbMAXPROCS;                           // audio Output channels
        public const int cbNUM_ANALOGOUT_CHANS          = (cbNUM_ANAOUT_CHANS + cbNUM_AUDOUT_CHANS);// total Analog Output
        public const int cbNUM_DIGIN_CHANS              = 1 * cbMAXPROCS;                           // digital Input channels
        public const int cbNUM_SERIAL_CHANS             = 1 * cbMAXPROCS;                           // serial Input channels
        public const int cbNUM_DIGOUT_CHANS             = 4 * cbMAXPROCS;                           // digital Output channels
        public const int cbMAXCHANS                     = (cbNUM_ANALOG_CHANS + cbNUM_ANALOGOUT_CHANS + cbNUM_DIGIN_CHANS + cbNUM_SERIAL_CHANS + cbNUM_DIGOUT_CHANS);   // total of all channels
        public const int cbMAXUNITS                     = 5;                                         // hard coded to 5 in some places

        // string length constants
        public const int cbLEN_STR_UNIT                 = 8;
        public const int cbLEN_STR_LABEL                = 16;
        public const int cbLEN_STR_FILT_LABEL           = 16;
        public const int cbLEN_STR_IDENT                = 64;
        public const int cbLEN_STR_COMMENT              = 256;

        // filter types  (from cbproto.h; flags used in cbFILTDESC.hptype and cbFILTDESC.lptype)
        public const int cbFILTTYPE_PHYSICAL            = 0x0001;
        public const int cbFILTTYPE_DIGITAL             = 0x0002;
        public const int cbFILTTYPE_ADAPTIVE            = 0x0004;
        public const int cbFILTTYPE_NONLINEAR           = 0x0008;
        public const int cbFILTTYPE_BUTTERWORTH         = 0x0100;
        public const int cbFILTTYPE_CHEBYCHEV           = 0x0200;
        public const int cbFILTTYPE_BESSEL              = 0x0400;
        public const int cbFILTTYPE_ELLIPTICAL          = 0x0800;

        // channel capabilities (from cbproto.h; flags used in cbPKT_CHANINFO.chancaps)
        public const int cbCHAN_EXISTS                  = 0x00000001;                       // Channel id is allocated
        public const int cbCHAN_CONNECTED               = 0x00000002;                       // Channel is connected and mapped and ready to use
        public const int cbCHAN_ISOLATED                = 0x00000004;                       // Channel is electrically isolated
        public const int cbCHAN_AINP                    = 0x00000100;                       // Channel has analog input capabilities
        public const int cbCHAN_AOUT                    = 0x00000200;                       // Channel has analog output capabilities
        public const int cbCHAN_DINP                    = 0x00000400;                       // Channel has digital input capabilities
        public const int cbCHAN_DOUT                    = 0x00000800;                       // Channel has digital output capabilities
        public const int cbCHAN_GYRO                    = 0x00001000;                       // Channel has gyroscope/accelerometer/magnetometer/temperature capabilities

        // digital input (from cbproto.h; flags used in cbPKT_CHANINFO.chancaps)
        public const int cbDINP_SERIALMASK              = 0x000000FF;                       // Bit mask used to detect RS232 Serial Baud Rates
        // ommitted other digital input constants...

        // analog input options (from cbproto.h; flags used in cbPKT_CHANINFO.ainpopts)
        public const UInt32 cbAINP_RAWPREVIEW           = 0x00000001;                       // Generate scrolling preview data for the raw channel
        public const UInt32 cbAINP_LNC                  = 0x00000002;                       // Line Noise Cancellation
        public const UInt32 cbAINP_LNCPREVIEW           = 0x00000004;                       // Retrieve the LNC correction waveform
        public const UInt32 cbAINP_SMPSTREAM            = 0x00000010;                       // stream the analog input stream directly to disk
        public const UInt32 cbAINP_SMPFILTER            = 0x00000020;                       // Digitally filter the analog input stream
        public const UInt32 cbAINP_RAWSTREAM            = 0x00000040;                       // Raw data stream available
        public const UInt32 cbAINP_SPKSTREAM            = 0x00000100;                       // Spike Stream is available
        public const UInt32 cbAINP_SPKFILTER            = 0x00000200;                       // Selectable Filters
        public const UInt32 cbAINP_SPKPREVIEW           = 0x00000400;                       // Generate scrolling preview of the spike channel
        public const UInt32 cbAINP_SPKPROC              = 0x00000800;                       // Channel is able to do online spike processing
        public const UInt32 cbAINP_OFFSET_CORRECT_CAP   = 0x00001000;                       // Offset correction mode (0-disabled 1-enabled)

        public const UInt32 cbAINP_LNC_OFF              = 0x00000000;                       // Line Noise Cancellation disabled
        public const UInt32 cbAINP_LNC_RUN_HARD         = 0x00000001;                       // Hardware-based LNC running and adapting according to the adaptation const
        public const UInt32 cbAINP_LNC_RUN_SOFT         = 0x00000002;                       // Software-based LNC running and adapting according to the adaptation const
        public const UInt32 cbAINP_LNC_HOLD             = 0x00000004;                       // LNC running, but not adapting
        public const UInt32 cbAINP_LNC_MASK             = 0x00000007;                       // Mask for LNC Flags
        public const UInt32 cbAINP_REFELEC_LFPSPK       = 0x00000010;                       // Apply reference electrode to LFP & Spike
        public const UInt32 cbAINP_REFELEC_SPK          = 0x00000020;                       // Apply reference electrode to Spikes only
        public const UInt32 cbAINP_REFELEC_MASK         = 0x00000030;                       // Mask for Reference Electrode flags
        public const UInt32 cbAINP_RAWSTREAM_ENABLED    = 0x00000040;                       // Raw data stream enabled
        public const UInt32 cbAINP_OFFSET_CORRECT       = 0x00000100;                       // Offset correction mode (0-disabled 1-enabled)

        // analog input spike options (from cbproto.h; flags usedin cbPKT_CHANINFO.spkopts)
        public const UInt32 cbAINPSPK_EXTRACT           = 0x00000001;                       // Time-stamp and packet to first superthreshold peak
        public const UInt32 cbAINPSPK_REJART            = 0x00000002;                       // Reject around clipped signals on multiple channels
        public const UInt32 cbAINPSPK_REJCLIP           = 0x00000004;                       // Reject clipped signals on the channel
        public const UInt32 cbAINPSPK_ALIGNPK           = 0x00000008;                       //
        public const UInt32 cbAINPSPK_REJAMP            = 0x00000010;                       // Reject based on amplitude
        public const UInt32 cbAINPSPK_THRLEVEL          = 0x00000100;                       // Analog level threshold detection
        public const UInt32 cbAINPSPK_THRENERGY         = 0x00000200;                       // Energy threshold detection
        public const UInt32 cbAINPSPK_THRAUTO           = 0x00000400;                       // Auto threshold detection
        public const UInt32 cbAINPSPK_SPREADSORT        = 0x00001000;                       // Enable Auto spread Sorting
        public const UInt32 cbAINPSPK_CORRSORT          = 0x00002000;                       // Enable Auto Histogram Correlation Sorting
        public const UInt32 cbAINPSPK_PEAKMAJSORT       = 0x00004000;                       // Enable Auto Histogram Peak Major Sorting
        public const UInt32 cbAINPSPK_PEAKFISHSORT      = 0x00008000;                       // Enable Auto Histogram Peak Fisher Sorting
        public const UInt32 cbAINPSPK_HOOPSORT          = 0x00010000;                       // Enable Manual Hoop Sorting
        public const UInt32 cbAINPSPK_PCAMANSORT        = 0x00020000;                       // Enable Manual PCA Sorting
        public const UInt32 cbAINPSPK_PCAKMEANSORT      = 0x00040000;                       // Enable K-means PCA Sorting
        public const UInt32 cbAINPSPK_PCAEMSORT         = 0x00080000;                       // Enable EM-clustering PCA Sorting
        public const UInt32 cbAINPSPK_PCADBSORT         = 0x00100000;                       // Enable DBSCAN PCA Sorting
        public const UInt32 cbAINPSPK_AUTOSORT          = (cbAINPSPK_SPREADSORT | cbAINPSPK_CORRSORT | cbAINPSPK_PEAKMAJSORT | cbAINPSPK_PEAKFISHSORT); // old auto sorting methods
        public const UInt32 cbAINPSPK_NOSORT            = 0x00000000;                                                                                   // No sorting
        public const UInt32 cbAINPSPK_PCAAUTOSORT       = (cbAINPSPK_PCAKMEANSORT | cbAINPSPK_PCAEMSORT | cbAINPSPK_PCADBSORT);                         // All PCA sorting auto algorithms
        public const UInt32 cbAINPSPK_PCASORT           = (cbAINPSPK_PCAMANSORT | cbAINPSPK_PCAAUTOSORT);                                               // All PCA sorting algorithms
        public const UInt32 cbAINPSPK_ALLSORT           = (cbAINPSPK_AUTOSORT | cbAINPSPK_HOOPSORT | cbAINPSPK_PCASORT);                                // All sorting algorithms

        // sampling constanst (from cbsdk.h)
        public const UInt32 cbSdk_CONTINUOUS_DATA_SAMPLES   = 102400;                       // default number of continuous samples that will be stored per channel in the trial buffer (multiple of 4096)
        public const UInt32 cbSdk_EVENT_DATA_SAMPLES        = (2 * 8192);                   // default number of events that will be stored per channel in the trial buffer (multiple of 4096)


        //
        //
        //

        // version structure
        [StructLayout(LayoutKind.Sequential)]
        public struct cbSdkVersion {
            // Library version
            public UInt32 major;
            public UInt32 minor;
            public UInt32 release;
            public UInt32 beta;
            // Protocol version
            public UInt32 majorp;
            public UInt32 minorp;
            // NSP version
            public UInt32 nspmajor;
            public UInt32 nspminor;
            public UInt32 nsprelease;
            public UInt32 nspbeta;
            // NSP protocol version
            public UInt32 nspmajorp;
            public UInt32 nspminorp;
        }

        public enum cbSdkResult : int {
            CBSDKRESULT_WARNCONVERT = 3,                // If file conversion is needed
            CBSDKRESULT_WARNCLOSED = 2,                 // Library is already closed
            CBSDKRESULT_WARNOPEN = 1,                   // Library is already opened
            CBSDKRESULT_SUCCESS = 0,                    // Successful operation
            CBSDKRESULT_NOTIMPLEMENTED = -1,            // Not implemented
            CBSDKRESULT_UNKNOWN = -2,                   // Unknown error
            CBSDKRESULT_INVALIDPARAM = -3,              // Invalid parameter
            CBSDKRESULT_CLOSED = -4,                    // Interface is closed cannot do this operation
            CBSDKRESULT_OPEN = -5,                      // Interface is open cannot do this operation
            CBSDKRESULT_NULLPTR = -6,                   // Null pointer
            CBSDKRESULT_ERROPENCENTRAL = -7,            // Unable to open Central interface
            CBSDKRESULT_ERROPENUDP = -8,                // Unable to open UDP interface (might happen if default)
            CBSDKRESULT_ERROPENUDPPORT = -9,            // Unable to open UDP port
            CBSDKRESULT_ERRMEMORYTRIAL = -10,           // Unable to allocate RAM for trial cache data
            CBSDKRESULT_ERROPENUDPTHREAD = -11,         // Unable to open UDP timer thread
            CBSDKRESULT_ERROPENCENTRALTHREAD = -12,     // Unable to open Central communication thread
            CBSDKRESULT_INVALIDCHANNEL = -13,           // Invalid channel number
            CBSDKRESULT_INVALIDCOMMENT = -14,           // Comment too long or invalid
            CBSDKRESULT_INVALIDFILENAME = -15,          // Filename too long or invalid
            CBSDKRESULT_INVALIDCALLBACKTYPE = -16,      // Invalid callback type
            CBSDKRESULT_CALLBACKREGFAILED = -17,        // Callback register/unregister failed
            CBSDKRESULT_ERRCONFIG = -18,                // Trying to run an unconfigured method
            CBSDKRESULT_INVALIDTRACKABLE = -19,         // Invalid trackable id, or trackable not present
            CBSDKRESULT_INVALIDVIDEOSRC = -20,          // Invalid video source id, or video source not present
            CBSDKRESULT_ERROPENFILE = -21,              // Cannot open file
            CBSDKRESULT_ERRFORMATFILE = -22,            // Wrong file format
            CBSDKRESULT_OPTERRUDP = -23,                // Socket option error (possibly permission issue)
            CBSDKRESULT_MEMERRUDP = -24,                // Socket memory assignment error
            CBSDKRESULT_INVALIDINST = -25,              // Invalid range or instrument address
            CBSDKRESULT_ERRMEMORY = -26,                // library memory allocation error
            CBSDKRESULT_ERRINIT = -27,                  // Library initialization error
            CBSDKRESULT_TIMEOUT = -28,                  // Conection timeout error
            CBSDKRESULT_BUSY = -29,                     // Resource is busy
            CBSDKRESULT_ERROFFLINE = -30,               // Instrument is offline
            CBSDKRESULT_INSTOUTDATED = -31,             // The instrument runs an outdated protocol version
            CBSDKRESULT_LIBOUTDATED = -32,              // The library is outdated
        }

        // cbSdk Connection Type (Central, UDP, other)
        public enum cbSdkConnectionType : int {
            CBSDKCONNECTION_DEFAULT = 0,                // Try Central then UDP
            CBSDKCONNECTION_CENTRAL = 1,                // Use Central
            CBSDKCONNECTION_UDP     = 2,                // Use UDP
            CBSDKCONNECTION_CLOSED  = 3,                // Closed
            CBSDKCONNECTION_COUNT                       // Allways the last value (Unknown)
        }

        // Instrument Type
        public enum cbSdkInstrumentType : int {
            CBSDKINSTRUMENT_NSP         = 0,            // NSP
            CBSDKINSTRUMENT_NPLAY       = 1,            // Local nPlay
            CBSDKINSTRUMENT_LOCALNSP    = 2,            // Local NSP
            CBSDKINSTRUMENT_REMOTENPLAY = 3,            // Remote nPlay
            CBSDKINSTRUMENT_COUNT                       // Allways the last value (Invalid)
        }

        // connection information structure (from cbsdk.h)
        [StructLayout(LayoutKind.Sequential)]
        public struct cbSdkConnection {
            public int nInPort;             // Central/Client port number
            public int nOutPort;            // Instrument port number
            public int nRecBufSize;         // Receive buffer size (0 to ignore altogether)
            public string szInIP;           // Central/Client IPv4 address
            public string szOutIP;          // Instrument IPv4 address
            public int nRange;              // Range of IP addresses to try to open
        }
        
        // trial continuous data (from cbsdk.h)
        [StructLayout(LayoutKind.Sequential)]
        public struct cbSdkTrialCont {
            public UInt16 count;                                                            // Number of valid channels in this trial (up to cbNUM_ANALOG_CHANS)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbNUM_ANALOG_CHANS)]
            public UInt16[] chan;                                                           // Channel numbers (1-based)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbNUM_ANALOG_CHANS)]
            public UInt16[] sample_rates;                                                   // Current sample rate (samples per second)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbNUM_ANALOG_CHANS)]
            public UInt32[] num_samples;                                                    // Number of samples
            public UInt32 time;                                                             // Start time for trial continuous data
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbNUM_ANALOG_CHANS)]
            public IntPtr[] samples;                                                        // Buffer to hold sample vectors
        }

        // packet header data structure
        [StructLayout(LayoutKind.Sequential)]
        public struct cbPKT_HEADER {
            public UInt64  time;                    // system clock timestamp
            public UInt16  chid;                    // channel identifier
            public UInt16  type;                    // packet type
            public UInt16  dlen;                    // length of data field in 32-bit chunks
            public Byte    instrument;              // instrument number to transmit this packets
            public Byte    reserved;                // reserved for future
        }

        /// scaling structure
        [StructLayout(LayoutKind.Sequential)]
        public struct cbSCALING {
            public Int16   digmin;                  // digital value that cooresponds with the anamin value
            public Int16   digmax;                  // digital value that cooresponds with the anamax value
            public Int32   anamin;                  // the minimum analog value present in the signal
            public Int32   anamax;                  // the maximum analog value present in the signal
            public Int32   anagain;                 // the gain applied to the default analog values to get the analog values
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbLEN_STR_UNIT)]
            public char[]  anaunit;                 // the unit for the analog signal (eg, "uV" or "MPa")
        }

        // filter description structure
        [StructLayout(LayoutKind.Sequential)]
        public struct cbFILTDESC {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbLEN_STR_FILT_LABEL)]
            public char[]  label;
            public UInt32  hpfreq;                  // high-pass corner frequency in milliHertz
            public UInt32  hporder;                 // high-pass filter order
            public UInt32  hptype;                  // high-pass filter type
            public UInt32  lpfreq;                  // low-pass frequency in milliHertz
            public UInt32  lporder;                 // low-pass filter order
            public UInt32  lptype;                  // low-pass filter type
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct cbMANUALUNITMAPPING {
            public Int16            nOverride;      // override to unit if in ellipsoid
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=3)]
            public Int16[]          afOrigin;       // ellipsoid origin
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3 * 3)]    // [3][3]
            public Int16[]          afShape;        // ellipsoid shape
            public Int16            aPhi;           //
            public UInt32           bValid;         // is this unit in use at this time?
        }

        /// hoop definition structure
        [StructLayout(LayoutKind.Sequential)]
        public struct cbHOOP {
            public UInt16           valid;          // 0=undefined, 1 for valid
            public Int16            time;           // time offset into spike window
            public Int16            min;            // minimum value for the hoop window
            public Int16            max;            // maximum value for the hoop window
        }

        // channel Information
        [StructLayout(LayoutKind.Sequential)]
        public struct cbPKT_CHANINFO {
            public cbPKT_HEADER cbpkt_header;       // packet header
            public UInt32       chan;               // actual channel id of the channel being configured
            public UInt32       proc;               // the address of the processor on which the channel resides
            public UInt32       bank;               // the address of the bank on which the channel resides
            public UInt32       term;               // the terminal number of the channel within it's bank
            public UInt32       chancaps;           // general channel capablities (given by cbCHAN_* flags)
            public UInt32       doutcaps;           // digital output capablities (composed of cbDOUT_* flags)
            public UInt32       dinpcaps;           // digital input capablities (composed of cbDINP_* flags)
            public UInt32       aoutcaps;           // analog output capablities (composed of cbAOUT_* flags)
            public UInt32       ainpcaps;           // analog input capablities (composed of cbAINP_* flags)
            public UInt32       spkcaps;            // spike processing capabilities
            public cbSCALING    physcalin;          // physical channel scaling information
            public cbFILTDESC   phyfiltin;          // physical channel filter definition
            public cbSCALING    physcalout;         // physical channel scaling information
            public cbFILTDESC   phyfiltout;         // physical channel filter definition
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = cbLEN_STR_LABEL)]
            public char[]       label;              // label of the channel (null terminated if <16 characters)
            public UInt32       userflags;          // user flags for the channel state
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public Int32[]      position;           // reserved for future position information
            public cbSCALING    scalin;             // user-defined scaling information for AINP
            public cbSCALING    scalout;            // user-defined scaling information for AOUT
            public UInt32       doutopts;           // digital output options (composed of cbDOUT_* flags)
            public UInt32       dinpopts;           // digital input options (composed of cbDINP_* flags)
            public UInt32       aoutopts;           // analog output options
            public UInt32       eopchar;            // digital input capablities (given by cbDINP_* flags)

            // union here of two structs with the 3 variables of the same consecutive types. Simply map as three variables with combined names
            UInt16              moninst_lowsamples; // instrument of channel to monitor   or   number of samples to set low for timed output
            UInt16              monchan_highsamples;// channel to monitor   or   number of samples to set high for timed output
            Int32               outvalue_offset;    // output value   or   number of samples to offset the transitions for timed output

            public Byte         trigtype;           // trigger type (see cbDOUT_TRIGGER_*)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public Byte[]       reserved;           // 2 bytes reserved
            public Byte         triginst;           // instrument of the trigger channel
            public UInt16       trigchan;           // trigger channel
            public UInt16       trigval;            // trigger value
            public UInt32       ainpopts;           // analog input options (composed of cbAINP* flags)
            public UInt32       lncrate;            // line noise cancellation filter adaptation rate
            public UInt32       smpfilter;          // continuous-time pathway filter id
            public UInt32       smpgroup;           // continuous-time pathway sample group
            public Int32        smpdispmin;         // continuous-time pathway display factor
            public Int32        smpdispmax;         // continuous-time pathway display factor
            public UInt32       spkfilter;          // spike pathway filter id
            public Int32        spkdispmax;         // spike pathway display factor
            public Int32        lncdispmax;         // Line Noise pathway display factor
            public UInt32       spkopts;            // spike processing options
            public Int32        spkthrlevel;        // spike threshold level
            public Int32        spkthrlimit;        //
            public UInt32       spkgroup;           // NTrodeGroup this electrode belongs to - 0 is single unit, non-0 indicates a multi-trode grouping
            public Int16        amplrejpos;         // Amplitude rejection positive value
            public Int16        amplrejneg;         // Amplitude rejection negative value
            public UInt32       refelecchan;        // Software reference electrode channel
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = cbMAXUNITS)]
            public cbMANUALUNITMAPPING[] unitmapping;  // manual unit mapping
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = cbMAXUNITS * cbMAXHOOPS)]
            public cbHOOP[]     spkhoops;           // spike hoop sorting set
        }


        //
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        //
        // managed prototypes/definitions to unmanaged Cerelink SDK DLL functions
        //
        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetVersion", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetVersion(UInt32 nInstance, ref cbSdkVersion ver);
        
        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetType", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetType(UInt32 nInstance, ref cbSdkConnectionType conType, ref cbSdkInstrumentType instType);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkOpen", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkOpen(UInt32 nInstance, cbSdkConnectionType conType, cbSdkConnection con);
        
        [DllImport("cbsdk.dll", EntryPoint="cbSdkClose", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkClose(UInt32 nInstance);
        
        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetChannelConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetChannelConfig(UInt32 nInstance, UInt16 channel, ref cbPKT_CHANINFO chaninfo);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkSetChannelConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkSetChannelConfig(UInt32 nInstance, UInt16 channel, ref cbPKT_CHANINFO chaninfo);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetSampleGroupList", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetSampleGroupList(UInt32 nInstance, UInt32 proc, UInt32 group, ref UInt32 length, UInt16[] list);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetSampleGroupInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetSampleGroupInfo(UInt32 nInstance, UInt32 proc, UInt32 group, char[] label, ref UInt32 period, ref UInt32 length);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetFilterDesc", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetFilterDesc(UInt32 nInstancev, UInt32 proc, UInt32 filt, ref cbFILTDESC chaninfo);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetTrialConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetTrialConfig(UInt32 nInstance, ref UInt32 pbActive, ref UInt16 pBegchan, ref UInt32 pBegmask, ref UInt32 pBegval, 
                                                             ref UInt16 pEndchan, ref UInt32 pEndmask, ref UInt32 pEndval, ref bool pbDouble, ref UInt32 puWaveforms, 
                                                             ref UInt32 puConts, ref UInt32 puEvents, ref UInt32 puComments, ref UInt32 puTrackings, ref bool pbAbsolute);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkSetTrialConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkSetTrialConfig(UInt32 nInstance, UInt32 bActive, UInt16 begchan, UInt32 begmask, UInt32 begval,
                                                             UInt16 endchan, UInt32 endmask, UInt32 endval, bool bDouble, UInt32 uWaveforms, 
                                                             UInt32 uConts, UInt32 uEvents, UInt32 uComments, UInt32 uTrackings, bool bAbsolute);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkInitTrialData", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkInitTrialContinuousData(UInt32 nInstance, UInt32 bActive, IntPtr trialevent, ref cbSdkTrialCont trialcont, IntPtr trialcomment, IntPtr trialtracking);

        [DllImport("cbsdk.dll", EntryPoint="cbSdkGetTrialData", CallingConvention = CallingConvention.Cdecl)]
        public static extern cbSdkResult cbSdkGetTrialContinuousData(UInt32 nInstance, UInt32 bActive, IntPtr trialevent, ref cbSdkTrialCont trialcont, IntPtr trialcomment, IntPtr trialtracking);



        ///////
        //       Wrapper functions
        //////

        public static void addLibraryPath(string path) {
            if (!SetDllDirectory(path))
                logger.Error("Could not set DLL directory ('" + path + "')");
        }

        /// <summary>
        /// Initialize the Cerebus Link (CereLink) libary.
        /// This function pre-links the DLL imports to check if the CereLink DLL can be found and the P/Invoke prototype definitions
        /// match the DLL entry-points. The DLL is then loaded and the version is retrieved trough the DLL to perform version checks
        /// </summary>
        /// <returns>True if the Cerebus Link (CereLink) libary was loaded succesfully and the wrapper is ready for use, false otherwise</returns>
        public static bool initialize() {

            /*
            // basic method that uses loadlibary to load the DLL
            string dllPath = path + Path.DirectorySeparatorChar + "cbsdk.dll";
            IntPtr pDll = LoadLibrary(dllPath);
            if (pDll == IntPtr.Zero) {
                MessageBox.Show("LoadLibrary failed to load dll ('" + dllPath + "') with error: " + GetLastError() + ".\n\n.", "DLL error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // note: FreeLibrary is to be expected, but since this is a static class which - if used - would run till the end of the
            //       execution process, the resource would be free anyway at the right moment
            //   
            //FreeLibrary(pDll);
            */


            // verify all the platform invoke prototypes
            // will load the DLL and verifies that it contains the requested method
            try {
                Marshal.PrelinkAll(typeof(Cerelink));
                logger.Info("Cerebus DLL pre-linked successfully");

                // retrieve the version
                cbSdkVersion? sdkVersion = getVersion();
                if (!sdkVersion.HasValue)
                    return false;

                // return success
                return true;

            } catch (DllNotFoundException e) {
                // message DLL could not be found
                // pass error so we might allow the user to add environmental path and try again
                logger.Error("Error while loading Cerebus library, the DLL file 'cbsdk.dll' or one its dependencies could not be found");
                throw new DllNotFoundException("DLL not found");

            } catch (EntryPointNotFoundException  e) {
                // message one or more entry points not found, check DLL version
                logger.Error("Error while loading Cerebus library, entry point not found. Most likely caused by an incompatible version of the SDK/DLL");
                logger.Error("   - " + e.Message);
                return false;

            } catch (BadImageFormatException  e) {
                // message 64-bit?
                // TODO: check bit: Environment.Is64BitProcess; 
                logger.Error("Error while loading Cerebus library, bad image format exception. This is most likely because of a 32-bit compilation of Palmtree trying to communicate with a 64-bit DLL. Recompile Palmtree as a 64-bit application.");
                logger.Error("   - " + e.Message);
                return false;
            } catch (Exception e) {
                // other errors
                logger.Error("Unknown error while loading Cerebus library. Check the log for more information.");
                logger.Error("   - " + e.Message);
                return false;
            }

        }


        

        ///////
        //       Wrappers
        //////


        /// <summary>
        /// Get library version
        /// </summary>
        /// <param name="nInstance">Library instance, numbered 0 (default) to 3</param>
        /// <returns>A version struct on success, or null on failure </returns>
        public static cbSdkVersion? getVersion(UInt32 nInstance = 0) {

            //
            cbSdkResult res;
            cbSdkVersion sdkVersion = new cbSdkVersion();
            try {
                res = cbSdkGetVersion(nInstance, ref sdkVersion);
            } catch (Exception e) {
                logger.Error("An error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }

            //
            //if (res == cbSdkResult.CBSDKRESULT_WARNCLOSED)
                //logger.Warn("Library is already closed");

            return sdkVersion;
        }

        /// <summary>
        /// Get connection and instrument type (of the open connection)
        /// </summary>
        /// <param name="nInstance">Library instance, numbered 0 (default) to 3</param>
        /// <returns>If succesfull, a tuple with two values: connection-type and instrument-type. On failure, will return null</returns>
        public static Tuple<cbSdkConnectionType, cbSdkInstrumentType> getGetType(UInt32 nInstance = 0) {

            //
            cbSdkResult res;
            cbSdkConnectionType conType = cbSdkConnectionType.CBSDKCONNECTION_COUNT;
            cbSdkInstrumentType instType = cbSdkInstrumentType.CBSDKINSTRUMENT_COUNT;
            try {
                res = cbSdkGetType(nInstance, ref conType, ref instType);
            } catch (Exception e) {
                logger.Error("An error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }

            return Tuple.Create(conType, instType);
        }

        /// <summary>
        /// Create a Cerelink connection struct with default connection settings.
        /// These settings can be used to configure the address and port of each instrument, and the address and port of
        /// your application or Central to which the instrument will send its data using central-addr and central-port
        /// </summary>
        /// <returns>A Cerelink connection struct</returns>
        public static cbSdkConnection defaultConnection() {
            cbSdkConnection con = new cbSdkConnection();
            con.szInIP      = "";                       // empty string as default central/client IPv4 address, the SDK DLL will default this to cbNET_UDP_ADDR_INST on windows systems
            con.nInPort     = cbNET_UDP_PORT_BCAST;
            con.szOutIP     = "";                       // empty string as default Instrument IPv4 address, the SDK DLL will default this to cbNET_UDP_ADDR_CNT
            con.nOutPort    = cbNET_UDP_PORT_CNT;
            con.nRecBufSize = (8 * 1024 * 1024);        // 8MB default needed for best performance (win32)
            con.nRange      = 0;
            return con;
        }

        /// <summary>
        /// Open the library and connect to an instrument
        /// </summary>
        public static bool open() {     return open(0, cbSdkConnectionType.CBSDKCONNECTION_DEFAULT, defaultConnection());  }

        /// <summary>
        /// Open the library and connect to an instrument
        /// </summary>
        /// <param name="nInstance">Library instance (0-3, 0=default). Can be used to allow the application to connect to multiple instruments at once (up to 4)</param>
        public static bool open(UInt32 nInstance = 0) {    return open(nInstance, cbSdkConnectionType.CBSDKCONNECTION_DEFAULT, defaultConnection());   }

        /// <summary>
        /// Open the library and connect to an instrument
        /// </summary>
        /// <param name="nInstance">Library instance (0-3, 0=default). Can be used to allow the application to connect to multiple instruments at once (up to 4)</param>
        /// <param name="conType">Connection type. Either through Central software, through the UDP data stream, or first try through Central then UDP interface (default)</param>
        public static bool open(UInt32 nInstance = 0, cbSdkConnectionType conType = cbSdkConnectionType.CBSDKCONNECTION_DEFAULT) {     return open(nInstance, conType, defaultConnection());   }

        /// <summary>
        /// Open the library and connect to an instrument
        /// </summary>
        /// <param name="nInstance">Library instance (0-3, 0=default). Can be used to allow the application to connect to multiple instruments at once (up to 4)</param>
        /// <param name="conType">Connection type. Either through Central software, through the UDP data stream, or first try through Central then UDP interface (default)</param>
        /// <param name="con">Connection information. Can be used to configure the address and port of each instrument, and the address and port of your application or Central to which the instrument will send its data using central-addr and central-port</param>
        /// <returns>True if the connection is succesfully opened, false otherwise</returns>
        public static bool open(UInt32 nInstance, cbSdkConnectionType conType, cbSdkConnection con) {

            // retrieve version (without a connection, only to report the library versioning)
            cbSdkVersion ver = new cbSdkVersion();
            cbSdkVersion? verResult = getVersion(nInstance);
            if (verResult.HasValue)
                ver = verResult.Value;

            // print libary version
            if (conType == cbSdkConnectionType.CBSDKCONNECTION_CENTRAL)
                logger.Info("Initializing real-time interface {0}.{1}.{2}.{3} (protocol cb{4}.{5})", ver.major, ver.minor, ver.release, ver.beta, ver.majorp, ver.minorp);
            else if (conType == cbSdkConnectionType.CBSDKCONNECTION_UDP)
                logger.Info("Initializing UDP real-time interface {0}.{1}.{2}.{3} (protocol cb{4}.{5})", ver.major, ver.minor, ver.release, ver.beta, ver.majorp, ver.minorp);
            else
                logger.Info("Initializing interface {0}.{1}.{2}.{3} (protocol cb{4}.{5})", ver.major, ver.minor, ver.release, ver.beta, ver.majorp, ver.minorp);

            // open instance
            cbSdkResult res = cbSdkOpen(nInstance, conType, con);
            if (res == cbSdkResult.CBSDKRESULT_WARNOPEN) {
                logger.Warn("Real-time interface already initialized (CBSDKRESULT_WARNOPEN");
                // TODO: why return? can't we just continue?
                return false;
            } else if (res < 0) {
                logger.Error("Error while opening library instance: " + getResultMessasge(res));
                return false;
            }

            // retrieve the version again (should contain more information now that it is connected)
            ver = new cbSdkVersion();
            verResult = getVersion(nInstance);
            if (verResult.HasValue)
                ver = verResult.Value;

            // retrieve the types on the actual opened connection
            conType = cbSdkConnectionType.CBSDKCONNECTION_COUNT;
            cbSdkInstrumentType instType = cbSdkInstrumentType.CBSDKINSTRUMENT_COUNT;
            Tuple<cbSdkConnectionType, cbSdkInstrumentType> types = getGetType(nInstance);
            if (types != null) {
                conType = types.Item1;
                instType = types.Item2;
            }
            if (conType < 0 || conType > cbSdkConnectionType.CBSDKCONNECTION_CLOSED)
                conType = cbSdkConnectionType.CBSDKCONNECTION_COUNT;
            if (instType < 0 || instType > cbSdkInstrumentType.CBSDKINSTRUMENT_COUNT)
                instType = cbSdkInstrumentType.CBSDKINSTRUMENT_COUNT;
            
            logger.Info("{0} real-time interface to {1} ({2}.{3}.{4}.{5} hwlib {6}.{7})", 
                                getConnectionTypeString(conType), getInstrumentTypeString(instType), 
                                ver.nspmajor, ver.nspminor, ver.nsprelease, ver.nspbeta, ver.nspmajorp, ver.nspminorp);

            // 
            logger.Info("Real-time interface opened");
            return true;

        }

        /// <summary>
        /// Open library
        /// </summary>
        /// <param name="nInstance">Library instance to close (0-3, 0=default)</param>
        public static void close(UInt32 nInstance = 0) {

            // 
            cbSdkResult res;
            try {
                 res = cbSdkClose(nInstance);
            } catch (Exception e) {
                logger.Error("Unable to close library, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return;
            }

            //
            if (res == cbSdkResult.CBSDKRESULT_WARNCLOSED) {
                logger.Warn("Real-time interface already closed");
                return;
            } else if (res < 0) {
                logger.Error("Error while closing library instance: " + getResultMessasge(res));
                return;
            }
            logger.Info("Real-time interface closed");

        }

        /// <summary>
        /// Retrieve the channel configuration
        /// </summary>
        /// <param name="channel">Index of the channel to retrieve the configuration from (0-based, ranges from 0 to cbMAXCHANS; de facto seems to be 283)</param>
        /// <param name="nInstance">Library instance to use</param>
        public static cbPKT_CHANINFO? getChannelConfig(UInt16 channel, UInt32 nInstance=0) {

            // retrieve channel configuration
            cbSdkResult res;
            cbPKT_CHANINFO chanInfo = new cbPKT_CHANINFO();
            try {
                res = cbSdkGetChannelConfig(nInstance, channel, ref chanInfo);
            } catch (Exception e) {
                logger.Error("Unable to retrieve channel configuration, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }

            // 
            if (res < 0) {
                logger.Error("Error while retrieving channel configuration: " + getResultMessasge(res));
                return null;
            }
            
            return chanInfo;
        }


        /// <summary>
        /// Set the channel configuration
        /// </summary>
        /// <param name="channel">Index of the channel of which to set the configuration (0-based, ranges from 0 to cbMAXCHANS; de facto seems to be 283)</param>
        /// <param name="nInstance">Library instance to use</param>
        /// <returns>True if the channel configuration was set succesfully, false otherwise</returns>
        public static bool setChannelConfig(UInt16 channel, cbPKT_CHANINFO chanInfo, UInt32 nInstance=0) {

            // set channel configuration
            cbSdkResult res;
            try {
                res = cbSdkSetChannelConfig(nInstance, channel, ref chanInfo);
            } catch (Exception e) {
                logger.Error("Unable to set channel configuration, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return false;
            }

            //
            if (res < 0) {
                logger.Error("Error while setting channel configuration: " + getResultMessasge(res));
                return false;
            }
            
            return true;
        }


        /// <summary>
        /// Retrieve a list (of indices) of the channels that are set to the given sample-group
        /// </summary>
        /// <param name="group">The group index for which to retrieve the channel-indices that are set to this group (1-based, ranges from 1 to cbMAXGROUPS=8, or actually 6 according to matlab doc)</param>
        /// <param name="nInstance">Library instance to use</param>
        /// <returns>If successfull, a list with the indices of the channels that are setup to a sample-group; elsewise null</returns>
        public static List<UInt16> getSampleGroupList(UInt32 group, UInt32 nInstance=0) {

            // retrieve group list
            cbSdkResult res;
            UInt32 proc = 1;
            UInt32 nChansInGroup = 0;
            UInt16[] pGroupList = new UInt16[cbNUM_ANALOG_CHANS + 0];
            try {
                res = cbSdkGetSampleGroupList(nInstance, proc, group, ref nChansInGroup, pGroupList);
            } catch (Exception e) {
                logger.Error("Unable to retrieve the sample-group (channel)list, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }
            if (res < 0) {
                logger.Error("Error while retrieving the sample-group (channel)list: " + getResultMessasge(res));
                return null;
            }

            // transfer the channel indices to a list
            List<UInt16> channelIndices = new List<UInt16>();
            for (int i = 0; i < nChansInGroup; i++)
                channelIndices.Add(pGroupList[i]);
            
            return channelIndices;
        }

        /// <summary>
        /// Retrieve the information of a sampling group
        /// </summary>
        /// <param name="group">The group index of which the informated need to be retrieved (0-based, but 0 is reserved, ranges from 1 to cbMAXGROUPS=8, or actually to 6 according to matlab doc)</param>
        /// <param name="nInstance">Library instance to use</param>
        /// <returns>If succesfull, the group information as a tuple with three values: group label, sampling-period and #channels on group. On failure, will return null</returns>
        public static Tuple<char[], UInt32, UInt32> getSampleGroupInfo(UInt32 group, UInt32 nInstance=0) {

            // retrieve group list
            cbSdkResult res;
            UInt32 proc = 1;
            char[] label = new char[cbLEN_STR_LABEL];
            UInt32 period = 0;
            UInt32 length = 0;
            try {
                res = cbSdkGetSampleGroupInfo(nInstance, proc, group, label, ref period, ref length);
            } catch (Exception e) {
                logger.Error("Unable to retrieve the sample-group information, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }

            if (res < 0) {
                logger.Error("Error while retrieving the sample-group information: " + getResultMessasge(res));
                return null;
            }

            return Tuple.Create(label, period, length);
        }

        
        /// <summary>
        /// Retrieve the filter description
        /// </summary>
        /// <param name="channel">Index of the filter to retrieve the description from (0-based, but 0 is reserved, ranges from 1 to cbMAXFILTS=32, or actually to 12 according to matlab doc)</param>
        /// <param name="nInstance">Library instance to use</param>
        public static cbFILTDESC? getFilterDesc(UInt32 filt, UInt32 nInstance=0) {

            // retrieve channel configuration
            cbSdkResult res;
            UInt32 proc = 1;
            cbFILTDESC filtDesc = new cbFILTDESC();
            try {
                res = cbSdkGetFilterDesc(nInstance, proc, filt, ref filtDesc);
            } catch (Exception e) {
                logger.Error("Unable to retrieve filter description, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return null;
            }
            if (res < 0) {
                logger.Error("Error while retrieving filter description: " + getResultMessasge(res));
                return null;
            }
            
            return filtDesc;
        }

        /// <summary>
        /// Configure trial settings, and start or stop buffering data
        /// </summary>
        /// <param name="bufCont">True to enable continuous data buffering up to cbSdk_CONTINUOUS_DATA_SAMPLES (=102400); False will disable, setting the buffer to 0</param>
        /// <param name="bufEvent">True to enable event buffering up to cbSdk_EVENT_DATA_SAMPLES (=2*8192); False will disable, setting the buffer to 0</param>
        /// <param name="active">True to flush data cache and (re-)start collecting data; False to stop collecting data</param>
        /// <param name="absoluteTiming">True makes timing absolute (setting 'active' to 1 will not reset time) and so time will not be relative to the start if the trial</param>
        /// <param name="dataAsDouble">True for the data in double-precision and the timestamps in a seconds, False for the data in 16-bit values and timestamps as 32-bit integers</param>
        /// <param name="nInstance">Library instance to use</param>
        /// <returns>True if trial configuration is set succesfully, false otherwise</returns>
        public static bool setTrialConfig(bool bufCont, bool bufEvent, bool active, bool absoluteTiming = false, bool dataAsDouble = false, UInt32 nInstance = 0) {
            return setTrialConfig(bufCont ? Cerelink.cbSdk_CONTINUOUS_DATA_SAMPLES : 0, bufEvent ? Cerelink.cbSdk_EVENT_DATA_SAMPLES : 0, 0, active, absoluteTiming, dataAsDouble, nInstance);
        }

        /// <summary>
        /// Configure trial settings, and start or stop buffering data
        /// </summary>
        /// <param name="contBufSamples">Set the continuous data buffering size in number of samples; 0 = no buffering, default = cbSdk_CONTINUOUS_DATA_SAMPLES = 102400</param>
        /// <param name="eventBufSamples">Set the event buffering size in number of samples; 0 = no buffering, default = cbSdk_EVENT_DATA_SAMPLES = 2*8192</param>
        /// <param name="commentBufSamples">Set the comment buffering size in number of samples; 0 = default = no buffering</param>
        /// <param name="active">True to flush data cache and (re-)start collecting data; False to stop collecting data</param>
        /// <param name="absoluteTiming">True makes timing absolute (setting 'active' to 1 will not reset time) and so time will not be relative to the start if the trial</param>
        /// <param name="dataAsDouble">True for the data in double-precision and the timestamps in a seconds, False for the data in 16-bit values and timestamps as 32-bit integers</param>
        /// <param name="nInstance">Library instance to use</param>
        /// <returns>True if trial configuration is set succesfully, false otherwise</returns>
        public static bool setTrialConfig(UInt32 contBufSamples, UInt32 eventBufSamples, UInt32 commentBufSamples, bool active, bool absoluteTiming = false, bool dataAsDouble=false, UInt32 nInstance=0) {
            
            // TODO: variables in some struct with helper functions around it
            UInt32 bActive = 0;
            UInt16 begchan = 0, endchan = 0;
            UInt32 begmask = 0, begval = 0, endmask = 0, endval = 0;
            bool bDouble = false, bAbsolute = false;
            UInt32 uWaveforms = 0, uConts = 0, uEvents = 0, uComments = 0, uTrackings = 0;

            // get trial configuration
            cbSdkResult res;
            try {
                res = cbSdkGetTrialConfig(nInstance, ref bActive,  ref begchan, ref begmask,  ref begval, ref endchan, ref endmask,  ref endval, 
                                                     ref bDouble, ref uWaveforms, ref uConts, ref uEvents, ref uComments, ref uTrackings, ref bAbsolute);
            } catch (Exception e) {
                logger.Error("Unable to get trial config before setting, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return false;
            }
            if (res < 0) {
                logger.Error("Error while getting trial config before setting: " + getResultMessasge(res));
                return false;
            }

            // set configuration variables
            bActive = (UInt32)(active ? 1 : 0);
            bDouble = dataAsDouble;
            uWaveforms = 0;             // does not work anyways (from cbpy.pyx)
            uConts = contBufSamples;
            uEvents = eventBufSamples;
            uComments = commentBufSamples;
            uTrackings = 0;
            bAbsolute = absoluteTiming;

            // fill mask related parameters with 0 defaults
            begchan = 0;
            begmask = 0;
            begval = 0;
            endchan = 0;
            endmask = 0;
            endval = 0;

            // set trial configuration
            try {
                res = cbSdkSetTrialConfig(nInstance, bActive, begchan, begmask, begval, endchan, endmask, endval, 
                                          bDouble, uWaveforms, uConts, uEvents, uComments, uTrackings, bAbsolute);
            } catch (Exception e) {
                logger.Error("Unable to set trial config, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return false;
            }
            if (res < 0) {
                logger.Error("Error while setting trial config: " + getResultMessasge(res));
                return false;
            }

            return true;

        }


        /// <summary>
        /// Retrieve the (trial) continuous data that is currently in the buffer
        /// </summary>
        /// <param name="active">True to clear all the buffered data, and reset the trial time to the current time; False (default) to leave buffer intact</param>
        public static bool fetchTrialContinuousData(bool active, out UInt32 time, out Int16[][] data, UInt32 nInstance = 0, int numAnalogChans = cbNUM_ANALOG_CHANS) {
            // TODO: use numAnalogChans to allow for deviations in the array of cbSdkTrialCont, dynamically adjusting to what the DLL is to return

            // default outs on failure
            time = 0;
            data = null;

            // retrieve the trial configuration
            // TODO: variables in some struct with helper functions around it
            UInt32 bActive = 0;
            UInt16 begchan = 0, endchan = 0;
            UInt32 begmask = 0, begval = 0, endmask = 0, endval = 0;
            bool bDouble = false, bAbsolute = false;
            UInt32 uWaveforms = 0, uConts = 0, uEvents = 0, uComments = 0, uTrackings = 0;
            cbSdkResult res;
            try {
                res = cbSdkGetTrialConfig(nInstance, ref bActive, ref begchan, ref begmask,  ref begval, ref endchan, ref endmask,  ref endval, 
                                                     ref bDouble, ref uWaveforms, ref uConts, ref uEvents, ref uComments, ref uTrackings, ref bAbsolute);
            } catch (Exception e) {
                logger.Error("Unable to get trial config before retrieving data, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                return false;
            }
            if (res < 0) {
                logger.Error("Error while getting trial config before retrieving data: " + getResultMessasge(res));
                return false;
            }

            // Initialize the structures (and fill with information about active channels and #samples in the buffer)
            // Note 1: called before data retrieval (cbSdkGetTrialData) to fill with latest number of samples.
            // Note 2: no allocation is performed here, buffer pointers must be set to appropriate allocated buffers after a call to this function
            bActive = (UInt32)(active ? 1 : 0);
            cbSdkTrialCont trialCont = new cbSdkTrialCont();
            res = cbSdkInitTrialContinuousData(nInstance, bActive, IntPtr.Zero, ref trialCont, IntPtr.Zero, IntPtr.Zero);
            if (res < 0) {
                logger.Error("Error while initializing trial continuous data structures: " + getResultMessasge(res));
                return false;
            }

            // no channels have been recorded, return success
            if (trialCont.count == 0) {
                //logger.Warn("No channels have been recorded by NSP");
                return true;
            }

            // TODO: check for any funky values, 0s in sampling or num_samples for each channel


            // initialize data matrix (jagged array, because theoretically each channel can have a different sampling-rate)
            // first dimension of the data matrix is sized to the number of channels
            data = new short[trialCont.count][];

            // create GCHandle objects (one for each channel data-pointer) to later pass a managed object to unmanaged code
            GCHandle[] gchandles = new GCHandle[trialCont.count];
            IntPtr[] gcptrs = new IntPtr[trialCont.count];

            // loop over the channels
            for (int i = 0; i < trialCont.count; i++) {

                // allocate/initialize an array to store this channel's samples in (second dimenion of the data matrix)
                data[i] = new short[trialCont.num_samples[i]];

                // creates the GCHandle, this ensures that the managed object (array) will not be freed by the garbage collector.
                // with the Pinned type to prevent the garbage collector from moving the object (undermines the efficiency of the garbage collector, free quickly after)
                gchandles[i] = GCHandle.Alloc(data[i], GCHandleType.Pinned);

                // retrieve the memory address (IntPrt) of the managed object (channel's data array)
                // and set it in the cbSdkTrialCont struct so that channel data will be written at that point in memory
                trialCont.samples[i] = gchandles[i].AddrOfPinnedObject();

            }

            try {
                // retrieve/copy the data onto the pointer locations (trialCont.samples) in the cbSdkTrialCont struct
                res = cbSdkGetTrialContinuousData(nInstance, bActive, IntPtr.Zero, ref trialCont, IntPtr.Zero, IntPtr.Zero);
                if (res < 0) {
                    logger.Error("Error while retrieving continuous data: " + getResultMessasge(res));
                    data = null;
                    return false;
                }

                // transfer the time and return success
                time = trialCont.time;
                return true;

            } catch (Exception e) {
                logger.Error("Unable to get trial config, an error occurred calling the CereLink SDK DLL");
                logger.Error("   - " + e.Message);
                data = null;
                return false;

            } finally {

                // free/unpin the pointers into the managed object
                foreach (GCHandle han in gchandles)
                    han.Free();

            }

            
        }



        ///////
        //       Supportive functions
        //////

        /// <summary>
        /// Retrieve the message that belong to a SDK result
        /// </summary>
        /// <param name="res">The SDK result</param>
        /// <returns>A string containing the message</returns>
        private static string getResultMessasge(cbSdkResult res) {
            switch (res) {
                case cbSdkResult.CBSDKRESULT_WARNCONVERT:
                    return "File conversion is needed";
                case cbSdkResult.CBSDKRESULT_WARNCLOSED:
                    return "Library is already closed";
                case cbSdkResult.CBSDKRESULT_WARNOPEN:
                    return "Library is already opened";
                case cbSdkResult.CBSDKRESULT_SUCCESS:
                    return "";
                case cbSdkResult.CBSDKRESULT_NOTIMPLEMENTED:
                    return "Not implemented";
                case cbSdkResult.CBSDKRESULT_UNKNOWN:
                    return "Unknown error";
                case cbSdkResult.CBSDKRESULT_INVALIDPARAM:
                    return "Invalid parameter";
                case cbSdkResult.CBSDKRESULT_CLOSED:
                    return "Interface is closed; cannot do this operation";
                case cbSdkResult.CBSDKRESULT_OPEN:
                    return "Interface is open; cannot do this operation";
                case cbSdkResult.CBSDKRESULT_NULLPTR:
                    return "Null pointer";
                case cbSdkResult.CBSDKRESULT_ERROPENCENTRAL:
                    return "Unable to open Central interface";
                case cbSdkResult.CBSDKRESULT_ERROPENUDP:
                    return "Unable to open UDP interface (might happen if default)";
                case cbSdkResult.CBSDKRESULT_ERROPENUDPPORT:
                    return "Unable to open UDP port";
                case cbSdkResult.CBSDKRESULT_ERRMEMORYTRIAL:
                    return "Unable to allocate RAM for trial cache data";
                case cbSdkResult.CBSDKRESULT_ERROPENUDPTHREAD:
                    return "Unable to open UDP timer thread";
                case cbSdkResult.CBSDKRESULT_ERROPENCENTRALTHREAD:
                    return "Unable to open Central communication thread";
                case cbSdkResult.CBSDKRESULT_INVALIDCHANNEL:
                    return "Invalid channel number";
                case cbSdkResult.CBSDKRESULT_INVALIDCOMMENT:
                    return "Comment too long or invalid";
                case cbSdkResult.CBSDKRESULT_INVALIDFILENAME:
                    return "Filename too long or invalid";
                case cbSdkResult.CBSDKRESULT_INVALIDCALLBACKTYPE:
                    return "Invalid callback type";
                case cbSdkResult.CBSDKRESULT_CALLBACKREGFAILED:
                    return "Callback register/unregister failed";
                case cbSdkResult.CBSDKRESULT_ERRCONFIG:
                    return "Trying to run an unconfigured method";
                case cbSdkResult.CBSDKRESULT_INVALIDTRACKABLE:
                    return "Invalid trackable id, or trackable not present";
                case cbSdkResult.CBSDKRESULT_INVALIDVIDEOSRC:
                    return "Invalid video source id, or video source not present";
                case cbSdkResult.CBSDKRESULT_ERROPENFILE:
                    return "Cannot open file";
                case cbSdkResult.CBSDKRESULT_ERRFORMATFILE:
                    return "Wrong file format";
                case cbSdkResult.CBSDKRESULT_OPTERRUDP:
                    return "Socket option error (possibly permission issue)";
                case cbSdkResult.CBSDKRESULT_MEMERRUDP:
                    return "Unable to assign UDP interface memory";
                case cbSdkResult.CBSDKRESULT_INVALIDINST:
                    return "Invalid range, instrument address or instrument mode";
                case cbSdkResult.CBSDKRESULT_ERRMEMORY:
                    return "Memory allocation error trying to establish master connection";
                case cbSdkResult.CBSDKRESULT_ERRINIT:
                    return "Initialization error";
                case cbSdkResult.CBSDKRESULT_TIMEOUT:
                    return "Connection timeout error";
                case cbSdkResult.CBSDKRESULT_BUSY:
                    return "Resource is busy";
                case cbSdkResult.CBSDKRESULT_ERROFFLINE:
                    return "Instrument is offline";
                default:
                    return res.ToString("D");
            }
        }


        /// <summary>
        /// Convert connection-type into a string representation
        /// </summary>
        /// <param name="type">The connection type</param>
        /// <returns>A string representation of the connection-type</returns>
        public static string getConnectionTypeString(cbSdkConnectionType type) {
            if (type == cbSdkConnectionType.CBSDKCONNECTION_DEFAULT)    return "Default";
            if (type == cbSdkConnectionType.CBSDKCONNECTION_CENTRAL)    return "Central";
            if (type == cbSdkConnectionType.CBSDKCONNECTION_UDP)        return "Udp";
            if (type == cbSdkConnectionType.CBSDKCONNECTION_CLOSED)     return "Closed";
            return "Unknown (" + type.ToString("D") + ")";
        }


        /// <summary>
        /// Convert instrument-type into a string representation
        /// </summary>
        /// <param name="type">The instrument type</param>
        /// <returns>A string representation of the instrument-type</returns>
        public static string getInstrumentTypeString(cbSdkInstrumentType type) {
            if (type == cbSdkInstrumentType.CBSDKINSTRUMENT_NSP)            return "NSP";
            if (type == cbSdkInstrumentType.CBSDKINSTRUMENT_NPLAY)          return "nPlay";
            if (type == cbSdkInstrumentType.CBSDKINSTRUMENT_LOCALNSP)       return "Local NSP";
            if (type == cbSdkInstrumentType.CBSDKINSTRUMENT_REMOTENPLAY)    return "Remote nPlay";
            return "Unknown (" + type.ToString("D") + ")";
        }


        /// <summary>
        /// Convert a filter type flag into a string representation
        /// </summary>
        /// <param name="type">The filter type flag</param>
        /// <returns>A string representation of the type</returns>
        public static string getFilterTypeString(UInt32 type) {
            List<string> parts = new List<string>();

            if ((type & cbFILTTYPE_PHYSICAL) != 0)      parts.Add("Physical");
            if ((type & cbFILTTYPE_DIGITAL) != 0)       parts.Add("Digital");
            if ((type & cbFILTTYPE_ADAPTIVE) != 0)      parts.Add("Adaptive");
            if ((type & cbFILTTYPE_NONLINEAR) != 0)     parts.Add("Non-Linear");
            if ((type & cbFILTTYPE_BUTTERWORTH) != 0)   parts.Add("Butterworth");
            if ((type & cbFILTTYPE_CHEBYCHEV) != 0)     parts.Add("Chebyshev");
            if ((type & cbFILTTYPE_BESSEL) != 0)        parts.Add("Bessel");
            if ((type & cbFILTTYPE_ELLIPTICAL) != 0)    parts.Add("Elliptical");
            
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Set a sorting algorithm for a channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to manipulate (obtained through getChannelConfig)</param>
        /// <param name="spkSrtOpt">Sorting method: cbAINPSPK_NOSORT, cbAINPSPK_HOOPSORT, cbAINPSPK_SPREADSORT, cbAINPSPK_CORRSORT, cbAINPSPK_PEAKMAJSORT, cbAINPSPK_PEAKFISHSORT, cbAINPSPK_PCAMANSORT</param>
        public static void setChannelSpikeSorting(ref cbPKT_CHANINFO chanInfo, UInt32 spkSrtOpt) {

            // delete all sorting options, and set the sorting method
            chanInfo.spkopts &= ~cbAINPSPK_ALLSORT;
            chanInfo.spkopts |= spkSrtOpt;

        }
  

        /// <summary>
        /// Check if a channel is set to process spikes
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if spike processing is enabled, false otherwise</returns>
        public static bool isSpikeProcessingEnabled(ref cbPKT_CHANINFO chanInfo) {
            return (chanInfo.ainpopts & cbAINPSPK_EXTRACT) == 0 ? false : true;
        }


        /// <summary>
        /// Enable or disable a spike extract for a channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to manipulate (obtained through getChannelConfig)</param>
        /// <param name="enabled">Whether to enabled (true) or disable (false) spike extraction</param>
        public static void setChannelSpikeExtraction(ref cbPKT_CHANINFO chanInfo, bool enabled) {
            if (enabled)
                chanInfo.spkopts |= cbAINPSPK_EXTRACT;
            else
                chanInfo.spkopts &= ~cbAINPSPK_EXTRACT;
        }


        /// <summary>
        /// Check if a channel is set to stream raw data
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if raw data streaming is enabled, false otherwise</returns>
        public static bool isChannelRawDataStreamingEnabled(ref cbPKT_CHANINFO chanInfo) {
            return (chanInfo.ainpopts & cbAINP_RAWSTREAM_ENABLED) == 0 ? false : true;
        }


        /// <summary>
        /// Enable or disable raw data streaming for a channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to manipulate (obtained through getChannelConfig)</param>
        /// <param name="enabled">Whether to enabled (true) or disable (false) raw data streaming</param>
        public static void setChannelRawDataStreaming(ref cbPKT_CHANINFO chanInfo, bool enabled) {
            if (enabled)
                chanInfo.ainpopts |= cbAINP_RAWSTREAM_ENABLED;
            else
                chanInfo.ainpopts &= ~cbAINP_RAWSTREAM_ENABLED;
        }


        /// <summary>
        /// Check if a channel is an analog-in channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if analog-in channel, false otherwise</returns>
        public static bool isChannelAnalogIn(ref cbPKT_CHANINFO chanInfo) {
            return (chanInfo.chancaps & cbCHAN_AINP) == cbCHAN_AINP;
        }
        

        /// <summary>
        /// Check if a channel is a front-end analog-in channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if front-end analog-in channel, false otherwise</returns>
        public static bool isChannelFEAnalogIn(ref cbPKT_CHANINFO chanInfo) {
            return (cbCHAN_AINP | cbCHAN_ISOLATED) == (chanInfo.chancaps & (cbCHAN_AINP | cbCHAN_ISOLATED));
        }


        /// <summary>
        /// Check if a channel is a digital-in channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if digital-in channel, false otherwise</returns>
        public static bool isChannelDigitalIn(ref cbPKT_CHANINFO chanInfo) {
            return (chanInfo.chancaps & cbDINP_SERIALMASK) != cbDINP_SERIALMASK;
        }


        /// <summary>
        /// Check if a channel is a serial channel
        /// </summary>
        /// <param name="chanInfo">The channel info structure to check (obtained through getChannelConfig)</param>
        /// <returns>True if serial channel, false otherwise</returns>
        public static bool isChannelSerial(ref cbPKT_CHANINFO chanInfo) {
            return (chanInfo.chancaps & cbDINP_SERIALMASK) == cbDINP_SERIALMASK;
        }

    }

}
