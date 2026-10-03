# RushHour

A third-person 3D delivery-dodging game built in Unity, addressing **SDG 11: Sustainable Cities and Communities**.

> *Every second counts. Every pothole costs.*

## Story

You play as **Kai**, a courier for **GreenLine Logistics**, delivering a sealed shipment of temperature-sensitive vaccines to the **Greenfield Community Clinic** on the edge of the city of **Veridia** — a district the city's infrastructure planning has repeatedly overlooked.

Piloting a lightweight electric delivery pod, Kai must navigate streets plagued by potholes, stalled construction, and congestion — all symptoms of underinvested urban infrastructure — while managing limited battery charge before the clinic's refrigeration window closes.

## Gameplay

- **Movement:** The vehicle drives forward automatically. Use **A/D** or **Left/Right arrow keys** to dodge between lanes.
- **Fuel/Charge:** Depletes constantly over time, and faster while dodging. Running out ends the delivery.
- **Obstacles:** Potholes, barricades, and debris spawn ahead with increasing frequency — colliding without a shield ends the run.
- **Protection Collectibles:** Glowing pickups representing small infrastructure investments (reinforced roads, cleared lanes). Grants temporary shield — absorbs one obstacle hit instead of crashing.
- **Goal:** Reach the delivery distance (or the clinic itself) before running out of fuel or crashing.

## Win / Fail States

| Outcome | Trigger |
|---|---|
| ✅ Delivered | Distance goal reached / clinic trigger entered |
| ❌ Crashed | Collision with an obstacle while unshielded |
| ❌ Out of Charge | Fuel reaches zero before delivery |

## SDG Mapping

**SDG 11 — Sustainable Cities and Communities.** The game's obstacles represent real consequences of poor urban infrastructure (potholes, stalled construction, congestion) on last-mile logistics, while the fuel mechanic reflects the added strain poor road conditions place on low-emission micro-vehicles. Protection collectibles represent the tangible benefit of targeted infrastructure investment.

## Current Scope (Early Version)

This version focuses on **core gameplay systems** only — environment art and atmosphere (Kerala-inspired setting via Aura volumetric lighting) are planned for a later milestone and not yet implemented. The current build uses placeholder geometry (boxes/planes) for all environment elements.

### Implemented
- Auto-forward driving with lane-dodge controls
- Fuel system with dodge-based drain penalty
- Obstacle spawner with time-based difficulty ramp
- Shield/protection collectible system
- Distance-based delivery goal
- Win/fail state handling with restart

### Not Yet Implemented
- Kerala-themed environment art and Aura volumetric atmosphere
- Start screen / main menu
- Sound design
- Polished vehicle and obstacle models
- Full UI (fuel bar, distance counter, status text) — scripts support it, UI elements not yet built in-scene

## Scripts

| Script | Purpose |
|---|---|
| `CarController.cs` | Forward movement, lane-dodge input, crash handling |
| `FuelSystem.cs` | Fuel drain (base + dodge penalty), triggers fuel-out failure |
| `ShieldSystem.cs` | Temporary invincibility from collectibles |
| `ObstacleSpawner.cs` | Spawns obstacles/collectibles ahead of the car, ramps difficulty over time |
| `Obstacle.cs` | Collision detection, shield consumption, self-cleanup |
| `Collectible.cs` | Pickup detection, activates shield |
| `GameManager.cs` | Tracks delivery distance, win/fail states, restart |

## Setup

1. Tag the car GameObject as `Player`.
2. Attach `CarController`, `FuelSystem`, and `ShieldSystem` to the Car.
3. Parent the Main Camera to the Car (local position ~`0, 3, -6`, slight downward tilt).
4. Create obstacle and collectible prefabs with trigger colliders, attach `Obstacle.cs` / `Collectible.cs` respectively.
5. Create empty `Spawner` and `GameManager` objects, attach `ObstacleSpawner.cs` and `GameManager.cs`.
6. Wire up Inspector references (Car, GameManager, prefabs) across all scripts.
7. **Note:** If you see an `InvalidOperationException` about `UnityEngine.Input`, go to **Edit → Project Settings → Player → Active Input Handling** and set it to **Both**.

## Team

*(Add your group member names and roles here.)*

## Course Context

Built for the Virtual Reality course project — demonstrates geometric transformations (camera-follow), motion modeling (lane-dodge movement), and real-time interaction (collision/trigger-based gameplay), implemented without VR hardware for broad accessibility.