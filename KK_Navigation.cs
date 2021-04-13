using BepInEx;
using BepInEx.Logging;
using JetBrains.Annotations;
using KKAPI.Studio;

namespace KK_Navigation
{
    [BepInPlugin(GUID, "KK_Navigation", Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    public partial class KK_Navigation : BaseUnityPlugin
    {
        public const string GUID = "kk_navigation";
        public const string Version = "0.1.0";

        internal new static ManualLogSource Logger { get; private set; }

        internal static KK_Navigation Instance;

        [UsedImplicitly]
        private void Awake()
        {
            Logger = base.Logger;

            Instance = this;

            if (StudioAPI.InsideStudio)
			{
				return;
			}

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Hooks));
        }
    }
}
