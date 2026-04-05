using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkestDepths.Helpers
{
    internal class MessageType
    {
        public const string InitializeMPGame = "InitializeMPGame";
        public const string RequestInitialLabryinthLevel = "RequestInitialLabryinthLevel";
        public const string RequestLabyrinthLevel = "RequestLabryinthLevel";
        public const string LabyrinthLevelCreated = "LabryinthLevelCreated";
    }
}
