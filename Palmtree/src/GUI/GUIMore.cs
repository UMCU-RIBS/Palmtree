/**
 * The GUIMore class
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
using NLog;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Palmtree.Core.Helpers;
using Palmtree.Core.Params;

namespace Palmtree.GUI {

    /// <summary>
    /// The <c>GUIMore</c> class.
    /// 
    /// ...
    /// </summary>
    public partial class GUIMore : Form {

        private static Logger logger = LogManager.GetLogger("GUIMore");

        public GUIMore() {
            InitializeComponent();
        }

        private void btnPrintParamInfo_Click(object sender, EventArgs e) {
            string paramInfo = "";

            // retrieve the parameter sets
            Dictionary<string, Parameters> paramSets = ParameterManager.getParameterSets();

            // open
            paramInfo += Environment.NewLine;
            paramInfo += "-----------------------------------------------" + Environment.NewLine;

            // loop through each paramset
            foreach (KeyValuePair<string, Parameters> entry in paramSets) {
                
                // module name
                paramInfo += Environment.NewLine + "-------------  Module " + entry.Value.ParamSetName + " (" + entry.Value.ParamSetType + ")  -------------" + Environment.NewLine;

                // loop through the parameters
                List<iParam> parameters = entry.Value.getParameters();
                for (int i = 0; i < parameters.Count; i++) {
                    iParam param = parameters[i];

                    paramInfo += "+++++ " + Environment.NewLine;

                    paramInfo += "Name: " + param.Name + Environment.NewLine;
                    paramInfo += "Desc: " + param.Desc + Environment.NewLine;
                    paramInfo += "MinValue: " + param.MinValue + Environment.NewLine;
                    paramInfo += "MaxValue: " + param.MaxValue + Environment.NewLine;
                    paramInfo += "StdValue: " + param.StdValue + Environment.NewLine;
                    paramInfo += "Options: " + string.Join(", ", param.Options) + Environment.NewLine;

                    paramInfo += Environment.NewLine;

                }

            }
                
            // close
            paramInfo += "------------------------" + Environment.NewLine;


            // write to console
            logger.Info(paramInfo);

        }

        private void btnGammaStandard_Click(object sender, EventArgs e) {
            MonitorHelper.setGamma(120);
        }
    }
}
