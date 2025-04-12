# EmployeeAssignmentsRevived

**EmployeeAssignmentsRevived** is a full rebuild of the original "Employee Assignments" mod for **Lethal Company**, originally created by **amnsoft**. This version by **FluxTeam** modernizes the entire mod, adds configurability, professionalizes the codebase, and introduces new assignment mechanics.

---

## Features

- 📋 Assigns unique objectives to selected players each round.
- ⚙️ Three distinct assignment types:
  - Scrap Retrieval
  - Hunt & Kill Enemy
  - Repair Broken Valve
- 💵 Assignment completion earns bonus credits.
- 🧠 Modular assignment system supports future expansion.
- 🛠️ Fully configurable via BepInEx config file.
- 🌐 Host-only logic ensures compatibility in multiplayer sessions.
- 🔍 Optional in-game update checker with Thunderstore version integration.

---

## Installation

1. Download `EmployeeAssignmentsRevived.dll` from the [Releases](https://github.com/FluxTeam/EmployeeAssignmentsRevived/releases).
2. Drop it into your `BepInEx/plugins/` folder.
3. Launch **Lethal Company** as the host — you're good to go.

---

## Build Instructions

To compile the mod yourself:

1. Clone this repository.
2. Create a folder named `References/` in the project root and place the following DLLs from your Lethal Company install:
   - `Assembly-CSharp.dll`
   - `BepInEx.dll`
   - `netstandard.dll`
   - `Unity.InputSystem.dll`
   - `Unity.Netcode.Runtime.dll`
   - `Unity.TextMeshPro.dll`
   - `UnityEngine.AIModule.dll`
   - `UnityEngine.CoreModule.dll`
   - `UnityEngine.dll`
   - `UnityEngine.JSONSerializeModule.dll`
   - `UnityEngine.TextRenderingModule.dll`
   - `UnityEngine.UI.dll`
   - `UnityEngine.UIModule.dll`
   - `UnityEngine.UnityWebRequestModule.dll`

3. Open the solution in **Visual Studio** (targeting **.NET Framework 4.8**).
4. Build using the `Release` configuration.

You can also use the provided `build.bat` if your environment is configured.

---

## Configuration

After first launch, a config file will be generated at:

```
BepInEx/config/FluxTeam.EmployeeAssignmentsRevived.cfg
```

Example:

```ini
[HostOnly.General]
MaxAssignedPlayers = 10
MinAssignedPlayers = 1
AssignAllPlayers = false
AllPlayersCanComplete = false

[Assignment.ScrapRetrieval]
AssignmentReward = 100

[Assignment.RepairValve]
AssignmentReward = 100

[Assignment.HuntAndKill]
EnemyWhitelist = Centipede,Bunker Spider,Hoarding bug,Crawler
EnemyWeights = 50,25,50,25
EnemyRewards = 100,200,100,200
```

You can customize assignment weights, enemy targets, and reward values. Host-only options apply only if you're the host.

---

## Credits

- 🛠️ Original mod by [amnsoft](https://thunderstore.io/c/lethal-company/p/amnsoft/EmployeeAssignments/)
- 🧪 Rebuilt, maintained, and expanded by **FluxTeam**
- 🧃 Community testing by: Darkmega, Portokalis, Moroxide, atony, grey, HipposaauR

---

## License

MIT — free to use, modify, and share. Please credit **amnsoft** for the original idea.

---