# Android Vulkan Validation (common)

License and version pin shared by `org.mass4.turian.android.validation.arm64` and `org.mass4.turian.android.validation.x64`. Install the brick for the architecture you build; it pulls this one in.

Turian packages the layer only into Android development builds, and export rejects a non-development APK that contains it. To use it, also set `ValidationLayers` in Graphics Settings; reports appear in `adb logcat -s Turian`.

`dotnet run Brick~/Tools/refresh-layers.cs` downloads the pinned Khronos release, verifies its SHA-256 and 16 KB ELF alignment, and rewrites the native libraries, licenses and `Brick~/build.json` in all three bricks. Change `version` and `archiveSha256` in that file to move to another release.
