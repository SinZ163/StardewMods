using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoonMisadventures_BiggerBackpackPatch
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(this.ModManifest.UniqueID);
            ObjectPatches.Initialize(Monitor);
            harmony.Patch(
                original: AccessTools.Constructor(typeof(InventoryPage), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.InventoryPage_Constructor__Postfix))
            );
            this.Monitor.Log("This mod overrides a patch by Moon Misadventures. If you notice issues with Moon Misadventures, make sure it happens without this mod before reporting it to the Automate page.", LogLevel.Trace);
        }
    }
}
