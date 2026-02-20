# Horizon GUI

*(Screenshot of the Dashboard... Wait, where?)*

**A web-like UI framework for VRChat.**

Horizon is designed to streamline the UI development process in VRChat. It allows you to build complex, responsive menus using familiar (Maybe...) **HTML and CSS**, while automating the tedious parts of development—such as layout management, collider setup, and script binding.

It ships as two parts:
1. **The Core Framework**: A compiler that translates markup into optimized Unity UI and wires up UdonSharp events.
2. **The Dashboard**: A fully functional, out-of-the-box UI template (sidebar, player grids, modals) that serves as a practical example of the framework's capabilities.

---

## ⚡ Why use Horizon?

*   **HTML & CSS Workflow:** Write your layout in `.html` and style it in `.css`. The compiler handles the layout groups, spacing, and rounding. It's pure web-like development inside Unity.
*   **Zero-Friction Binding:** Forget dragging references in the inspector. Tag an element with `u-bind="MyVar"` in HTML, and the system automatically links it to the `MyVar` field in your C# script during compilation.
*   **Pre-baked UI Systems:** Includes high-level logic out of the box. `<h-grid pool="64">` automatically creates a fixed object pool for player lists or inventories, while `h-view` and `h-change` attributes automate menu navigation without extra code.
*   **Built for VRChat:** It generates pure Unity UI with `VRCUiShape` components and colliders attached. No runtime parsing tax — the final output is zero-overhead, optimized GameObjects.

## 🚀 Quick Start

1. Right-click anywhere in your scene hierarchy and select `GameObject -> Horizon -> Create UI System`.
2. Select the newly created **Horizon UI System** object.
3. In the Inspector, click **"Initialize Dashboard Environment"**. This unpacks the default HTML/CSS templates and spawns the example logic scripts.
4. Hit **"COMPILE INTERFACE"**. Watch the hierarchy populate with a fully built, styled, and wired Canvas.

---

## 💡 How it works (The Workflow)

Write your layout, write your logic, and let Horizon connect them. Here is a simple Audio Settings example.

**1. The Layout (`.html`)**
Notice how we handle routing (`h-change`/`h-view`) and C# bindings (`u-bind`/`u-click`) directly in the markup.

```html
<!-- Navigation Button -->
<button class="tab-btn" h-change="Main:Audio">Audio Settings</button>

<!-- The actual page view -->
<div class="page-content" h-view="Main:Audio">
    <h2>Volume Control</h2>
    <hr />

    <div class="row">
        <!-- Calls ToggleMute() on click -->
        <button class="icon-btn" u-click="ToggleMute">
            <icon src="speaker_icon.png" />
        </button>

        <!-- Binds to the C# VolumeSlider variable and calls OnVolumeChange() -->
        <input type="range" min="0" max="1" u-bind="VolumeSlider" u-click="OnVolumeChange" />
    </div>

    <!-- Binds to the C# StatusText variable -->
    <text class="dim-text" u-bind="StatusText">Volume: 100%</text>
</div>
```

**2. The Logic (`.cs`)**
Drop this script anywhere inside your Horizon UI System object. When you hit "Compile", Horizon scans its children, finds this script, and injects the UI references automatically.

```csharp
using UdonSharp;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class AudioSettingsModule : UdonSharpBehaviour
{
    // Automatically populated by the compiler thanks to 'u-bind'
    public Slider VolumeSlider;
    public TextMeshProUGUI StatusText;

    // Optional: Called by the compiler in the Editor right after building the UI.
    // Great for baking default states or version numbers before uploading!
    public void OnHorizonBuild()
    {
        VolumeSlider.value = 1f;
        StatusText.text = "Volume: 100%";
    }

    public void ToggleMute()
    {
        VolumeSlider.value = 0f;
        UpdateStatus();
    }

    public void OnVolumeChange()
    {
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int percent = Mathf.RoundToInt(VolumeSlider.value * 100);
        StatusText.text = $"Volume: {percent}%";
    }
}
```