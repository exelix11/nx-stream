# osu-stream-nx

This is a switch homebrew port of osu!stream on top of the [mono runtime port](https://github.com/exelix11/mono-nx). To run this on your console you will need one that is able of running homebrew. To add additional maps to the ones provided in the original repo you will need to extract them from the android or ios version of the game.

Currently this repository does not ship binary releases as per licensing requirements, you will need to build the game and collect the needed assets yourself.

## Port features and limitations

- Base gameplay fully functional
- No online features available (store/rankings/news), this is a limitation of mono-nx since the relevant apis are not implemented
- No localisation support, this is a limitation of mono-nx since the relevant apis are not implemented
- No maps are provided, you will need to extract them from the original game
- Only touch input is supported, can only be played in portable mode
- Due to a number of changes in project structure and dependencies this branch will not build for any other platform, this is not meant to be merged to the original main branch.

https://github.com/user-attachments/assets/48c32b21-51e0-458a-a4ce-1e18bff9b953

## Build instructions

1) Setup the devkita64 toolchain and the [mono-nx sdk](https://github.com/exelix11/mono-nx#building)
2) In your local clone of the mono-nx repo run `source env.sh`
3) Build the interpreter fork in this repo
  - Dependency first: `cd mono-nx/fdk-aac && ./build.sh`
  - Then build the interpreter: `cd mono-nx/interpreter && make -j$(nproc)`
4) Build the managed osu stream fork: `cd osu!stream && dotnet build`

This is good enough for local development and testing, with this setup you can quickly  iterate code changes in the game by building only the managed code and running it in the interpreter. Note that this needs a full copy of the mono-nx framework dlls on your SD card. For releases you'll want to build the standalone AOT version:

1) Same prerequisites as above, make sure the interpreter built fine, then `cd mono-nx/aot`
3) Build the c# side and aot it `./build_aot.sh`, this produces the game as a set of native static libraries 
4) Build the actual nro with `make -j$(nproc)` 
5) Collect the release package with `./collect_sd_release.sh` and copy it to the SD card.
6) Consider tweaking mono_config.ini for debugging to enable/disable logging.

In case of issues reference the github actions build steps.

---

Original readme below

<p align="center">
  <img src="Artwork/osu logo white.jpg">
</p>

# osu!stream

tap, slide, hold and spin to a **beat you can feel!**

## Status

This is basically a “finished” project to me. For more information on the state and history of osu!stream, here's some further reading:

- Read my [blog post](https://blog.ppy.sh/osu-stream-2020-release/) about the final release
- ["osu!stream" on osu! wiki](https://osu.ppy.sh/help/wiki/osu!stream)
- Visit the [App Store](https://apps.apple.com/us/app/osu-stream/id436952197) or [Play Store](https://play.google.com/store/apps/details?id=sh.ppy.osustream) listing page

While contributions are welcomed I likely won’t have time to review anything too large. There are some exceptions listed below, mostly which fall under the clean-up umbrella – trying to get things into a good final state:

- Bring code standards in line with osu!lazer (using the same DotSettings configuration).
- Doing something about the amount of compile-time `#if`s in the code (especially in `using` blocks).
- Bringing the `arcade` branch up-to-date and potentially merging changes back into master.
- Documentation of any kind.
- Code quality improvements of any kind (as long as they can easily be reviewed and are guaranteed to not change behaviour). Keep individual PRs under 200 lines of change, optimally.

## Running

If you are looking to play osu!stream, the [app store](https://apps.apple.com/us/app/osu-stream/id436952197) or [play store](https://play.google.com/store/apps/details?id=sh.ppy.osustream) release is the best way to consume it.

## Building

The primary target of osu!stream is iOS. It should compile with relatively little effort via `osu!stream.sln` (tested via Visual Studio for Mac and Rider).

It will also run on desktop (tested only on windows) via `osu!stream_desktop.sln`. Note that the desktop release needs slightly differently packaged beatmaps (as it doesn't support `m4a` of released beatmaps).

In addition, there is an [arcade branch](https://github.com/ppy/osu-stream/tree/arcade) for the osu!arcade specific release. This branch really needs to be merged up-to-date with the latest master.

## Mapping

The process of mapping for osu!stream is still done via the osu! editor. I believe there was a custom build or mode in the editor to make it easier to place hitobjects at the same point in time, but should be possible out-of-the-box.

Tools for testing beatmaps are included (`StreamTester`) and there is [a branch](https://github.com/ppy/osu-stream/tree/mapper) for building a release of osu!stream with mapper-specific changes (heavily outdated and maybe not useful).

Some documentation exists [in this document](https://docs.google.com/document/d/1FYmHhRX-onR-osgTS6uHSOZuu_0JEbfRZePVySvvr9g/edit?usp=sharing) but beware that you will need some level of expertise to get the tools working and learn the process. If anyone decides to try mapping for osu!stream, I highly encourage you to contribute knowledge back in the form of pull requests to this `README` or a separate `MAPPING.md` if it gets too long.

## Licence

*osu!stream*'s code is released under the [MIT licence](https://opensource.org/licenses/MIT). Please see [the licence file](LICENCE) for more information. [tl;dr](https://tldrlegal.com/license/mit-license) you can do whatever you want as long as you include the original copyright and license notice in any copy of the software/source.

Please note that this *does not cover* the usage of the "osu!" or "ppy" branding in any software, resources, advertising or promotion, as this is protected by trademark law. As in don't go uploading builds of this without permission.

Also a word of caution that there may be exceptions to this license for specific resources included in this repository. The primary purpose of publicising this source code is for educational purposes; if you plan on using it in another way I ask that you contact me [via email](mailto:pe@ppy.sh) or [open an issue](https://github.com/ppy/osu-stream/issues) first!
