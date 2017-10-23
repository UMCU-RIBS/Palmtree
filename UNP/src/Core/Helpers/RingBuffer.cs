using System;

namespace UNP.Core.Helpers {

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
        }

    }

}
