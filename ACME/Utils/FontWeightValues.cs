using Windows.UI.Text;

namespace ACME.Utils
{
    /// <summary>
    /// Defines font weight constants to replace FontWeights static class
    /// </summary>
    public static class FontWeightValues
    {
        public static FontWeight Thin = new FontWeight { Weight = 100 };
        public static FontWeight ExtraLight = new FontWeight { Weight = 200 };
        public static FontWeight Light = new FontWeight { Weight = 300 };
        public static FontWeight SemiLight = new FontWeight { Weight = 350 };
        public static FontWeight Normal = new FontWeight { Weight = 400 };
        public static FontWeight Medium = new FontWeight { Weight = 500 };
        public static FontWeight SemiBold = new FontWeight { Weight = 600 };
        public static FontWeight Bold = new FontWeight { Weight = 700 };
        public static FontWeight ExtraBold = new FontWeight { Weight = 800 };
        public static FontWeight Black = new FontWeight { Weight = 900 };
        public static FontWeight ExtraBlack = new FontWeight { Weight = 950 };
    }
} 