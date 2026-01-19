# Horizon GUI 🎨

**A modular toolkit for building interfaces in VRChat.**

Horizon is a system designed to bridge the gap between Udon code and visual UI elements. It helps you construct responsive menus, bind scripts automatically, and manage styles without getting lost in the Unity hierarchy.

---

## Key Features

- **Markup-Based Workflow**
  Define your UI structure using familiar **HTML** and style it with **CSS**.

- **CSS Styling**
  A centralized styling system. Change colors, spacing, and fonts in a standard `.css` file, and the entire UI updates upon rebuild.

- **Auto-Discovery & Direct Binding**
  Place your UdonSharp logic scripts within the UI System's hierarchy. Horizon automatically finds them and links UI components to your C# variables by matching names (e.g., `u-bind="MyButton"` links to `public Button MyButton;`).

- **Editor-Time Events**
  Implement an `OnHorizonBuild()` method in your scripts to run code immediately after the UI is compiled, perfect for baking version numbers or setting initial states.

- **Glassmorphism**
  Includes a lightweight background blur shader optimized for VR.

---

## Quick Start

1.  **Create the System:**
    Right-click in the Hierarchy: `GameObject -> Horizon -> Create UI System`.
2.  **Initialize (Optional):**
    Select the created object. In the Inspector, click **"Initialize Dashboard Environment"** to generate default templates and logic scripts.
3.  **Build:**
    Click the **"COMPILE INTERFACE"** button. The system will construct the Canvas, layout, and link all Udon scripts automatically.

---

## Workflow Example

Horizon links your HTML layout to your UdonSharp code through a simple name-matching system.

**1. Define the UI in HTML**

Use `u-bind` to mark elements that your scripts will interact with, and `u-click` to specify which method to call on an event.

```html
<!-- A view for weather controls -->
<div class="weather-panel" u-bind="Weather_View">
  <h1>Weather Control</h1>
  <hr />

  <!-- This button will call the "ToggleRain" method in your C# script -->
  <button u-click="ToggleRain">
    <icon src="cloud_rain.png" />
  </button>

  <!-- This slider will be linked to a C# variable named "Weather_TimeSlider" -->
  <input type="range" min="0" max="1" u-bind="Weather_TimeSlider" />
</div>
```

**2. Write the Logic in C#**

Create a GameObject inside your `Horizon UI System` (e.g., "Logic_Weather") and attach this UdonSharp script to it.

```csharp
// HorizonGUI_WeatherModule.cs
public class HorizonGUI_WeatherModule : UdonSharpBehaviour
{
    // These variables are filled automatically by the compiler
    // because their names match the 'u-bind' attributes in the HTML.
    public GameObject Weather_View;
    public Slider Weather_TimeSlider;

    // This method is called by the button with 'u-click="ToggleRain"'
    public void ToggleRain()
    {
        Debug.Log("Rain toggled!");
        Debug.Log("Current time from slider: " + Weather_TimeSlider.value);
    }
}
```

When you press **"COMPILE INTERFACE"**, Horizon finds your script, matches the variable names to the `u-bind` attributes in the HTML, and links them for you.
