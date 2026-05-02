using System.Drawing;

namespace SilkroadAIBot.UI
{
    public static class ThemeColors
    {
        // Base
        public static readonly Color Background           = Color.FromArgb(5,  5,  8);
        public static readonly Color PanelSidebarBackground = Color.FromArgb(8,  8, 14);
        public static readonly Color PanelContentBackground = Color.FromArgb(14, 14, 24);
        public static readonly Color SidebarBg           = Color.FromArgb(10, 10, 16);
        public static readonly Color HeaderBg             = Color.FromArgb(7,  7, 12);
        public static readonly Color ActiveNavBg          = Color.FromArgb(22, 20, 32);

        // Accents
        public static readonly Color PrimaryAccent  = Color.FromArgb(201, 168,  76); // Gold
        public static readonly Color GoldDark       = Color.FromArgb(138, 106,  31);
        public static readonly Color GoldLight      = Color.FromArgb(240, 208, 128);
        public static readonly Color SecondaryAccent= Color.FromArgb(139,  26,  26); // Deep Red
        public static readonly Color AccentRed      = Color.FromArgb(192,  57,  43);
        public static readonly Color ConnectedStatus= Color.FromArgb( 46, 204, 113);
        public static readonly Color Error          = Color.FromArgb(231,  76,  60);
        public static readonly Color Warning        = Color.FromArgb(241, 196,  15);

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(212, 197, 160);
        public static readonly Color TextMuted   = Color.FromArgb(122, 110,  90);

        // Borders
        public static readonly Color BorderColor     = Color.FromArgb( 50, 201, 168,  76);
        public static readonly Color PanelBorder     = Color.FromArgb( 40, 201, 168,  76);
        public static readonly Color GroupTitleText  = Color.FromArgb(201, 168,  76);

        // Bars
        public static readonly Color HpBar = Color.FromArgb(192, 57, 43);
        public static readonly Color MpBar = Color.FromArgb( 41,128,185);

        // Misc
        public static readonly Color CheckboxBorder  = Color.FromArgb(201, 168,  76);
        public static readonly Color CheckboxFill    = Color.FromArgb( 20,  20,  30);
        public static readonly Color BadgeBackground = Color.FromArgb( 20,  20,  30);
        public static readonly Color BadgeText       = Color.FromArgb(201, 168,  76);
    }
}
