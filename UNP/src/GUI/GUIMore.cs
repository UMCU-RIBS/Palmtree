using NLog;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using UNP.Core.Params;

namespace UNP.GUI {
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


    }
}
