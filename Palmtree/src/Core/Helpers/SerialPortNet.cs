/**
 * The SerialPortNet class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Specialized;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace Palmtree.Core.Helpers {

    /// <summary>
    /// The <c>SerialPortNet</c> class.
    /// 
    /// ...
    /// </summary>
    public class SerialPortNet {

        [DllImport("kernel32.dll")]
        public static extern bool SetCommMask(IntPtr hFile, uint dwEvtMask);

        [DllImport("kernel32.dll")]
        public static extern bool GetCommState(IntPtr hFile, ref Palmtree.Core.Helpers.SerialPortNet.DCB lpDCB);

        [DllImport("kernel32.dll")]
        public static extern bool SetCommState(IntPtr hFile, ref Palmtree.Core.Helpers.SerialPortNet.DCB lpDCB);

        [DllImport("kernel32.dll")]
        public static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);

        [DllImport("kernel32.dll")]
        public static extern bool ClearCommError([In] IntPtr hFile, // not int, convert int to IntPtr: new IntPtr(12)
          [Out, Optional] out uint lpErrors,
          [Out, Optional] out Palmtree.Core.Helpers.SerialPortNet.COMSTAT lpStat
        );

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

        public const uint EV_RXCHAR = 0x0001;

        public enum ExtendedFunctions : uint {
            CLRBREAK = 9, //Restores character transmission and places the transmission line in a nonbreak state.
            CLRDTR = 6, //Clears the DTR (data-terminal-ready) signal.
            CLRRTS = 4, //Clears the RTS (request-to-send) signal.
            SETBREAK = 8, //Suspends character transmission and places the transmission line in a break state until the ClearCommBreak function is called
            SETDTR = 5, //Sends the DTR (data-terminal-ready) signal.
            SETRTS = 3, //Sends the RTS (request-to-send) signal.
            SETXOFF = 1, //Causes transmission to act as if an XOFF character has been received.
            SETXON = 2 //Causes transmission to act as if an XON character has been received.
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMSTAT {
            public const uint fCtsHold = 0x1;
            public const uint fDsrHold = 0x2;
            public const uint fRlsdHold = 0x4;
            public const uint fXoffHold = 0x8;
            public const uint fXoffSent = 0x10;
            public const uint fEof = 0x20;
            public const uint fTxim = 0x40;
            public UInt32 Flags;
            public UInt32 cbInQue;
            public UInt32 cbOutQue;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DCB {
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

    }
}
