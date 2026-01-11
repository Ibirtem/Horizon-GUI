# Horizon GUI 🎨

**A modular toolkit for building interfaces in VRChat.**

Horizon is a system designed to bridge the gap between Udon code and visual UI elements. It helps you construct responsive menus, bind scripts automatically, and manage styles without getting lost in the Unity hierarchy.

---

## Key Features

- **Builder Workflow**
  Generate complex layouts (Columns, Grids, Sidebars) programmatically or via configuration, rather than placing RectTransforms manually.
- **Theme System**
  Centralized "CSS-like" styling. Change colors and font sizes in one ScriptableObject, and the entire UI updates upon rebuild.

- **Auto-Binding**
  Automatically detects UI components and links them to your UdonSharp behaviours. Removes the need for manual event assignment in the Inspector.

- **Glassmorphism**
  Includes a lightweight background blur shader optimized for VR.

---

## Quick Start

1.  **Create the System:**
    Right-click in the Hierarchy: `Horizon -> Create UI System`.
2.  **Configure:**
    Select the created object. You will see the **Horizon GUI Authoring** component.
    Assign a **Theme** (Create one via `Create -> Horizon -> UI Theme` if needed).

3.  **Build:**
    Click the **"GENERATE INTERFACE"** button in the Inspector.
    The system will construct the Canvas, layout, and link all Udon scripts automatically.

---

## Code Example

```csharp
public HorizonGUIModule BuildPage(GameObject container)
{
    // Create a simple column
    var page = HorizonGUIFactory.CreateColumn("MyPage", container, spacing: 20);

    // Add a styled header
    HorizonGUIFactory.CreateText(page, "Settings", HorizonGUIFactory.TextStyle.H1);

    // Add a toggle
    var toggle = HorizonGUIFactory.CreateToggle(page, "Enable Magic", true);

    // Bind logic automatically
    return HorizonGUIFactory.ConfigureLogic<MyScript>(page, binder =>
    {
        binder.Bind("toggleReference", toggle);
    });
}
```
