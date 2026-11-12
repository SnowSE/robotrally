Robot Rally as a desktop app. Install it once and it keeps itself up to date: new versions download in the background, and the projector offers **Restart** when one is ready (or it installs when you close the app).

| Computer | Download |
|---|---|
| Windows 10 / 11 | `RobotRally-win-Setup.exe` |
| Mac with Apple silicon (M1 and newer) | `RobotRally-osx-Setup.pkg` |
| Mac with an Intel chip | `RobotRally-osx-x64-Setup.pkg` |
| Linux (x64) | `RobotRally.AppImage` |

There are also `-Portable.zip` builds for Windows and Mac that run from a folder without installing (they update too). The `releases.*.json` and `.nupkg` files are how installed copies find and download updates; they aren't for downloading by hand.

The projector page opens in its own window, already logged in. Kids on other computers on the same network go to the address shown on the screen. To play on the same computer, click **🧒 Race on this computer too** under the join address (it opens a racer window in front of the projector).

**First run (the app isn't code-signed):**
- **Windows:** if you see "Windows protected your PC", click **More info → Run anyway**. When Windows Firewall asks, click **Allow** so other computers can join.
- **Mac:** when macOS says it can't verify the installer, open **System Settings → Privacy & Security** and click **Open Anyway**, then run it again. If the Mac firewall asks about incoming connections, click **Allow**.
- **Linux:** make the AppImage executable (`chmod +x RobotRally.AppImage`, or Properties → Allow executing) and run it. It needs WebKitGTK 4.1 (`webkit2gtk4.1` on Fedora, `libwebkit2gtk-4.1-0` on Ubuntu/Debian). If your firewall blocks it, open the port shown on the projector (usually 5000/tcp).

Kids open the projector's address in any browser. The app uses port 5000, or the next free one (5001, 5002, …) if something else has it (on a Mac that's often AirPlay Receiver); the address on the projector always has the right number. Other computers can reach the projector page at `/admin` with the password `rally`, or set `ROBOTRALLY_ADMIN_PASSWORD` before starting the app.
