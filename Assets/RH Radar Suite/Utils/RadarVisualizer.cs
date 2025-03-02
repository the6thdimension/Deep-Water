using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Visualizes radar coverage and detected contacts in the scene view.
    /// </summary>
    [RequireComponent(typeof(RadarSuiteController))]
    public class RadarVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [Tooltip("Enable radar coverage visualization")]
        public bool showRadarCoverage = true;
        
        [Tooltip("Enable contact visualization")]
        public bool showContacts = true;
        
        [Tooltip("Color for radar coverage")]
        public Color coverageColor = new Color(0.2f, 0.8f, 1f, 0.1f);
        
        [Tooltip("Color for radar beam")]
        public Color beamColor = new Color(0.2f, 0.8f, 1f, 0.3f);
        
        [Tooltip("Color for detected contacts")]
        public Color contactColor = Color.red;
        
        [Tooltip("Size of contact markers")]
        public float contactMarkerSize = 1f;
        
        [Tooltip("Show contact information")]
        public bool showContactInfo = true;
        
        [Tooltip("Distance at which to show contact labels")]
        public float labelVisibilityDistance = 500f;
        
        [Header("Debug Visualization")]
        [Tooltip("Show scan points")]
        public bool showScanPoints = true;
        
        [Tooltip("Maximum number of scan points to display")]
        public int maxScanPoints = 100;
        
        [Tooltip("Color for scan points")]
        public Color scanPointColor = new Color(1f, 0.5f, 0f, 0.5f);
        
        // References
        private RadarSuiteController radarController;
        private Camera mainCamera;
        
        // Visualization data
        private List<Vector3> scanPoints = new List<Vector3>();
        
        private void Awake()
        {
            radarController = GetComponent<RadarSuiteController>();
            mainCamera = Camera.main;
        }
        
        private void OnEnable()
        {
            if (radarController != null)
            {
                radarController.OnContactDetected += HandleContactDetected;
                // We'll manually add scan points instead of using an event
            }
        }
        
        private void OnDisable()
        {
            if (radarController != null)
            {
                radarController.OnContactDetected -= HandleContactDetected;
                // No need to unsubscribe from scan points event
            }
        }
        
        private void HandleContactDetected(RadarContact contact)
        {
            // Could add special visualization for newly detected contacts
        }
        
        // Manual method to add scan points from LOD modules
        public void AddScanPoint(Vector3 point)
        {
            if (showScanPoints)
            {
                scanPoints.Add(point);
                if (scanPoints.Count > maxScanPoints)
                {
                    scanPoints.RemoveAt(0);
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (radarController == null) return;
            
            // Draw radar coverage
            if (showRadarCoverage && radarController.IsActive)
            {
                DrawRadarCoverage();
            }
            
            // Draw scan points
            if (showScanPoints && scanPoints.Count > 0)
            {
                Gizmos.color = scanPointColor;
                foreach (var point in scanPoints)
                {
                    Gizmos.DrawSphere(point, 0.5f);
                }
            }
            
            // Draw contacts
            if (showContacts && radarController.IsActive)
            {
                DrawContacts();
            }
        }
        
        private void DrawRadarCoverage()
        {
            // Draw detection range sphere
            Gizmos.color = coverageColor;
            Gizmos.DrawWireSphere(transform.position, radarController.MaxDetectionRange);
            
            // Draw current beam direction based on LOD
            switch (radarController.CurrentLOD)
            {
                case RadarLOD.LOD2_BasicRadar:
                case RadarLOD.LOD3_DopplerRadar:
                    DrawRadarBeam();
                    break;
                
                case RadarLOD.LOD4_3DTracking:
                case RadarLOD.LOD5_HighFidelity:
                    DrawPhaseArrayBeam();
                    break;
            }
        }
        
        private void DrawRadarBeam()
        {
            // Get the current beam direction from the active module
            Vector3 beamDirection = transform.forward;
            float beamWidth = 30f; // Default
            
            // Try to get actual beam parameters from the active module
            var basicModule = GetComponent<BasicRadarModule>();
            if (basicModule != null && basicModule.isActive)
            {
                beamWidth = basicModule.beamWidth;
            }
            
            var dopplerModule = GetComponent<DopplerRadarModule>();
            if (dopplerModule != null && dopplerModule.isActive)
            {
                beamWidth = dopplerModule.beamWidth;
            }
            
            // Draw beam cone
            DrawBeamCone(beamDirection, beamWidth, radarController.MaxDetectionRange);
        }
        
        private void DrawPhaseArrayBeam()
        {
            // For phased array, we might have multiple beams or a steered beam
            // This is a simplified visualization
            var trackingModule = GetComponent<ThreeDTrackingModule>();
            if (trackingModule != null && trackingModule.isActive)
            {
                // Draw a narrower beam
                DrawBeamCone(transform.forward, 10f, radarController.MaxDetectionRange);
            }
            else
            {
                // Default visualization
                DrawBeamCone(transform.forward, 20f, radarController.MaxDetectionRange);
            }
        }
        
        private void DrawBeamCone(Vector3 direction, float beamWidthDegrees, float range)
        {
            Gizmos.color = beamColor;
            
            float beamWidthRadians = beamWidthDegrees * Mathf.Deg2Rad;
            float radius = Mathf.Tan(beamWidthRadians / 2) * range;
            
            // Draw cone
            Vector3 endPoint = transform.position + direction * range;
            DrawWireCone(transform.position, endPoint, radius);
        }
        
        private void DrawWireCone(Vector3 start, Vector3 end, float radius)
        {
            Vector3 up = Vector3.up;
            if (Vector3.Dot((end - start).normalized, up) > 0.99f)
            {
                up = Vector3.forward;
            }
            
            Vector3 forward = (end - start).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right);
            
            // Draw end cap
            int segments = 20;
            float angleStep = 360f / segments;
            
            Vector3 previousPoint = end + right * radius;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = end + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                
                Gizmos.DrawLine(previousPoint, newPoint);
                Gizmos.DrawLine(start, previousPoint);
                
                previousPoint = newPoint;
            }
        }
        
        private void DrawContacts()
        {
            if (radarController.ActiveContacts.Count == 0) return;
            
            Gizmos.color = contactColor;
            
            foreach (var contact in radarController.ActiveContacts)
            {
                if (contact == null || !contact.IsActive) continue;
                
                // Draw contact marker
                Gizmos.DrawSphere(contact.Position, contactMarkerSize);
                
                // Draw line from radar to contact
                Gizmos.DrawLine(transform.position, contact.Position);
                
                // Draw contact info if enabled and camera is close enough
                if (showContactInfo && mainCamera != null)
                {
                    float distanceToCamera = Vector3.Distance(mainCamera.transform.position, contact.Position);
                    if (distanceToCamera <= labelVisibilityDistance)
                    {
                        DrawContactLabel(contact);
                    }
                }
            }
        }
        
        private void DrawContactLabel(RadarContact contact)
        {
            // This needs to be implemented in OnGUI for proper text rendering
            // We'll just mark the position for now
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(contact.Position, contactMarkerSize * 1.5f);
        }
        
        private void OnGUI()
        {
            if (!showContactInfo || !showContacts || !radarController.IsActive || mainCamera == null)
                return;
            
            foreach (var contact in radarController.ActiveContacts)
            {
                if (contact == null || !contact.IsActive) continue;
                
                float distanceToCamera = Vector3.Distance(mainCamera.transform.position, contact.Position);
                if (distanceToCamera <= labelVisibilityDistance)
                {
                    Vector3 screenPos = mainCamera.WorldToScreenPoint(contact.Position);
                    if (screenPos.z > 0) // Only if in front of camera
                    {
                        string info = $"{contact.TargetName}\n" +
                                     $"Range: {contact.Range:F0}m\n" +
                                     $"Signal: {contact.SignalStrength:P0}";
                        
                        GUI.color = Color.yellow;
                        GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 60), info);
                    }
                }
            }
        }
    }
}
