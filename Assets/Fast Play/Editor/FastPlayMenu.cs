using UnityEditor;
using UnityEngine;

namespace DeepWater.FastPlay.Editor
{
    /// <summary>
    /// One menu item to turn the domain reload on play back on and off. Deep Water enters play without one
    /// (BUILD-SPEED-LEDGER, round 6). The first-party runtime code is audited for it (StaticStateTests); vendor code is not.
    /// If a vendor-heavy legacy scene misbehaves on its second play, turn the reload back on here.
    /// </summary>
    public static class FastPlayMenu
    {
        const string Item = "Deep Water/Fast Play/Reload Domain On Play";

        static bool Reloads => !EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;

        [MenuItem(Item)]
        static void Toggle()
        {
            bool reloadNext = !Reloads;
            EditorSettings.enterPlayModeOptionsEnabled = !reloadNext;
            EditorSettings.enterPlayModeOptions = reloadNext ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload;
            Debug.Log(reloadNext
                ? "[Fast Play] the domain reloads on play again (slower, and safe for vendor code that keeps static state)."
                : "[Fast Play] play mode starts without a domain reload (the scene still reloads).");
        }

        [MenuItem(Item, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(Item, Reloads);
            return !EditorApplication.isPlaying;
        }
    }
}
