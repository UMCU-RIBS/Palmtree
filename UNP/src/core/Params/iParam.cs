using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public interface iParam {

        String Name         { get; }
        String Group        { get; }
        String Desc         { get; }
        String MinValue     { get; }
        String MaxValue     { get; }
        String StdValue     { get; }
        String[] Options    { get; }

        String getValue();
        T getValue<T>();
        int getValueInSamples();
        bool setStdValue(String stdValue);
        bool tryValue(String value);
        bool setValue(String value);

    }

}
