using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if HEXA_STEAMVR

using Valve.VR;

#endif

public class HexaSteamVRActionEnabler : MonoBehaviour
{
#if HEXA_STEAMVR
    public SteamVR_ActionSet actionSet = SteamVR_Input.GetActionSet("HVR");

    public SteamVR_Input_Sources forSources = SteamVR_Input_Sources.Any;
#endif

    public bool disableAllOtherActionSets = false;

    public bool activateOnStart = true;
    public bool deactivateOnDestroy = true;


    private void Start()
    {
#if HEXA_STEAMVR
        if (actionSet != null && activateOnStart)
        {
            //Debug.Log(string.Format("[SteamVR] Activating {0} action set.", actionSet.fullPath));
            actionSet.Activate(forSources, 0, disableAllOtherActionSets);
        }
#endif
    }

    private void OnDestroy()
    {
#if HEXA_STEAMVR
        if (actionSet != null && deactivateOnDestroy)
        {
            //Debug.Log(string.Format("[SteamVR] Deactivating {0} action set.", actionSet.fullPath));
            actionSet.Deactivate(forSources);
        }
#endif
    }

}
