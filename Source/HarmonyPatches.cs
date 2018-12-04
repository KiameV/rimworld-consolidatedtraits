using ConfigurableMaps.Settings;
using Harmony;
using System;
using System.Reflection;
using Verse;

namespace ConsolidatedTraits
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
		public static bool UsingInGameDefEditor { get; private set; }
		static HarmonyPatches()
		{
			foreach (var m in ModsConfig.ActiveModsInLoadOrder)
			{
				if ("[KV] In-Game Definition Editor - 1.0".Equals(m.Name))
				{
					Log.Message("ConsolidatedTraits will not load settings. In-Game Definition Editor should be used instead.");
					UsingInGameDefEditor = true;
					break;
				}
			}

			if (!UsingInGameDefEditor)
			{
				var harmony = HarmonyInstance.Create("com.consolidatedtraits.rimworld.mod");
				harmony.PatchAll(Assembly.GetExecutingAssembly());
				Log.Message(
					"ConsolidatedTraits Harmony Patches:" + Environment.NewLine +
					"  Prefix:" + Environment.NewLine +
					"    GameComponentUtility.StartedNewGame" + Environment.NewLine +
					"    GameComponentUtility.LoadedGame");
			}
		}
    }

    [HarmonyPatch(typeof(GameComponentUtility), "StartedNewGame")]
    static class Patch_GameComponentUtility_StartedNewGame
    {
        static void Postfix()
        {
            Controller.Settings.Init();
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), "LoadedGame")]
    static class Patch_GameComponentUtility_LoadedGame
    {
        static void Postfix()
        {
            Controller.Settings.Init();
        }
    }
}