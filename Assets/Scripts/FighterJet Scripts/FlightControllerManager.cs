using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ModuleStatus
{
    public MonoBehaviour module;
    public GameObject statusIndicator; // GameObject for the status indicator
    public ModuleState currentState;
}

public enum ModuleState { Active, Offline, Warning, Informational, Degraded, ManualOverride, CriticalAlert, Processing, Idle, Disabled }

public class FlightControllerManager : MonoBehaviour
{
    public ModuleStatus[] modules; // Array of module statuses
    public WaypointManager waypointManager;
    public RouteOptimizer routeOptimizer;

    void Update()
    {
        for (int i = 0; i < modules.Length; i++)
        {
            UpdateModuleStatus(ref modules[i]);
        }
    }

    private void UpdateModuleStatus(ref ModuleStatus moduleStatus)
    {
        switch (moduleStatus.currentState)
        {
            case ModuleState.Active:
                SetStatusColor(moduleStatus, Color.green);
                break;
            case ModuleState.Offline:
                SetStatusColor(moduleStatus, Color.red);
                break;
            case ModuleState.Warning:
                SetStatusColor(moduleStatus, Color.yellow);
                break;
            case ModuleState.Informational:
                SetStatusColor(moduleStatus, Color.blue);
                break;
            case ModuleState.Degraded:
                SetStatusColor(moduleStatus, Color.yellow);
                break;
            case ModuleState.ManualOverride:
                SetStatusColor(moduleStatus, Color.magenta); // Magenta for manual override
                break;
            case ModuleState.CriticalAlert:
                FlashStatusColor(moduleStatus, Color.red); // Flashing red for critical alerts
                break;
            case ModuleState.Processing:
                FlashStatusColor(moduleStatus, Color.green); // Flashing green for active processing
                break;
            case ModuleState.Idle:
                SetStatusColor(moduleStatus, Color.white);
                break;
            case ModuleState.Disabled:
                SetStatusColor(moduleStatus, Color.grey);
                break;
        }
    }

    private void SetStatusColor(ModuleStatus moduleStatus, Color color)
    {
        if (moduleStatus.statusIndicator != null)
        {
            Renderer renderer = moduleStatus.statusIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }

    private void FlashStatusColor(ModuleStatus moduleStatus, Color color)
    {
        // Implement flashing logic here
        // This could be a coroutine that toggles the color at certain intervals
        // Note: Actual implementation will depend on your specific requirements
    }
}
