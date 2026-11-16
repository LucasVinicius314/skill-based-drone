# Skill Based Drone

An FPV drone combat game made with Godot 4.7 (C#). Fly a drone with helicopter-style controls, shoot drones, tanks and worms with a 50 cal and homing missiles, and play co-op with friends over peer-to-peer multiplayer.

## Play

Paste one of these into a terminal, in an empty folder: it downloads the latest release, unpacks it and starts the game.

Windows (PowerShell):

```powershell
$ProgressPreference='SilentlyContinue'; Invoke-WebRequest https://github.com/LucasVinicius314/skill-based-drone/releases/latest/download/SkillBasedDrone-windows.zip -OutFile SkillBasedDrone-windows.zip; Expand-Archive SkillBasedDrone-windows.zip SkillBasedDrone -Force; .\SkillBasedDrone\SkillBasedDrone.exe
```

Linux:

```bash
curl -fL https://github.com/LucasVinicius314/skill-based-drone/releases/latest/download/SkillBasedDrone-linux.tar.gz -o SkillBasedDrone-linux.tar.gz && mkdir -p SkillBasedDrone && tar -xzf SkillBasedDrone-linux.tar.gz -C SkillBasedDrone && chmod +x SkillBasedDrone/SkillBasedDrone.x86_64 && ./SkillBasedDrone/SkillBasedDrone.x86_64
```

To update later, run `Update.bat` (Windows) or `update.sh` (Linux) in the game folder.

## Controls

| Input | Action |
|---|---|
| W / S | throttle |
| A / D | yaw |
| mouse | roll (X) and pitch (Y) |
| left click | 50 cal |
| right click | missile |
| X | flare |
| mouse wheel | zoom |
| V | third person camera |
| Alt | free look (third person) |
| Esc | pause menu |

## Multiplayer

Host a room from the menu and share the code, or join one by code or from your friends' rooms. Players are matched through a signaling server and then play peer to peer. The server address is set in the menu's Settings page.

## Development

See [BUILDING.md](BUILDING.md) to build and release, and [CLAUDE.md](CLAUDE.md) for the code layout and conventions. First time:

```
git clone --recurse-submodules <repo>
scripts/fetch-webrtc.ps1        # or: bash scripts/fetch-webrtc.sh
dotnet build SkillBasedDrone.csproj
```
