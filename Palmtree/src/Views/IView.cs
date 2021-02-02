/**
 * The IView interface
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

namespace Palmtree.Views {

    /// <summary>
    /// The <c>IView</c> interface.
    /// 
    /// abc.
    /// </summary>
    public interface IView {

        // window/control/view functions
        void start();
        void stop();

        int getWindowX();
        int getWindowY();
        void setWindowLocation(int x, int y);

        int getWindowWidth();
        int getWindowHeight();
        void setWindowSize(int width, int height);

        int getContentWidth();
        int getContentHeight();
        void setContentSize(int width, int height);

        bool hasBorder();
        void setBorder(bool border);

        void setBackgroundColor(float red, float green, float blue);

        bool isStarted();

        bool isTapped();
        bool isLeftMouseDown();
        bool isRightMouseDown();

        bool isKeyDown(System.Windows.Forms.Keys key);
        bool isKeyDownEscape();
        bool isKeyUp(System.Windows.Forms.Keys key);

    }

}
