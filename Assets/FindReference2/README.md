# Find Reference 2 (FR2)

**Version**: 2.5.11  
**Last Update**: December 5, 2024  
**Author**: Vietlabs  

Welcome to **Find Reference 2 (FR2)**, the ultimate tool for cleaning up Unity projects, optimizing build sizes, and improving modularity. FR2 provides a clear view of asset dependencies, usage counts, and much more to help streamline your Unity development workflow.

---

## 🚀 Features

- **Usage Count for Assets**: See how often each asset is used in the project.
- **Dependency Mapping**: Analyze both forward (used by) and backward (uses) dependencies for:
  - Scenes
  - Prefabs
  - Materials
  - Textures
  - Shaders, and more!
- **Find and Merge Duplicates**: Locate and replace duplicate assets.
- **Remove Unused Assets**: One-click removal of all unused assets.
- **Subsystem Support**: Works with Unity systems like:
  - Addressable
  - Shader Graph
  - UI Toolkit
  - Terrain, and more!
- **Custom Export**: Export dependencies or assets for isolated usage.
- **Temporarily Disable FR2**: Pause monitoring for improved performance during non-essential tasks.

---

## 🛠 Getting Started

1. Import the FR2 package into your Unity project.
2. Open the FR2 panel via `Windows > Find Reference 2`.
3. Click the **Scan Project** button in the FR2 panel to begin.

### Basic Usage
- **Usage Count**: Displays next to assets in the Project panel.
- **Dependencies**:
  - **Uses**: Lists assets the selected item depends on.
  - **Used By**: Lists assets that reference the selected item.

---

## 🌟 Rating and Feedback

Your feedback is incredibly valuable. If FR2 meets your expectations, consider leaving a 5-star review on the [Unity Asset Store](http://tiny.cc/fr2-assetstore). It helps us improve and reach more developers.

If it doesn’t, let us know what can be improved! Email us at [unity3d@vietlabs.net](mailto:unity3d@vietlabs.net).

---

## 📚 FAQs

### How does FR2 help reduce build size?
1. **Identify Unwanted Assets**:
   - View all assets in a scene and remove unnecessary ones.
2. **Exclude Placeholder Assets**:
   - Move placeholder assets to a separate folder to exclude them from the final build.
3. **Optimize AssetBundles**:
   - Detect and resolve duplicate assets in multiple AssetBundles.

### How does FR2 help modularize projects?
1. Select a scene or prefab to list all its dependencies.
2. Group assets by type and organize them into folders.
3. Move assets to isolate modules, ensuring better project structure and maintainability.

---

## ⚠️ Important Notes

- **Backup Before Merging**: Asset merging is a powerful but irreversible process. Always backup your project before using the **Merge & Replace** function.
- **GUID Tools**:
  - Provides advanced options like merging GUIDs. Use cautiously as there’s no undo.

---

## 📺 Resources

- **[Intro Video](http://tiny.cc/fr2-video-intro)**
- **[Documentation](http://tiny.cc/fr2-docs)**
- **[Forum](http://tiny.cc/fr2-forum)**
- **[Unity Asset Store](http://tiny.cc/fr2-assetstore)**

---

Thank you for choosing Find Reference 2. Happy developing!