using BepInEx;

namespace Daywheel
{
    /// <summary>
    /// Lets BepInEx start Daywheel, for anyone who has BepInEx or uses a mod
    /// manager. Without BepInEx, Keel starts it instead and this class is
    /// never loaded.
    /// </summary>
    [BepInPlugin(Main.Guid, Main.Title, Main.Ver)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Keel.Bep.Start(new Main(), Config, Logger);
        }
    }
}
