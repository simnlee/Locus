# Locus - VR Location Scouting and Shotlist Copilot for Content Creators

## Problem to solve

Film preproduction teams and amateur filmmakers waste significant time and money on location scouting and shotlist planning. You don't know if a place works until you're there: lighting's wrong, streets are crowded, camera moves don't fit. For indie creators and short-form teams, flying to scout is unrealistic; for studios, scouting dozens of options is slow and expensive. Existing virtual tools (Street View screenshots, moodboards) are flat and don't translate into camera-accurate plans or a shareable storyboard you can iterate on.

## Our solution

Locus is a VR Location Scouting and Shot Planning tool for the Meta Quest that lets creators explore any real-world location in 3D and instantly generate camera-ready shot ideas. Say, *"Hey Locus, pull up 30 Causeway St, Boston."* Explore the environment, and Locus overlays a living storyboard: framing suggestions, time-of-day variations, and script-style shot descriptions you can refine with your voice.

## Who it's for

Hollywood preproduction teams, amateur/student filmmakers, commercial directors, YouTubers/TikTokers—anyone who wants to plan production for a real-world location without being there in person.

## What it does today

- **Go anywhere:** Load real locations in a Quest app using Cesium for Unity and walk around with Meta Interaction SDK.
- **Capture → Generate:** Takes periodic snapshots of your view and sends it to our Stable Diffusion-based image processing pipeline with a short user intent (e.g., *"golden hour, wet streets, neon reflections"*).
- **Return a scene image:** Our API produces a scenic image tailored to the user's story or outline (e.g., day→night, mood/style change) you can review and save.

## How we built it

- **VR client:** Unity + Cesium for Unity (global 3D tiles) + Meta Interaction SDK for locomotion/interactions.
- **Model service:** Python Flask server on Colab GPU, exposed publicly with ngrok.
- **Gen pipeline:** Snapshot (image) + user_intent → SD-aware prompt (LLM) → Stable Diffusion 1.5 (img2img) → scene image

## Looking into the future

- **VR overlays:** Render the generated frames in-headset as shot cards on top of the live Cesium view.
- **Auto-storyboard:** Periodic auto-capture → rank best frames → assemble a shot sequence with script-style blurbs (lens, move, action).
- **Voice loop:** "Make it noir / push to 35mm / add rain" → regenerate and update the sequence hands-free.
- **Stronger geometry control:** ControlNet (depth/canny/pose) + IP-Adapter for style/identity lock; SDXL/Flux for higher-fidelity frames.
- **Exports & collaboration:** PDF shotlists, team comments, and DCC integrations.
