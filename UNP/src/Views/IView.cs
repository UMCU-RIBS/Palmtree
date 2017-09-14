using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Views {

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

        bool isLeftMouseDown();
        bool isRightMouseDown();

        bool isKeyDown(System.Windows.Forms.Keys key);
        bool isKeyDownEscape();
        bool isKeyUp(System.Windows.Forms.Keys key);

    }

}
