using NLog;
using Palmtree.Core;
using Palmtree.Core.DataIO;
using Palmtree.Core.Params;
using Palmtree.Filters;
using System;
using System.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Text.Json;

namespace Palmtree.Filters
{

    public class WSIO : WebSocketBehavior
    {
        private static NLog.Logger logger = LogManager.GetLogger("Data");

        public class DataStruct
        {
            public string eventState { get; set; }
            public string eventCode { get; set; }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            DataStruct dataStruct = JsonSerializer.Deserialize<DataStruct>(e.Data);
            Data.logEvent(1, dataStruct.eventState, dataStruct.eventCode);
        }
    }
}