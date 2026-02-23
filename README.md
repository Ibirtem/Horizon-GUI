# Horizon GUI

_(Screenshot of the Dashboard... Wait, where?)_

**A web-like UI framework for VRChat.**

Horizon is designed to streamline the UI development process in VRChat. It allows you to build complex, responsive menus using familiar **HTML and CSS**, while automating the tedious parts of development—such as layout management, collider setup, and script binding.

---

## ⚡ Why use Horizon?

Horizon allows you to focus on _design_ and _logic_, effectively removing the "Unity UI" layer from your workflow.

### 1. The Framework (Core)

- **HTML & CSS Workflow:** Write your layout in `.html` and style it in `.css`. The compiler handles the layout groups, spacing, and rounding. It's pure web-like development inside Unity.
- **Zero-Friction Binding:** Forget dragging references in the inspector. Tag an element with `u-bind="MyVar"` in HTML, and the system automatically links it to the `MyVar` field in your C# script during compilation.
- **Optimized Output:** It generates pure Unity UI with `VRCUiShape` components and colliders automatically attached. No runtime parsing tax — the final output is zero-overhead, optimized GameObjects.

### 2. The Dashboard (Included)

Horizon ships with a production-ready Dashboard template that showcases the framework's capabilities. It's not just a UI skin; it includes full logic:

- 📸 **Smart Home Screen:** Features a **Live Player Grid** that uses a zero-overhead "Photobooth" system to render player avatars into UI slots dynamically.
- ✨ **Post-Processing Control:** A pre-wired module to control Bloom, AO, and other effects without touching layer masks manually.
- ☁️ **Weather Integration:** Out-of-the-box support for [Horizon Weather & Time](https://github.com/Ibirtem/Horizon-Weather-Time). Control time of day and weather profiles directly from the UI.

---

## 🚀 Quick Start

1. Right-click anywhere in your scene hierarchy and select `GameObject -> Horizon -> Create UI System`.
2. Select the newly created **Horizon UI System** object.
3. In the Inspector, click **"Setup Default Dashboard"**. This unpacks the default HTML/CSS templates and spawns the example logic scripts (Home, Weather, Post-Processing).
4. Hit **"COMPILE INTERFACE"**. Watch the hierarchy populate with a fully built, styled, and wired Canvas.

---

## ⚡ Syntax & Binding (Cheatsheet)

Horizon extends standard HTML with special attributes to wire up Udon logic without code. Until the Wiki is live, you can learn by example in `Horizon_Default_Layout.html` or check the logic in `Editor/Parsing/HorizonCompiler.cs`.

### Udon Binding (`u-*`)

Connects your UI directly to UdonSharp variables and methods.

- `u-bind="VariableName"`: Links this element to a public variable in your C# script.
  - _Text:_ Updates the text content.
  - _Input/Slider:_ Two-way binding (updates the variable when UI changes).
- `u-click="MethodName"`: Calls this public method when the element is clicked.

### Horizon Logic (`h-*`)

Controls the structure and navigation flow.

- `h-view="Channel:PageID"`: Marks a container as a "page" inside a specific channel (e.g., `Main:Settings`).
- `h-change="Channel:PageID"`: Turns a button into a navigation trigger that switches the visible view.
- `<h-grid>`: A special component for Data Grids (inventories, player lists). Uses object pooling automatically.

> **💡 Pro Tip:** You don't need to write `GetComponent<Text>()`. Just use `u-bind`, and the Compiler creates the reference for you.

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
    <input
      type="range"
      min="0"
      max="1"
      u-bind="VolumeSlider"
      u-click="OnVolumeChange"
    />
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
