using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RH.Utilities
{
    /// <summary>
    /// Defines a scene group for organizing scenes
    /// </summary>
    [Serializable]
    public class SceneGroup
    {
        public string Name;
        public string Description;
        public Color Color = Color.gray;
        public bool IsExpanded = true;
        public List<SceneItem> Scenes = new List<SceneItem>();

        public SceneGroup(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Represents a scene item
    /// </summary>
    [Serializable]
    public class SceneItem
    {
        public string Name;
        public string Path;
        public string Description;
        public List<string> Tags = new List<string>();
        public string ThumbnailPath;
        public bool IsEnabled = true;
        public DateTime LastOpened;
        public DateTime LastValidated;
        public List<ValidationResult> ValidationResults = new List<ValidationResult>();
        public bool HasLightingBaked = false;
        public float EstimatedMemoryUsage = 0f;
        public float EstimatedLoadTime = 0f;
        public bool LoadAdditive = false;
        public int AccessCount = 0;
        public bool IsFavorite = false;
        public string Notes = "";
        public string ShortcutKey = "";
        
        public SceneItem() { }
        
        public SceneItem(string name, string path)
        {
            Name = name;
            Path = path;
            Description = "";
            LastOpened = DateTime.Now;
        }
        
        /// <summary>
        /// Returns true if this scene has any validation issues
        /// </summary>
        public bool HasValidationIssues()
        {
            return ValidationResults != null && ValidationResults.Any(r => !r.Success);
        }
        
        /// <summary>
        /// Returns all validation messages
        /// </summary>
        public List<string> GetValidationMessages()
        {
            if (ValidationResults == null)
                return new List<string>();
                
            return ValidationResults.Select(r => r.Message).ToList();
        }
        
        /// <summary>
        /// Increments the access count for this scene
        /// </summary>
        public void IncrementAccessCount()
        {
            AccessCount++;
            LastOpened = DateTime.Now;
        }
    }

    /// <summary>
    /// Represents user preferences for the scene manager
    /// </summary>
    [Serializable]
    public class SceneManagerPreferences
    {
        public bool ShowPaths = false;
        public bool ShowThumbnails = true;
        public bool ShowValidationStatus = true;
        public bool ShowPerformanceMetrics = false;
        public bool AutoSaveOnSceneChange = true;
        public bool ConfirmSceneLoad = true;
        public bool TrackRecentScenes = true;
        public int MaxRecentScenes = 10;
        public bool SyncWithVersionControl = false;
        public string DefaultPlayModeScene = "";
        public bool UseDefaultPlayModeScene = false;
        public SortMode SceneSortMode = SortMode.AlphabeticalAsc;
        public ViewMode SceneViewMode = ViewMode.Detailed;
        public string UserIdentifier = "";
    }

    /// <summary>
    /// Defines how scenes are sorted in the UI
    /// </summary>
    public enum SortMode
    {
        AlphabeticalAsc,
        AlphabeticalDesc,
        LastAccessed,
        MostAccessed,
        Custom
    }

    /// <summary>
    /// Defines how scenes are displayed in the UI
    /// </summary>
    public enum ViewMode
    {
        Compact,
        Detailed,
        Grid
    }

    /// <summary>
    /// Defines a custom macro for scene operations
    /// </summary>
    [Serializable]
    public class SceneMacro
    {
        public string Name;
        public string Description;
        public List<SceneItem> ScenesInvolved = new List<SceneItem>();
        public bool LoadAdditive;
        public bool EnableDebugMode;
        public string CustomAction;
        public string ShortcutKey;

        public SceneMacro(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Defines a validation check result
    /// </summary>
    [Serializable]
    public class ValidationResult
    {
        public bool Success;
        public string Message;
        public ValidationSeverity Severity;

        public ValidationResult(bool success, string message, ValidationSeverity severity = ValidationSeverity.Warning)
        {
            Success = success;
            Message = message;
            Severity = severity;
        }
    }

    /// <summary>
    /// Defines the severity of a validation issue
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// User preferences for the scene manager
    /// </summary>
    [Serializable]
    public class UserPreferences
    {
        // UI Settings
        public bool ShowThumbnails = true;
        public bool ShowPaths = false;
        public bool ShowSceneTags = true;
        public bool EnableDarkMode = false;
        public bool ShowValidationStatus = true;
        public bool ShowPerformanceMetrics = false;
        
        // Behavior Settings
        public bool ConfirmSceneLoad = true;
        public bool TrackRecentScenes = true;
        public int MaxRecentScenes = 10;
        public bool AutoValidateOnLoad = false;
        
        // Validation Settings
        public bool ValidateMissingReferences = true;
        public bool ValidatePerformance = true;
        public bool ValidateLighting = true;
        
        // Data Management
        public bool AutoSaveGroups = true;
        public bool BackupBeforeClear = true;
    }
}
