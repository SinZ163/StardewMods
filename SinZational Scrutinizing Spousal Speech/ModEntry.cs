using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley;
using System.Reflection.Emit;

namespace SinZ.ScrutinizingSpousalSpeech;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var harmony = new Harmony(this.ModManifest.UniqueID);
        HarmonyPatches.Setup(Monitor, harmony);

        // helper.Events.Content.AssetRequested += Content_AssetRequested;
    }

    /*private void Content_AssetRequested(object? sender, StardewModdingAPI.Events.AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/MarriageDialogueAbigail"))
        {
            e.Edit(data =>
            {
                var dict = data.AsDictionary<string, string>();
                dict.Data.Clear();
            }, StardewModdingAPI.Events.AssetEditPriority.Late);
        }
        if (e.NameWithoutLocale.IsEquivalentTo("Characters/Dialogue/MarriageDialogue"))
        {
            e.Edit(data =>
            {
                var dict = data.AsDictionary<string, string>();
                dict.Data.Clear();
            }, StardewModdingAPI.Events.AssetEditPriority.Late);
        }
    }*/
}

public static class HarmonyPatches
{
    private static IMonitor monitor;
    public static void Setup(IMonitor monitor, Harmony harmony)
    {
        HarmonyPatches.monitor = monitor;
        var tryToGetMarriageSpecificDialogue = typeof(NPC).GetMethod(nameof(NPC.tryToGetMarriageSpecificDialogue));
        if (tryToGetMarriageSpecificDialogue != null)
        {
            harmony.Patch(tryToGetMarriageSpecificDialogue, transpiler: new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(tryToGetMarriageSpecificDialogue__transpiler))));
        }
    }
    public static IEnumerable<CodeInstruction> tryToGetMarriageSpecificDialogue__transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
    {
        var output = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldloc_2)
            {
                var loadThis = new CodeInstruction(OpCodes.Ldarg_0)
                {
                    labels = instruction.labels
                };
                instruction.labels = new List<Label>();
                output.Add(loadThis);
                output.Add(new CodeInstruction(OpCodes.Ldarg_1));
                output.Add(new CodeInstruction(OpCodes.Call, typeof(HarmonyPatches).GetMethod(nameof(tryToGetMarriageSpecificDialogue__failRoomate))));
            }
            if (instruction.opcode == OpCodes.Stloc_1 && output[output.Count - 1].opcode == OpCodes.Ldstr)
            {
                var ldStr = output[output.Count - 1];

                var loadThis = new CodeInstruction(OpCodes.Ldarg_0)
                {
                    labels = ldStr.labels
                };
                ldStr.labels = new List<Label>();


                output.Insert(output.Count - 1, loadThis);
                output.Insert(output.Count - 1, new CodeInstruction(OpCodes.Ldarg_1));
                output.Insert(output.Count - 1, new CodeInstruction(OpCodes.Call, typeof(HarmonyPatches).GetMethod(nameof(tryToGetMarriageSpecificDialogue__failNPC))));
            }
            if (instruction.opcode == OpCodes.Ldnull && instruction.labels.Count > 0)
            {
                var Ldarg_1 = new CodeInstruction(OpCodes.Ldarg_1)
                {
                    labels = instruction.labels
                };
                instruction.labels = new List<Label>();
                output.Add(Ldarg_1);
                output.Add(new CodeInstruction(OpCodes.Call, typeof(HarmonyPatches).GetMethod(nameof(tryToGetMarriageSpecificDialogue__fail))));
            }
            output.Add(instruction);
        }
        return output;
    }
    public static void tryToGetMarriageSpecificDialogue__failRoomate(NPC _this, string dialogueKey)
    {
        if (_this.isRoommate())
        {
            HarmonyPatches.monitor.Log($"Failed to load {dialogueKey} for {_this.Name}Roomate", LogLevel.Debug);
        }
    }
    public static void tryToGetMarriageSpecificDialogue__failNPC(NPC _this, string dialogueKey)
    {
        HarmonyPatches.monitor.Log($"Failed to load {dialogueKey} for {_this.Name}", LogLevel.Debug);
    }
    public static void tryToGetMarriageSpecificDialogue__fail(string dialogueKey)
    {
        HarmonyPatches.monitor.Log($"Failed to load {dialogueKey} at all", LogLevel.Debug);
    }
}