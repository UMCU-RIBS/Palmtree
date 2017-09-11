using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Helpers {

    public class BoolRingBuffer {

        private bool[] mData = null;
        private uint mCursor = 0;
        private bool mWrapped = false;

        public BoolRingBuffer(uint inSize) {
            mData = new bool[inSize];     // values are automatically initiated to 0
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
            return mWrapped ? (uint)mData.Count() : mCursor;
        }

        public bool[] Data() {
            return mData;
        }

        public void Put(bool inData) {

            if (mData.Count() > 0)    mData[mCursor] = inData;
            if (++mCursor == mData.Count()) {
                mWrapped = true;
                mCursor = 0;
            }
        }

        public void Clear() {
            mCursor = 0;
            mWrapped = false;
        }

    }

}
