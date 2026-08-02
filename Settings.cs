using UnityEngine;
using UnityModManagerNet;

namespace DvMod.WagonDestination
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool enableLogging = false;

        public readonly string? version = Main.mod?.Info.Version;

        public void Draw()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false));
            enableLogging = GUILayout.Toggle(enableLogging,
                " Write debug detail to the mod log (Player.log)");
            GUILayout.EndVertical();
        }

        public override void Save(UnityModManager.ModEntry entry)
        {
            Save<Settings>(this, entry);
        }
    }
}
