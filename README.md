# Horizon GUI 🎨

**A modular toolkit for building interfaces in VRChat.**

Horizon is a system designed to bridge the gap between Udon code and visual UI elements. It helps you construct responsive menus, bind scripts automatically, and manage styles without getting lost in the Unity hierarchy.

---

## Key Features

- **Markup-Based Workflow**
  Define your UI structure using standard **HTML** and style it with **CSS**. No need to manually position RectTransforms or compile complex C# factories.

- **CSS Styling**
  Centralized styling system. Change colors, rounding, and fonts in a standard `.css` file, and the entire UI updates upon rebuild.

- **Declarative Logic (`u-script`)**
  Assign specific UdonSharp behaviours to different modules directly in HTML. Decompose your logic into small, manageable scripts.

- **Auto-Binding**
  Automatically detects UI components and links them to your UdonSharp variables and events using simple attributes (`u-bind`, `u-click`).

- **Glassmorphism**
  Includes a lightweight background blur shader optimized for VR.

---

## Quick Start

1.  **Create the System:**
    Right-click in the Hierarchy: `Horizon -> Create UI System`.
2.  **Configure:**
    Select the created object. You will see the **Horizon GUI Authoring** component.
    Assign your `.html` and `.css` files into the respective slots.
3.  **Build:**
    Click the **"COMPILE INTERFACE"** button in the Inspector.
    The system will construct the Canvas, layout, and link all Udon scripts automatically.

---

## Workflow Example

Horizon allows you to bind logic purely through markup attributes, keeping your C# code clean from UI references.

**1. HTML Definition**
```html
<!-- Define a module and attach a specific script to it -->
<module id="Weather" u-script="HorizonGUI_WeatherModule">
    
    <h1>Weather Control</h1>
    <hr />

    <!-- Bind button click to a public method 'ToggleRain' -->
    <button u-click="ToggleRain">
        <icon src="cloud_rain.png" />
    </button>

    <!-- Bind slider value to a public float variable 'timeOfDay' -->
    <input type="range" min="0" max="1" u-bind="timeOfDay" />

</module>
```

**2. C# Logic (UdonSharp)**
```csharp
public class HorizonGUI_WeatherModule : UdonSharpBehaviour
{
    // Automatically linked by 'u-bind="timeOfDay"'
    public Slider timeOfDay; 

    // Automatically called by 'u-click="ToggleRain"'
    public void ToggleRain()
    {
        Debug.Log("Rain toggled!");
    }
}
```