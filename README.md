# Gameplay Systems Portfolio (Unity C#)

This repository showcases gameplay programming systems developed for **Dungeons of Mytheras**, focusing on clean, modular logic and reusable architecture.

## Featured Systems
### 1) Stamina Resource System (Base Character)
- Integer stamina with **float-based regen** using a fractional buffer
- Regen delay after spending
- Clamp to max stamina

**Key file:** `src/Character.cs`

### 2) Charged Ranged Attack (Elf)
- Stamina-gated charged shot execution
- **Aim snapshotting** at release (spawn + direction) to ensure consistent firing
- Cooldown applied after execution

**Key file:** `src/Elf.cs`

## Architecture Overview
