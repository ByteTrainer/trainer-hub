# Tinykin runtime dependencies

- Harmony 2.3.3, MIT: https://github.com/pardeike/Harmony
  - Official NuGet package: https://api.nuget.org/v3-flatcontainer/lib.harmony/2.3.3/lib.harmony.2.3.3.nupkg
  - Payload: `lib/net472/0Harmony.dll`
  - SHA-256: `E96FC01100B6BBDC094B900EFE47850CF97AA9DF7C6A78CBE723B1DECF5A506A`
- Mono.Cecil: the existing project dependency used only by the installation tool.

Both upstream licenses are included in the signed package's `Mono.Cecil.LICENSE.txt`.

Only `Game.dll` is patched on disk. The original is preserved through the Hub's transaction and backup system. `DLC-Challenges.dll` is fingerprinted and remains unchanged; Harmony replaces only the race iterator's elapsed-time increment in memory. It preserves all other delta-time calls, including camera movement, countdowns and animations. The original delta resumes immediately when the control is disabled or the command lease expires.

Do not distribute game assemblies. Build against the local installation and publish only the trainer runtime, installer tool and licensed dependencies.

## Verified behavior

Steam build 10185644. Game original SHA-256: `32D8F41F78A1EA976330D2250896BA526971B4DFB2EA3E8CE022E192CA1ED886`.

Health protection, repeated air jumps, movement at 2×, unlimited bubble fuel, level gliding and camera following during air jumps were confirmed in the running game. The camera hook changes only the normal player-follow height, retaining the native smoothing, collision handling and cinematic cameras. Disabling air jumps and level gliding restores the native ground-height reference.

The race timer passed the native runtime and Harmony transpiler tests, and its patch loaded successfully under the game's Mono runtime. An actual race could not be tested in the current save because challenges were not unlocked. Null-tag deaths remain available for native fall recovery; the health control does not prevent fatal fall respawns.

## Communication while unfocused (1.0.1)

Unity may suspend `Update` when Tinykin loses focus, including minimization and opening the Hub overlay. A 250 ms managed timer maintains IPC after focus loss and a 750 ms gap in main-thread updates. It reads command leases and publishes the last main-thread readiness snapshot. It never calls Unity APIs or accesses Unity objects from the worker thread, and never forces the game to run or unpause in the background.

The worker cannot conceal a stalled foreground game: it stops publishing once focus returns unless the main loop resumes. Original PID checks, command expiry and the Hub's two-second response timeout remain unchanged. Shutdown disposes the timer. Automated tests cover a suspended main loop, changing controls in the background, expired leases, foreground stalls, Unity API isolation and actual timer disposal.
