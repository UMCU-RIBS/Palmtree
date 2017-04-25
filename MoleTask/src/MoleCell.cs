using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoleTask {

    class MoleCell {
        
		public enum CellType : int {
			Empty,
			Hole,
			Mole,
			Exit
		};

        public int mX;
        public int mY;
        public int mWidth;
        public int mHeight;
        public CellType mType;

        public MoleCell(int x, int y, int width, int height, CellType type) {
	        mX = x;
	        mY = y;
	        mWidth = width;
	        mHeight = height;
            mType = type;
        }

    }

}
