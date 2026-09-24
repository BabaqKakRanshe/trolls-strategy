using UnityEngine;

namespace TrollStrategy.Presentation
{
    // Shared scene and HUD palette for the tabletop diorama.
    public static class ColonyPalette
    {
        public static readonly Color Night = new Color32(29, 38, 34, 255);       // #1D2622
        public static readonly Color Panel = new Color32(42, 54, 45, 255);       // #2A362D
        public static readonly Color Raised = new Color32(60, 75, 59, 255);      // #3C4B3B
        public static readonly Color Slate = new Color32(70, 83, 68, 255);       // #465344
        public static readonly Color Stone = new Color32(101, 117, 91, 255);     // #65755B
        public static readonly Color PaleStone = new Color32(148, 157, 128, 255);// #949D80
        public static readonly Color Text = new Color32(241, 232, 213, 255);     // #F1E8D5
        public static readonly Color MutedText = new Color32(198, 194, 170, 255);// #C6C2AA
        public static readonly Color Forest = new Color32(37, 86, 46, 255);      // #25562E
        public static readonly Color Pine = new Color32(70, 130, 50, 255);       // #468232
        public static readonly Color Grass = new Color32(117, 167, 67, 255);     // #75A743
        public static readonly Color GrassLight = new Color32(168, 202, 88, 255);// #A8CA58
        public static readonly Color WoodDark = new Color32(122, 72, 65, 255);   // #7A4841
        public static readonly Color Wood = new Color32(173, 119, 87, 255);      // #AD7757
        public static readonly Color Path = new Color32(192, 148, 115, 255);     // #C09473
        public static readonly Color Sand = new Color32(215, 181, 148, 255);     // #D7B594
        public static readonly Color Cream = new Color32(231, 213, 179, 255);    // #E7D5B3
        public static readonly Color Gold = new Color32(232, 193, 112, 255);     // #E8C170
        public static readonly Color Clay = new Color32(207, 87, 60, 255);       // #CF573C
        public static readonly Color Water = new Color32(115, 190, 211, 255);    // #73BED3
        public static readonly Color DeepStone = new Color32(21, 29, 40, 255);   // #151D28

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
