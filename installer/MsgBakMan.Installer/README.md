# Custom Installer Graphics

This directory contains custom graphics for the MsgBakMan MSI installer.

## Files

- **BannerTop.bmp** (493×58) - Modern gradient banner displayed at the top of installer dialogs
- **DialogBackground.bmp** (493×312) - Background image for welcome and completion dialogs
- **License-en.rtf** - End-user license agreement text

## Design

The installer uses a modern visual design with:
- Blue-to-purple gradient banner with product name and tagline
- Clean, light background with subtle gradients for the dialog screens
- Decorative accent elements for visual interest
- Professional appearance matching modern Windows application standards

## Regenerating Graphics

If you need to modify the graphics, edit `create_installer_graphics.py` and run:

```bash
python3 create_installer_graphics.py
```

This will regenerate both BMP files with your changes.

## Specifications

The graphics follow WiX Toolset requirements:
- Banner: 493×58 pixels, 24-bit BMP
- Dialog Background: 493×312 pixels, 24-bit BMP
- Format: Windows BMP (no transparency support)

These dimensions and format are required by Windows Installer and WiX Toolset.
