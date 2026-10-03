# RushHour

A third-person 3D delivery-dodging game built in Unity, addressing **SDG 11: Sustainable Cities and Communities**.

> *Every second counts. Every pothole costs.*

## Story

You play as **Kai**, a courier for **GreenLine Logistics**, delivering a sealed shipment of temperature-sensitive vaccines to the **Greenfield Community Clinic** on the edge of the city of **Veridia** — a district the city's infrastructure planning has repeatedly overlooked.

Piloting a lightweight electric delivery pod, Kai must navigate streets plagued by potholes, stalled construction, and congestion — all symptoms of underinvested urban infrastructure — while managing limited battery charge before the clinic's refrigeration window closes.

## Gameplay

- **Movement:** The vehicle drives forward automatically. Use **A/D** or **Left/Right arrow keys** to dodge between lanes.
- **Battery/Charge:** Depletes constantly over time, and faster while dodging. Running out ends the delivery.
- **Obstacles:** Potholes, barricades, and debris spawn ahead with increasing frequency — colliding without a shield ends the run.
- **Protection Collectibles (Shields):** Glowing pickups representing small infrastructure investments (reinforced roads, cleared lanes). Grants temporary shield — absorbs one obstacle hit instead of crashing.
- **Charge Collectibles:** Restores a portion of your battery to keep the pod running.
- **Goal:** Reach the delivery distance (or the clinic itself) before running out of charge or crashing.

## Win / Fail States

| Outcome | Trigger |
|---|---|
| ✅ Delivered | Distance goal reached / clinic trigger entered |
| ❌ Crashed | Collision with an obstacle while unshielded |
| ❌ Out of Charge | Battery reaches zero before delivery |

## SDG Mapping

**SDG 11 — Sustainable Cities and Communities.** The game's obstacles represent real consequences of poor urban infrastructure (potholes, stalled construction, congestion) on last-mile logistics, while the battery mechanic reflects the added strain poor road conditions place on low-emission micro-vehicles. Protection collectibles represent the tangible benefit of targeted infrastructure investment.

## Current Scope

### Implemented
- Auto-forward driving with lane-dodge controls
- Battery system with dodge-based drain penalty and charge pickups
- Obstacle spawner with time-based difficulty ramp
- Shield/protection collectible system
- Distance-based delivery goal and Clinic trigger
- Win/fail state handling with restart
- Full UI (battery bar, distance counter, status text) via HudController
- Start screen / main menu with custom gritty UI
- Visual polish: Camera shake, pod signals, road scrolling

### Not Yet Implemented
- Kerala-themed environment art and Aura volumetric atmosphere
- Sound design
- Polished vehicle and obstacle models

## Scripts

| Script | Purpose |
|---|---|
| `CarController.cs` | Forward movement, lane-dodge input, crash handling |
| `BatterySystem.cs` | Battery drain (base + dodge penalty), triggers out of charge failure |
| `ShieldSystem.cs` | Temporary invincibility from collectibles |
| `ObstacleSpawner.cs` | Spawns obstacles/collectibles ahead of the car, ramps difficulty over time |
| `Obstacle.cs` | Collision detection, shield consumption, self-cleanup |
| `ShieldPickup.cs` / `ChargePickup.cs`| Pickup detection, activates shield or restores battery |
| `GameManager.cs` | Tracks delivery distance, win/fail states, restart |
| `HudController.cs` | Manages all in-game UI (distance, battery bar, game over screens) |
| `CameraShake.cs` / `PodSignals.cs` | Visual juice (shake on crash, turn signals/braking lights) |
| `RoadScroller.cs` | Visual effect of road moving beneath the pod |
| `StartScreenController.cs` | Manages the main menu and scene transitions |
| `StartScreenBackdrop.cs` | Animates the 3D backdrop of the start screen |
| `MenuButtonEffect.cs` | Stylized UI button interactions (tilting and brush strokes) |
| `ClinicTrigger.cs` | Detects when the pod reaches the final clinic destination |

## Setup

1. Tag the car GameObject as `Player`.
2. Attach `CarController`, `BatterySystem`, `ShieldSystem`, and `PodSignals` to the Car.
3. Parent the Main Camera to the Car (local position ~`0, 3, -6`, slight downward tilt) and attach `CameraShake`.
4. Create obstacle and collectible prefabs with trigger colliders, attach `Obstacle.cs`, `ShieldPickup.cs`, or `ChargePickup.cs` respectively.
5. Create empty `Spawner` and `GameManager` objects, attach `ObstacleSpawner.cs` and `GameManager.cs`.
6. Set up the Canvas UI and attach `HudController.cs`.
7. Wire up Inspector references (Car, GameManager, prefabs, UI elements) across all scripts.
8. **Note:** If you see an `InvalidOperationException` about `UnityEngine.Input`, go to **Edit → Project Settings → Player → Active Input Handling** and set it to **Both**.


## Course Context

Built for the Virtual Reality course project — demonstrates geometric transformations (camera-follow), motion modeling (lane-dodge movement), and real-time interaction (collision/trigger-based gameplay), implemented without VR hardware for broad accessibility.