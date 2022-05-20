using StardewModdingAPI;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoonMisadventures_BiggerBackpackPatch
{
    internal class ObjectPatches
    {
        private static IMonitor Monitor;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public static void InventoryPage_Constructor__Postfix(InventoryPage __instance)
        {
            var equipment = __instance.equipmentIcons.Find(item => item.myID == 123450101);
            if (equipment == null)
            {
                Monitor.Log("Unable to find the necklace slot for Moon Misadentures", LogLevel.Warn);
            }
            equipment.bounds.Y =
                (__instance.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 4 + 256 - 12) // original
                + (64); // Extra
        }
    }
}
