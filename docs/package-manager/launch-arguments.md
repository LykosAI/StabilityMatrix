# Launch Arguments

Launch arguments configure how a package starts. Stability Matrix stores them per installation, so two copies of the same package can use different ports or startup options.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Edit Launch Options](#edit-launch-options)
- [Example: Change the ComfyUI Port](#example-change-the-comfyui-port)
- [Extra Launch Arguments](#extra-launch-arguments)
- [Arguments, Dependencies, and Environment Variables](#arguments-dependencies-and-environment-variables)
- [Troubleshooting](#troubleshooting)

---

## Edit Launch Options

1. Open **Packages** and find the installation to configure.
2. Choose **three-dot menu → Launch Options**.
3. Search for an option by its displayed title, or browse the available cards.
4. Change the relevant checkbox, choice, or value, then click **Save**.
5. Launch the package, or **Restart** it if it is already running.

Saving changes does not restart the running process. **Cancel** closes the dialog without saving the edited options.

The cards come from that package's definitions. A checkbox adds or removes a flag; a value field supplies a value for the displayed option. For example, enter just a port number in a **Port** field, not the entire `--port` argument.

Defaults can depend on the package, operating system, and detected hardware. An empty value field can leave an option at the package's own default. Avoid assuming that one package's options or defaults apply to another.

## Example: Change the ComfyUI Port

This is useful when another local service already occupies ComfyUI's default port.

1. Open **Launch Options** for the ComfyUI installation you intend to use.
2. Set **Port** to an unused port, for example `8189`.
3. Leave **Host** at `127.0.0.1` for use on this computer.
4. Click **Save**, then restart ComfyUI.
5. Check the console for successful startup and use **Web UI** to open its reported address.

Stability Matrix's ComfyUI definition uses `127.0.0.1` and `8188` as the default host and port. The built-in Inference client reads the managed package's host and port options, so configure them on the same installation that Inference uses. See [ComfyUI Integration](../advanced/comfyui-integration.md#comfyui-as-a-standalone-package).

## Extra Launch Arguments

Packages that support custom arguments include an **Extra Launch Arguments** field. Use it for flags that are not represented by the existing cards and that your installed package version supports.

Enter only the arguments. Do not include `python`, a script name, or a shell command. This field is passed through as argument text; unlike dedicated value fields, it does not add quotes around individual values for you. Quote a path containing spaces when the package's argument syntax requires it.

Avoid defining the same option both in a dedicated card and in **Extra Launch Arguments**. Duplicate or conflicting flags can cause startup errors or make the effective setting unclear. Use ComfyUI's **Host** and **Port** cards for connection settings so the Inference client can read them.

## Arguments, Dependencies, and Environment Variables

These settings solve different problems:

| Setting | Use it for |
|---|---|
| **Launch Options** | Flags read by the package when it starts, such as a port or a supported memory mode. |
| **Python Packages / PyTorch Index** | Installing the Python libraries and compute backend the package uses. |
| **Python Dependencies Override** | Pinning or excluding dependencies during installation and updates. |
| **Environment Variables** | Values read by Python, libraries, or child processes, such as cache and proxy settings. |

A launch flag does not install the GPU backend it needs. If a backend is missing or incorrect, consult [Hardware Support](../advanced/hardware-support.md) and [Installing Packages](installing-packages.md#selecting-a-hardware-backend). Environment configuration is covered separately in [Environment Variables](../advanced/environment-variables.md).

## Troubleshooting

| Symptom | What to check |
|---|---|
| The change has no effect | Confirm you saved, restarted, and edited the installation you actually launched. |
| `unrecognized arguments` at startup | Remove the last custom argument and check whether that installed package version supports it. |
| An address or port is already in use | Stop the other instance or select a different unused port. |
| Inference cannot connect after a port change | Check successful ComfyUI startup, the selected installation, and its **Host** and **Port** cards. |
| Startup fails after several changes | Undo the recent edits, including custom arguments, then reintroduce one change at a time. |

Keep the console error if the problem persists; [Common Issues](../troubleshooting/common-issues.md#finding-logs-and-reporting-bugs) explains what to include in a report.
