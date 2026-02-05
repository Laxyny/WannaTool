# WannaTool

> WannaTool is a lightweight and focused Windows launcher designed to stay fast, simple, and out of your way.  
> It provides quick access to applications and a small set of power-user actions — without background bloat or unnecessary services.

Inspired by tools like Spotlight and Raycast, WannaTool is built specifically for Windows, with performance and restraint as first-class goals.

![GitHub last commit](https://img.shields.io/github/last-commit/laxyny/wannatool?style=for-the-badge)
![GitHub issues](https://img.shields.io/github/issues/laxyny/wannatool?style=for-the-badge)
![GitHub pull requests](https://img.shields.io/github/issues-pr/laxyny/wannatool?style=for-the-badge)
![GitHub license](https://img.shields.io/github/license/laxyny/wannatool?style=for-the-badge)

<p align="center">
  <img src="https://raw.githubusercontent.com/Laxyny/WannaTool/main/.github/assets/screenshot.png" alt="WannaTool Screenshot" width="700"/>
</p>

---

## Philosophy

WannaTool is intentionally minimal.

It does **not** try to replace Task Manager, MSI Center, or system “optimizer” tools.  
It avoids background monitoring, aggressive cleaners, and permanent services.

Every feature follows a simple rule:

> If it is not explicitly useful, fast, and user-initiated, it does not belong in WannaTool.

The goal is to provide a clean launcher and a small set of reliable power-user actions — nothing more.

---

## Core Features

- **Quick launcher**  
  Open applications, files, and shortcuts instantly using `Alt + Space`.

- **Process inspection & termination**  
  View the heaviest running processes and terminate them on demand (`!top`, `!kill`).  
  No background monitoring, no automation.

- **Lightweight system monitoring**  
  Optional CPU and GPU usage display, designed to stay near zero cost when idle.

- **Modern and clean UI**  
  Minimal interface, high-quality icons, DPI-aware, and fully multi-monitor compatible.

- **Persistent settings**  
  User configuration stored safely in AppData, with instant application of changes.

---

## What WannaTool Is *Not*

To keep the project focused and reliable, WannaTool deliberately does **not** include:

- RAM cleaners or “boosters”
- Automatic system or service optimizers
- Battery or peripheral monitoring
- Heavy background watchers

Those features exist elsewhere and do not align with the purpose of this tool.

---

## Current Status

WannaTool is currently in **alpha**.

The core architecture is stable, but features and UI may evolve.  
The focus is on correctness, performance, and polish rather than rapid feature expansion.

---

## Installation

Prebuilt binaries will be provided in GitHub Releases.

For development:

  ```bash
    git clone https://github.com/laxyny/wannatool.git
  ```

---

## Roadmap (Short Term)
- Further performance and memory optimizations
- Command system improvements
- UX polish and edge-case handling
- Documentation and command discovery

No large features are planned without clear justification.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

Developed and maintained by [Kevin Gregoire - Nodasys](https://github.com/laxyny).
