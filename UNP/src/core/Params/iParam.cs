using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public interface iParam {

        string Name         { get; }
        string Group        { get; }
        string Desc         { get; }
        string MinValue     { get; }
        string MaxValue     { get; }
        string StdValue     { get; }
        string[] Options    { get; }

        string getValue();
        T getValue<T>();
        int getValueInSamples();
        bool setStdValue(string stdValue);
        bool tryValue(string value);
        bool setValue(string value);

        iParam clone();

    }

}
