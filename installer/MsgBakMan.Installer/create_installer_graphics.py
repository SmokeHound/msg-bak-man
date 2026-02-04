#!/usr/bin/env python3
"""
Creates modern, professional installer graphics for WiX MSI installer.
"""

from PIL import Image, ImageDraw, ImageFont
import os

def create_banner_bmp():
    """Create modern banner bitmap (493 x 58 pixels)."""
    width, height = 493, 58
    
    # Modern gradient background (blue-purple gradient)
    img = Image.new('RGB', (width, height), color='white')
    draw = ImageDraw.Draw(img)
    
    # Create gradient from left to right
    start_color = (41, 98, 255)  # Modern blue
    end_color = (120, 80, 255)   # Modern purple
    
    for x in range(width):
        ratio = x / width
        r = int(start_color[0] + (end_color[0] - start_color[0]) * ratio)
        g = int(start_color[1] + (end_color[1] - start_color[1]) * ratio)
        b = int(start_color[2] + (end_color[2] - start_color[2]) * ratio)
        draw.line([(x, 0), (x, height)], fill=(r, g, b))
    
    # Add product name text
    try:
        # Try to use a modern font, fall back to default if not available
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 28)
    except:
        font = ImageFont.load_default()
    
    text = "MsgBakMan"
    # Get text bounding box for centering
    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    
    text_x = 20
    text_y = (height - text_height) // 2 - 5
    
    # Draw text with slight shadow for depth
    draw.text((text_x + 2, text_y + 2), text, fill=(0, 0, 0, 128), font=font)
    draw.text((text_x, text_y), text, fill='white', font=font)
    
    # Add tagline
    try:
        small_font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 11)
    except:
        small_font = ImageFont.load_default()
    
    tagline = "SMS Backup Manager"
    draw.text((text_x + 2, text_y + 30), tagline, fill=(255, 255, 255, 200), font=small_font)
    
    img.save('BannerTop.bmp')
    print(f"✓ Created BannerTop.bmp ({width}x{height})")

def create_dialog_bmp():
    """Create modern dialog background bitmap (493 x 312 pixels)."""
    width, height = 493, 312
    
    # Modern gradient background
    img = Image.new('RGB', (width, height), color='white')
    draw = ImageDraw.Draw(img)
    
    # Vertical gradient from white to light blue-gray
    start_color = (245, 247, 250)  # Very light blue-gray
    end_color = (225, 235, 245)    # Light blue
    
    for y in range(height):
        ratio = y / height
        r = int(start_color[0] + (end_color[0] - start_color[0]) * ratio)
        g = int(start_color[1] + (end_color[1] - start_color[1]) * ratio)
        b = int(start_color[2] + (end_color[2] - start_color[2]) * ratio)
        draw.rectangle([(0, y), (width, y + 1)], fill=(r, g, b))
    
    # Add decorative accent on left side
    accent_width = 150
    accent_color = (41, 98, 255, 40)  # Semi-transparent modern blue
    
    # Create a semi-transparent overlay
    overlay = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    overlay_draw = ImageDraw.Draw(overlay)
    
    # Draw diagonal accent shape
    points = [
        (0, 0),
        (accent_width, 0),
        (accent_width + 50, height),
        (0, height)
    ]
    overlay_draw.polygon(points, fill=accent_color)
    
    # Composite with main image
    img = img.convert('RGBA')
    img = Image.alpha_composite(img, overlay)
    img = img.convert('RGB')
    
    # Add welcome text area (this will be behind the actual dialog text)
    draw = ImageDraw.Draw(img)
    
    try:
        title_font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 32)
    except:
        title_font = ImageFont.load_default()
    
    # Add subtle pattern or icon suggestion in the accent area
    try:
        small_font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 14)
    except:
        small_font = ImageFont.load_default()
    
    # Add decorative elements (circles) for modern look
    draw.ellipse([30, 40, 50, 60], fill=(255, 255, 255, 100), outline=(41, 98, 255))
    draw.ellipse([35, 80, 45, 90], fill=(255, 255, 255, 80), outline=(120, 80, 255))
    draw.ellipse([60, 100, 80, 120], fill=(255, 255, 255, 100), outline=(41, 98, 255))
    
    img.save('DialogBackground.bmp')
    print(f"✓ Created DialogBackground.bmp ({width}x{height})")

def main():
    print("Creating modern installer graphics...")
    print()
    
    create_banner_bmp()
    create_dialog_bmp()
    
    print()
    print("Graphics created successfully!")
    print()
    print("Files created:")
    print("  - BannerTop.bmp (493x58) - Top banner for installer dialogs")
    print("  - DialogBackground.bmp (493x312) - Background for welcome/completion screens")

if __name__ == '__main__':
    main()
