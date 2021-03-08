/**
 * The RingBuffer class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * Adapted from:        BCI2000 (Schalk Lab, www.schalklab.org)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;

namespace Palmtree.Core.Helpers {

    /// <summary>
    /// The <c>RingBuffer</c> class.
    /// 
    /// ...
    /// </summary>
    public class RingBuffer {

        private double[] mData = null;
        private uint mCursor = 0;
        private bool mWrapped = false;

        public RingBuffer(uint inSize) {
            mData = new double[inSize];     // values are automatically initiated to 0
            mCursor = 0;
            mWrapped = false;
        }

	    public bool IsFull() {
            return mWrapped; 
        }

        public uint CursorPos() {
            return mCursor;
        }

        public uint Fill() {
            return mWrapped ? (uint)mData.Length : mCursor;
        }

        public double[] Data() {
            return mData;
        }

        public double[] DataSequential() {
            double[] retArr;
            if (mWrapped) {
                retArr = new double[mData.Length];
                if (mCursor == 0)
                    Array.Copy(mData, 0, retArr, 0, mData.Length);
                else {
                    Array.Copy(mData, mCursor, retArr, 0, mData.Length - mCursor);
                    Array.Copy(mData, 0, retArr, mData.Length - mCursor, mCursor);
                }
            } else {
                retArr = new double[mCursor];
                Array.Copy(mData, 0, retArr, 0, mCursor);
            }
            return retArr;
        }

        public void Put(double inData) {

            if (mData.Length > 0)    mData[mCursor] = inData;
            if (++mCursor == mData.Length) {
                mWrapped = true;
                mCursor = 0;
            }
        }

        public int Size() {
            return mData.Length;
        }

        public void Clear() {
            mCursor = 0;
            mWrapped = false;
            Array.Clear(mData, 0, mData.Length);
        }

    }

}
