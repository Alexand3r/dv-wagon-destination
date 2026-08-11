using UnityEngine;
using UnityModManagerNet;

namespace DvMod.WagonDestination
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool destinationOnJobRow = false;
        public bool ellipsizeCargoName = true;
        public bool massInTonnes = false;
        public bool enableLogging = true;

        public readonly string? version = Main.mod?.Info.Version;

        public void Draw()
        {
            bool prevJobRow = destinationOnJobRow;
            bool prevEllipsize = ellipsizeCargoName;
            bool prevTonnes = massInTonnes;
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false));
            GUILayout.Label("Destination placement:");
            destinationOnJobRow = GUILayout.SelectionGrid(
                destinationOnJobRow ? 1 : 0,
                new[]
                {
                    " Above the job id, on the cargo name row",
                    " Next to the job id, cargo mass shortened to tonnes",
                },
                1, GUI.skin.toggle) == 1;
            GUILayout.Space(10);
            ellipsizeCargoName = GUILayout.Toggle(ellipsizeCargoName,
                " Shorten long cargo names with … where the destination would overlap");
            massInTonnes = GUILayout.Toggle(massInTonnes,
                " Show masses in tonnes (36000kg -> 36t)");
            enableLogging = GUILayout.Toggle(enableLogging,
                " Write debug detail to the mod log (Player.log)");
            GUILayout.EndVertical();
            if (prevJobRow != destinationOnJobRow || prevEllipsize != ellipsizeCargoName
                || prevTonnes != massInTonnes)
                PlateDestination.RefreshAll();
        }

        public override void Save(UnityModManager.ModEntry entry)
        {
            Save<Settings>(this, entry);
        }
    }
}
