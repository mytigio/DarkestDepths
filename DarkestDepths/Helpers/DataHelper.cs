using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkestDepths.Helpers
{
    internal static class DataHelper
    {
        public const string LOCATION_CONTEXT = "labyrinth_location_context";

        public static IModHelper MyHelper { get; set; }
        public static IMonitor MyMonitor { get; set; }

    }
}
