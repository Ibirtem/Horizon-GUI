# Horizon GUI 🎨

**A modular toolkit for building interfaces in Unity & VRChat.**

Horizon provides a set of tools to construct responsive UIs and connect them to your game logic seamlessly. It serves as a bridge between visual elements and Udon code.

---

## Key Features

*   **Interface Toolkit**
    A structured collection of utilities to build and manage UI layouts programmatically. Designed to simplify the creation of menus and HUDs without getting lost in the Unity hierarchy.

*   **Glassmorphism**
    Includes a custom **Background Blur** shader to create modern, frosted glass visuals that are optimized for VR performance.

*   **Auto-Binding**
    Automatically detects UI components (Buttons, Sliders) and links them to your UdonSharp behaviours. Removes the need for manual event assignment in the Inspector.

*   **Modular Architecture**
    The system is built to be expandable. You can create your own modules and builders to fit specific project needs.

---

## Quick Start

1.  **Import** the package into your project.
2.  Navigate to the top menu: `Horizon -> UI -> 2. Construct Main Layout`.
3.  **Done!** The system generates the Canvas, Camera, and Event System automatically.

---

## Code Example

```csharp
public HorizonGUIModule BuildPage(GameObject container)
{
    // Use the toolkit to create a simple column
    var page = HorizonGUIFactory.CreateColumn("MyPage", container, spacing: 20);

    // Add a pre-styled toggle
    var toggle = HorizonGUIFactory.CreateToggle(page, "Enable Magic", true);

    // Bind logic automatically
    return HorizonGUIFactory.ConfigureLogic<MyScript>(page, binder =>
    {
        binder.Bind("toggleReference", toggle);
    });
}
```

> **Note for Developers:**
> To understand the full API and available controls (Columns, Rows, Grids, Sliders), please refer to the documentation comments inside:
> `Packages/com.blackhorizon.horizongui/Editor/HorizonGUIFactory.cs`