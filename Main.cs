using HarmonyLib;
using System;
using UnityModManagerNet;

namespace DvMod.WagonDestination
{
    [EnableReloading]
    public static class Main
    {
        public static UnityModManager.ModEntry? mod;
        public static Settings settings = new Settings();
        public static bool enabled;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;

            try
            {
                var loaded = Settings.Load<Settings>(modEntry);
                if (loaded.version == modEntry.Info.Version)
                    settings = loaded;
            }
            catch
            {
            }

            mod.OnGUI = OnGUI;
            mod.OnSaveGUI = OnSaveGUI;
            mod.OnToggle = OnToggle;
            mod.OnUnload = OnUnload;

            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            return OnToggle(modEntry, false);
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw();
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Harmony harmony = new Harmony(modEntry.Info.Id);

            if (value)
            {
                harmony.PatchAll();
                WorldStreamingInit.LoadingFinished += Start;
                UnloadWatcher.UnloadRequested += Stop;
                if (WorldStreamingInit.Instance && WorldStreamingInit.IsLoaded)
                {
                    Start();
                }
            }
            else
            {
                Stop();
                UnloadWatcher.UnloadRequested -= Stop;
                WorldStreamingInit.LoadingFinished -= Start;
                harmony.UnpatchAll(modEntry.Info.Id);
                CargoNameClipper.RestoreAll();
            }
            enabled = value;
            return true;
        }

        private static void Start()
        {
        }

        private static void Stop()
        {
        }

        public static void DebugLog(Func<string> message)
        {
            if (settings.enableLogging)
                mod?.Logger.Log(message());
        }
    }
}
